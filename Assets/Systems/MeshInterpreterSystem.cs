using SS.Resources;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;
using static SS.TextureUtils;

namespace SS.System {
  [CreateAfter(typeof(MaterialProviderSystem))]
  [UpdateInGroup(typeof(VariableRateSimulationSystemGroup))]
  public partial class MeshInterpeterSystem : SystemBase {

    private EntityQuery newMeshQuery;
    private EntityQuery activeMeshQuery;
    private EntityQuery removedMeshQuery;
    private EntityQuery animatedQuery;

    private EntityArchetype viewPartArchetype;

    private readonly ConcurrentDictionary<Entity, Mesh> entityMeshes = new();
    private readonly NativeHashMap<Entity, BatchMeshID> entityMeshIDs = new(ObjectConstants.NUM_OBJECTS, Allocator.Persistent);

    #region Dynamic mesh job variables
    private readonly VertexState[] vertexBuffer = new VertexState[1000];

    private readonly byte[] vertexColor = new byte[32];

    private readonly unsafe byte* parameterData = (byte*)UnsafeUtility.Malloc(4 * 100, 4, Allocator.Persistent);

    private DrawState drawState;
    private NativeList<Vertex> subMeshVertices;
    private NativeParallelMultiHashMap<ushort, ushort> subMeshIndices;
    #endregion

    private NativeArray<VertexAttributeDescriptor> vertexAttributes;
    private RenderMeshDescription renderMeshDescription;

    private ComponentLookup<ObjectInstance> instanceLookup;
    private ComponentLookup<ObjectInstance.Decoration> decorationLookup;

    private MaterialProviderSystem materialProviderSystem;

    private Resources.ObjectProperties objectProperties;
    private ShadeTableData shadeTable;

    protected override async void OnCreate() {
      base.OnCreate();

      RequireForUpdate<Level>();
      RequireForUpdate<AsyncLoadTag>();

      newMeshQuery = new EntityQueryBuilder(Allocator.Temp)
        .WithAll<MeshInfo>()
        .WithNone<MeshCachedTag>()
        .Build(this);

      activeMeshQuery = new EntityQueryBuilder(Allocator.Temp)
        .WithAll<MeshInfo, ObjectInstance, MeshCachedTag /*, ModelPartRebuildTag */>()
        .Build(this);

      removedMeshQuery = new EntityQueryBuilder(Allocator.Temp)
        .WithAll<MeshCachedTag>()
        .WithNone<MeshInfo>()
        .Build(this);

      animatedQuery = new EntityQueryBuilder(Allocator.Temp)
        .WithAll<AnimationData>()
        .Build(this);

      viewPartArchetype = World.EntityManager.CreateArchetype(
        typeof(ModelPart),

        typeof(LocalTransform),
        typeof(Parent),

        typeof(LocalToWorld),
        typeof(RenderBounds)
      );

      materialProviderSystem = World.GetOrCreateSystemManaged<MaterialProviderSystem>();

      vertexAttributes = new(4, Allocator.Persistent) {
        [0] = new VertexAttributeDescriptor(VertexAttribute.Position),
        [1] = new VertexAttributeDescriptor(VertexAttribute.Normal),
        [2] = new VertexAttributeDescriptor(VertexAttribute.Tangent),
        [3] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float16, 2)
      };

      renderMeshDescription = new RenderMeshDescription(
        shadowCastingMode: ShadowCastingMode.Off,
        receiveShadows: false,
        staticShadowCaster: false
      );

      instanceLookup = GetComponentLookup<ObjectInstance>(true);
      decorationLookup = GetComponentLookup<ObjectInstance.Decoration>(true);

      objectProperties = await Services.ObjectProperties;
      shadeTable = await Services.ShadeTable;

      EntityManager.AddComponent<AsyncLoadTag>(SystemHandle);
    }

    protected override void OnDestroy() {
      base.OnDestroy();

      entityMeshIDs.Dispose();
      vertexAttributes.Dispose();
    }

    protected override void OnUpdate() {
      var ecbSystem = World.GetExistingSystemManaged<EndVariableRateSimulationEntityCommandBufferSystem>();
      var commandBuffer = ecbSystem.CreateCommandBuffer();

      var entitiesGraphicsSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<EntitiesGraphicsSystem>();

      #region Cache mesh of new entities
      /*
      var addMeshToCacheJob = new AddMeshToCacheJob() {
        EntityTypeHandle = GetEntityTypeHandle(),
        CommandBuffer = commandBuffer.AsParallelWriter(),
        EntityMeshes = entityMeshes
      };

      Dependency = addMeshToCacheJob.ScheduleParallel(newMeshQuery, dependsOn: Dependency);
      */

      Entities
        .WithAll<MeshInfo>()
        .WithNone<MeshCachedTag>()
        .ForEach((Entity entity) => {
          var mesh = new Mesh();
          mesh.MarkDynamic();
          if (entityMeshes.TryAdd(entity, mesh) && entityMeshIDs.TryAdd(entity, entitiesGraphicsSystem.RegisterMesh(mesh))) {
            commandBuffer.AddComponent<MeshCachedTag>(entity);
            commandBuffer.AddComponent<ModelPartRebuildTag>(entity);
          } else {
            entityMeshes.Remove(entity, out Mesh tmp);
            entityMeshIDs.Remove(entity);
            UnityEngine.Object.Destroy(mesh);
          }
        })
        .WithoutBurst()
        .Run();
      #endregion


      var entityCount = activeMeshQuery.CalculateEntityCount();
      if (entityCount > 0) {
        instanceLookup.Update(this);
        decorationLookup.Update(this);

        // TODO Jobify

        using var entities = activeMeshQuery.ToEntityArray(Allocator.Temp);

        var meshDataArray = Mesh.AllocateWritableMeshData(entityCount); // No need to dispose
        var meshes = new Mesh[entityCount];

        var textureIds = new NativeArray<ushort>(entityCount * 4, Allocator.Temp);
        var textureDatas = new NativeArray<int>(entityCount, Allocator.Temp);

        subMeshIndices = new NativeParallelMultiHashMap<ushort, ushort>(256, Allocator.Temp);
        subMeshVertices = new NativeList<Vertex>(64, Allocator.Temp);

        using var animationData = animatedQuery.ToComponentDataArray<AnimationData>(Allocator.Temp);

        var textureIdAccumulator = 0;
        for (int entityIndex = 0; entityIndex < entityCount; ++entityIndex) {
          var entity = entities[entityIndex];
          
          if (entityMeshes.TryGetValue(entity, out meshes[entityIndex]) == false)
            throw new Exception(@"No mesh in cache.");

          var instanceData = instanceLookup.GetRefRO(entity).ValueRO;
          var meshInfo = SystemAPI.GetComponentRO<MeshInfo>(entity).ValueRO;
          var localTransform = SystemAPI.GetComponentRO<LocalTransform>(entity).ValueRO;

          #region Interpret, copy vertices and reorder indices, assing texture ids to submeshes
          {
            using (MemoryStream ms = new(meshInfo.Commands.Value.ToArray())) {
              using BinaryReader msbr = new(ms);

              subMeshIndices.Clear();
              subMeshVertices.Clear();
              drawState = default;

              IntepreterLoop(ms, msbr, localTransform.ToMatrix());
            }

            var (submeshKeys, submeshCount) = subMeshIndices.GetUniqueKeyArray(Allocator.Temp);
            var totalVertexCount = subMeshVertices.Length;
            var totalIndexCount = subMeshIndices.Count();

            // Debug.Log($"totalVertexCount {submeshCount} {totalVertexCount}");

            var meshData = meshDataArray[entityIndex];
            meshData.subMeshCount = submeshCount;
            meshData.SetVertexBufferParams(totalVertexCount, vertexAttributes);
            meshData.SetIndexBufferParams(totalIndexCount, IndexFormat.UInt16);

            // Debug.Log($"{entityIndex} Indice count requested {totalIndexCount} tvc {totalVertexCount}");

            ushort submeshIndexStart = 0;
            ushort indexCount = 0;
            var indices = meshData.GetIndexData<ushort>();
            var vertices = meshData.GetVertexData<Vertex>();
            vertices.CopyFrom(subMeshVertices.AsArray());

            for (int submeshIndex = 0; submeshIndex < submeshCount; ++submeshIndex) {
              if (subMeshIndices.TryGetFirstValue(submeshKeys[submeshIndex], out var index, out var indexIterator)) {
                do {
                  indices[indexCount++] = index;
                } while (subMeshIndices.TryGetNextValue(out index, ref indexIterator));
              }

              meshData.SetSubMesh(submeshIndex, new SubMeshDescriptor(submeshIndexStart, indexCount - submeshIndexStart, MeshTopology.Triangles));
              textureIds[textureIdAccumulator++] = submeshKeys[submeshIndex];
              submeshIndexStart = indexCount;
            }

            // Debug.Log($"{entityIndex} Indice count allocated {indexCount} vc {vertexCount}");

            submeshKeys.Dispose();
          }
          #endregion

          var level = SystemAPI.GetSingleton<Level>();
          var baseProperties = objectProperties.BasePropertyData(instanceData);

          // Debug.Log($"{instanceData.Class}:{instanceData.SubClass}:{instanceData.Info.Type} DrawType {baseProperties.DrawType} CurrentFrame {instanceData.Info.CurrentFrame}");

          // TODO could more fo this be moved to TextureUtils. CalculateTextureData already gets level and instanceData
          var objectIndex = level.ObjectReferences.Value[instanceData.CrossReferenceTableIndex].ObjectIndex;
          var isAnimating = IsAnimated(objectIndex, animationData.AsReadOnly());

          textureDatas[entityIndex] = CalculateTextureData(entity, baseProperties, instanceData, level, instanceLookup, decorationLookup, isAnimating);
        }

        // Debug.Log($"textureIdAccumulator {textureIdAccumulator} entities {entityCount} max {textureIds.Length}");

        Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, meshes);

        for (int meshIndex = 0; meshIndex < meshes.Length; ++meshIndex) {
          var mesh = meshes[meshIndex];
          mesh.RecalculateNormals();
          // mesh.RecalculateTangents();
          mesh.RecalculateBounds();
          mesh.UploadMeshData(false);
        }

        textureIdAccumulator = 0;
        for (int entityIndex = 0; entityIndex < entityCount; ++entityIndex) {
          var entity = entities[entityIndex];

          if (entityMeshIDs.TryGetValue(entity, out BatchMeshID meshID) == false)
            continue;

          if (entityMeshes.TryGetValue(entity, out Mesh mesh) == false)
            continue;

          var childCount = 0;
          DynamicBuffer<Child> children = default;

          if (EntityManager.HasBuffer<Child>(entity)) {
            children = EntityManager.GetBuffer<Child>(entity, true);
            childCount = children.Length;
          }

          var texturedMaterialID = materialProviderSystem.ParseTextureData(textureDatas[entityIndex], true, false, out var textureType, out var scale);

          var submeshCount = mesh.subMeshCount;
          for (ushort submeshIndex = 0; submeshIndex < Mathf.Max(submeshCount, childCount); ++submeshIndex) {
            var textureId = textureIds[textureIdAccumulator++];

            var materialID = textureId switch {
              ushort.MaxValue => materialProviderSystem.ColorMaterialID,
              0 => texturedMaterialID,
              _ => BatchMaterialID.Null
            };

            if (materialID == BatchMaterialID.Null)
              materialID = materialProviderSystem.GetMaterial((ushort)(ModelTextureIdBase + textureId), 0, true, false);

            if (submeshIndex < childCount) {
              var modelPart = children[submeshIndex].Value;

              if (submeshIndex >= submeshCount) {
                commandBuffer.DestroyEntity(modelPart);
                continue;
              }

              // Update mesh bounds and material
              commandBuffer.SetComponent(modelPart, new RenderBounds { Value = mesh.bounds.ToAABB() });
              commandBuffer.SetComponent(modelPart, new MaterialMeshInfo {
                MeshID = meshID,
                MaterialID = materialID,
                SubMesh = submeshIndex
              });
            } else {
              var modelPart = EntityManager.CreateEntity(viewPartArchetype); // Sync point
              RenderMeshUtility.AddComponents( // TODO should this be after if (see commented out section)
                modelPart,
                EntityManager,
                renderMeshDescription,
                new MaterialMeshInfo {
                  MeshID = meshID,
                  MaterialID = materialID,
                  SubMesh = submeshIndex
                }
              );

              commandBuffer.SetComponent(modelPart, new Parent { Value = entity });
              commandBuffer.SetComponent(modelPart, LocalTransform.Identity);
            }

            // commandBuffer.SetSharedComponent(viewPart, sceneTileTag);
          }
        }

        textureIds.Dispose();
        textureDatas.Dispose();
        subMeshIndices.Dispose();
        subMeshVertices.Dispose();

        // entityManager.RemoveComponent<ModelPartRebuildTag>(activeMeshQuery);
      }

      var removeMeshToCacheJob = new RemoveMeshFromCacheJob() {
        EntityTypeHandle = GetEntityTypeHandle(),
        CommandBuffer = commandBuffer.AsParallelWriter(),

        EntitiesGraphicsSystem = entitiesGraphicsSystem,
        EntityMeshes = entityMeshes,
        EntityMeshIDs = entityMeshIDs
      };

      Dependency = removeMeshToCacheJob.ScheduleParallel(removedMeshQuery, Dependency);
    }

    private unsafe void IntepreterLoop(MemoryStream ms, BinaryReader msbr, float4x4 objectLocalToWorld, int[] customParams = null) {
      float3 eyePositionLocal = math.transform(math.inverse(objectLocalToWorld), Camera.main.transform.position); // Camera position in object space.

      while (ms.Position < ms.Length) {
        long dataPos = ms.Position;
        OpCode command = (OpCode)msbr.ReadUInt16();

        // Debug.Log(command);

        if (command == OpCode.eof || command == OpCode.debug) {
          break;
        } else if (command == OpCode.jnorm) {
          ushort skipBytes = msbr.ReadUInt16();

          float3 normal = new(msbr.ReadFixed1616(), -msbr.ReadFixed1616(), msbr.ReadFixed1616());
          float3 point = new(msbr.ReadFixed1616(), -msbr.ReadFixed1616(), msbr.ReadFixed1616());

          var viewVec = point - eyePositionLocal;

          // Debug.Log($"n {normal} p {point} view {viewVec} dot {math.dot(viewVec, normal)}");

          // is normal pointin towards camera?
          if (math.dot(viewVec, normal) >= 0f) // Not facing.
            ms.Position = dataPos + skipBytes;
        } else if (command == OpCode.lnres) {
          ushort vertexA = msbr.ReadUInt16();
          ushort vertexB = msbr.ReadUInt16();

          // draw line a -> b
        } else if (command == OpCode.multires) {
          ushort count = msbr.ReadUInt16();
          ushort vertexStart = msbr.ReadUInt16();

          for (ushort i = 0; i < count; ++i) {
            vertexBuffer[vertexStart + i].position = new(msbr.ReadFixed1616(), -msbr.ReadFixed1616(), msbr.ReadFixed1616());
            vertexBuffer[vertexStart + i].flags = 0;
          }

        } else if (command == OpCode.polyres) {
          ushort count = msbr.ReadUInt16();
          ushort vertexCount = count;

          var vertexIndices = new NativeArray<ushort>(vertexCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
          // var colorUV = new half2(new((drawState.color & 0xFF) / 255f), new((drawState.color >> 8) / 255f));
          var colorUV = new half2(new((drawState.color & 0xFF) / 255f), half.zero);

          if (drawState.gouraud != Gouraud.normal)
            Debug.LogWarning($"Non implemented drawState.gouraud {drawState.gouraud}");

          while (count-- > 0)
            vertexIndices[count] = msbr.ReadUInt16();

          var origin = vertexBuffer[vertexIndices[0]].position;
          var normal = math.cross(vertexBuffer[vertexIndices[1]].position - origin, vertexBuffer[vertexIndices[2]].position - origin);
          var viewVec = origin - eyePositionLocal;

          if (drawState.check == false && math.dot(viewVec, normal) >= 0f) { // TODO check if math.dot this is really needed.
            int vertexStart = subMeshVertices.Length;

            for (int i = 0; i < vertexCount; ++i) {
              var vertexState = vertexBuffer[vertexIndices[i]];

              // TODO can we use 0 instaed of MaxValue?

              //if (drawState.gouraud == Gouraud.normal)
              subMeshVertices.Add(new Vertex { pos = vertexState.position, uv = colorUV });
              //else // TODO if needed
              //  subMeshVertices.Add(new Vertex { pos = vertexState.position, uv = half2.zero });
            }

            for (int i = 0; i < vertexCount - 2; ++i) {
              subMeshIndices.Add(ushort.MaxValue, (ushort)vertexStart);
              subMeshIndices.Add(ushort.MaxValue, (ushort)(vertexStart + i + 1));
              subMeshIndices.Add(ushort.MaxValue, (ushort)(vertexStart + i + 2));
            }
          }

          vertexIndices.Dispose();
        } else if (command == OpCode.setcolor) {
          drawState.color = (byte)msbr.ReadUInt16();
          drawState.gouraud = Gouraud.normal;
        } else if (command == OpCode.sortnorm) {
          float3 normal = new(msbr.ReadFixed1616(), -msbr.ReadFixed1616(), msbr.ReadFixed1616());
          float3 point = new(msbr.ReadFixed1616(), -msbr.ReadFixed1616(), msbr.ReadFixed1616());

          long firstOpcodePosition = dataPos + msbr.ReadUInt16();
          long secondOpcodePosition = dataPos + msbr.ReadUInt16();

          long continuePosition = ms.Position;

          var viewVec = point - eyePositionLocal;

          if (math.dot(viewVec, normal) < 0f) { // is normal pointin towards camera?
            ms.Position = firstOpcodePosition;
            IntepreterLoop(ms, msbr, objectLocalToWorld);
            ms.Position = secondOpcodePosition;
            IntepreterLoop(ms, msbr, objectLocalToWorld);
          } else {
            ms.Position = secondOpcodePosition;
            IntepreterLoop(ms, msbr, objectLocalToWorld);
            ms.Position = firstOpcodePosition;
            IntepreterLoop(ms, msbr, objectLocalToWorld);
          }

          ms.Position = continuePosition;
        } else if (command == OpCode.setshade) {
          ushort count = msbr.ReadUInt16();
          while (count-- > 0) {
            ref var vertex = ref vertexBuffer[msbr.ReadUInt16()];
            vertex.i = msbr.ReadUInt16();
            vertex.flags |= VertexFlag.I;
          }
        } else if (command == OpCode.goursurf) {
          drawState.gouraudColorBase = (ushort)(msbr.ReadUInt16() << 8);
          drawState.gouraud = Gouraud.spoly;
        } else if (command == OpCode.x_rel) {
          ushort vertexIndex = msbr.ReadUInt16();
          ushort referenceVertex = msbr.ReadUInt16();

          var vertex = vertexBuffer[referenceVertex];
          vertex.position.x += msbr.ReadFixed1616();
          vertex.flags = 0;
          vertexBuffer[vertexIndex] = vertex;
        } else if (command == OpCode.y_rel) {
          ushort vertexIndex = msbr.ReadUInt16();
          ushort referenceVertex = msbr.ReadUInt16();

          var vertex = vertexBuffer[referenceVertex];
          vertex.position.y += -msbr.ReadFixed1616();
          vertex.flags = 0;
          vertexBuffer[vertexIndex] = vertex;
        } else if (command == OpCode.z_rel) {
          ushort vertexIndex = msbr.ReadUInt16();
          ushort referenceVertex = msbr.ReadUInt16();

          var vertex = vertexBuffer[referenceVertex];
          vertex.position.z += msbr.ReadFixed1616();
          vertex.flags = 0;
          vertexBuffer[vertexIndex] = vertex;
        } else if (command == OpCode.xy_rel) {
          ushort vertexIndex = msbr.ReadUInt16();
          ushort referenceVertex = msbr.ReadUInt16();

          var vertex = vertexBuffer[referenceVertex];
          vertex.position.x += msbr.ReadFixed1616();
          vertex.position.y += -msbr.ReadFixed1616();
          vertex.flags = 0;
          vertexBuffer[vertexIndex] = vertex;
        } else if (command == OpCode.xz_rel) {
          ushort vertexIndex = msbr.ReadUInt16();
          ushort referenceVertex = msbr.ReadUInt16();

          var vertex = vertexBuffer[referenceVertex];
          vertex.position.x += msbr.ReadFixed1616();
          vertex.position.z += msbr.ReadFixed1616();
          vertex.flags = 0;
          vertexBuffer[vertexIndex] = vertex;
        } else if (command == OpCode.yz_rel) {
          ushort vertexIndex = msbr.ReadUInt16();
          ushort referenceVertex = msbr.ReadUInt16();

          var vertex = vertexBuffer[referenceVertex];
          vertex.position.y += -msbr.ReadFixed1616();
          vertex.position.z += msbr.ReadFixed1616();
          vertex.flags = 0;
          vertexBuffer[vertexIndex] = vertex;
        } else if (command == OpCode.icall_p) {
          long nextOpcode = dataPos + msbr.ReadUInt32();

          float3 subObjectPosition = new(msbr.ReadFixed1616(), -msbr.ReadFixed1616(), msbr.ReadFixed1616());
          var angle = (*(ushort*)(parameterData + msbr.ReadUInt16()) * 2f * math.PI) / 255f;
          var subobjectLocalToWorld = math.mul(objectLocalToWorld, math.mul(float4x4.RotateX(angle), float4x4.Translate(subObjectPosition)));

          var continuePosition = ms.Position;
          ms.Position = nextOpcode;
          IntepreterLoop(ms, msbr, subobjectLocalToWorld);

          ms.Position = continuePosition;
        } else if (command == OpCode.icall_b) {
          long nextOpcode = dataPos + msbr.ReadUInt32();

          float3 subObjectPosition = new(msbr.ReadFixed1616(), -msbr.ReadFixed1616(), msbr.ReadFixed1616());
          var angle = (*(ushort*)(parameterData + msbr.ReadUInt16()) * 2f * math.PI) / 255f;
          var subobjectLocalToWorld = math.mul(objectLocalToWorld, math.mul(float4x4.RotateZ(angle), float4x4.Translate(subObjectPosition)));

          var continuePosition = ms.Position;
          ms.Position = nextOpcode;
          IntepreterLoop(ms, msbr, subobjectLocalToWorld);

          ms.Position = continuePosition;
        } else if (command == OpCode.icall_h) {
          long nextOpcode = dataPos + msbr.ReadUInt32();

          float3 subObjectPosition = new(msbr.ReadFixed1616(), -msbr.ReadFixed1616(), msbr.ReadFixed1616());
          var angle = (*(ushort*)(parameterData + msbr.ReadUInt16()) * 2f * math.PI) / 255f;
          var subobjectLocalToWorld = math.mul(objectLocalToWorld, math.mul(float4x4.RotateY(angle), float4x4.Translate(subObjectPosition)));

          var continuePosition = ms.Position;
          ms.Position = nextOpcode;
          IntepreterLoop(ms, msbr, subobjectLocalToWorld);

          ms.Position = continuePosition;
        } else if (command == OpCode.sfcal) {
          long nextOpcode = dataPos + msbr.ReadUInt16();
          var continuePosition = ms.Position;
          IntepreterLoop(ms, msbr, objectLocalToWorld);
          ms.Position = continuePosition;
        } else if (command == OpCode.defres) {
          ushort vertexIndex = msbr.ReadUInt16();
          vertexBuffer[vertexIndex].position = new(msbr.ReadFixed1616(), -msbr.ReadFixed1616(), msbr.ReadFixed1616());
          vertexBuffer[vertexIndex].flags = 0;
        } else if (command == OpCode.defres_i) {
          ushort vertexIndex = msbr.ReadUInt16();
          VertexState vertex = default;
          vertex.position = new(msbr.ReadFixed1616(), -msbr.ReadFixed1616(), msbr.ReadFixed1616());
          vertex.flags = 0;

          vertex.i = msbr.ReadUInt16();
          vertex.flags |= VertexFlag.I;
          vertexBuffer[vertexIndex] = vertex;
        } else if (command == OpCode.getparms) {
          var dest = (int*)(parameterData + msbr.ReadUInt16());
          ushort src = msbr.ReadUInt16();
          ushort count = msbr.ReadUInt16();
          // In SS object rendering is called with variable amount of params. This copies them to array
          while (count-- > 0)
            *(dest++) = customParams[src++];
        } else if (command == OpCode.getparms_i) {
          var dest = *(int**)(parameterData + msbr.ReadUInt16()); // Notice, pointer of pointer.
          ushort src = msbr.ReadUInt16();
          ushort count = msbr.ReadUInt16();
          // In SS object rendering is called with variable amount of params. This copies them to array
          while (count-- > 0)
            *(dest++) = customParams[src++];
        } else if (command == OpCode.gour_p) {
          drawState.gouraudColorBase = (ushort)(*(parameterData + msbr.ReadUInt16()) << 8);
          drawState.gouraud = Gouraud.spoly;
        } else if (command == OpCode.gour_vc) {
          drawState.gouraudColorBase = (ushort)(vertexColor[msbr.ReadUInt16()] << 8);
          drawState.gouraud = Gouraud.spoly;
        } else if (command == OpCode.getvcolor) {
          ushort colorIndex = msbr.ReadUInt16();
          drawState.color = vertexColor[colorIndex];
          drawState.gouraud = Gouraud.normal;
        } else if (command == OpCode.getvscolor) {
          ushort colorIndex = msbr.ReadUInt16();
          ushort shade = msbr.ReadUInt16();
          drawState.color = shadeTable[(shade << 8) | vertexColor[colorIndex]];
        } else if (command == OpCode.rgbshades) {
          ushort count = msbr.ReadUInt16();
          while (count-- > 0) {
            ref var vertex = ref vertexBuffer[msbr.ReadUInt16()];
            vertex.rgb = msbr.ReadUInt32();
            vertex.flags |= VertexFlag.RGB;
            ms.Position += 4;
          }
        } else if (command == OpCode.draw_mode) {
          ushort flags = msbr.ReadUInt16();
          drawState.wire = ((flags >> 8) & 1) == 1;
          flags &= 0x00FF;
          flags <<= 1;
          drawState.check = ((flags >> 8) & 1) == 1;
          flags &= 0x00FF;
          flags <<= 2;
          drawState.gouraud = (Gouraud)(flags - 1);
        } else if (command == OpCode.getpcolor) {
          drawState.color = *(parameterData + msbr.ReadUInt16());
          drawState.gouraud = Gouraud.normal;
        } else if (command == OpCode.getpscolor) {
          ushort colorIndex = *(parameterData + msbr.ReadUInt16());
          ushort shade = msbr.ReadUInt16();
          drawState.color = shadeTable[(shade << 8) | (colorIndex & 0xFF)];
        } else if (command == OpCode.scaleres) {
          break;
        } else if (command == OpCode.vpnt_p) {
          ushort paramByteOffset = msbr.ReadUInt16();
          ushort vertexIndex = msbr.ReadUInt16();

          var p = *(g3s_point*)(parameterData + paramByteOffset);

          vertexBuffer[vertexIndex] = new VertexState {
            position = new(p.x / 65536f, p.y / 65536f, p.z / 65536f),
            uv = new(p.u / 65536f, 1f - p.v / 65536f),
            flags = (VertexFlag)p.p3_flags,
            i = (ushort)p.i,
            rgb = p.u, // if gouroud
          };
        } else if (command == OpCode.vpnt_v) {
          ushort vpointIndex = msbr.ReadUInt16();
          ushort vertexIndex = msbr.ReadUInt16();
          // vertexBuffer[vertexIndex] = _vpoint_tab[vpointIndex>>2];
        } else if (command == OpCode.setuv) {
          ref var vertex = ref vertexBuffer[msbr.ReadUInt16()];
          vertex.uv = new(msbr.ReadFixed1616(), 1f - msbr.ReadFixed1616());
          vertex.flags |= VertexFlag.U | VertexFlag.V;
        } else if (command == OpCode.uvlist) {
          ushort count = msbr.ReadUInt16();

          while (count-- > 0) {
            ref var vertex = ref vertexBuffer[msbr.ReadUInt16()];
            vertex.uv = new(msbr.ReadFixed1616(), 1f - msbr.ReadFixed1616());
            vertex.flags |= VertexFlag.U | VertexFlag.V;
          }
        } else if (command == OpCode.tmap) {
          ushort textureId = msbr.ReadUInt16();
          ushort vertexCount = msbr.ReadUInt16();

          int vertexStart = subMeshVertices.Length;

          for (int i = 0; i < vertexCount; ++i) {
            var vertexState = vertexBuffer[msbr.ReadUInt16()];
            subMeshVertices.Add(new Vertex { pos = vertexState.position, uv = new half2(vertexState.uv) });
          }

          for (int i = 0; i < vertexCount - 2; ++i) {
            subMeshIndices.Add(textureId, (ushort)(vertexStart + i + 2));
            subMeshIndices.Add(textureId, (ushort)(vertexStart + i + 1));
            subMeshIndices.Add(textureId, (ushort)vertexStart);
          }
        } else if (command == OpCode.dbg) {
          ushort skip = msbr.ReadUInt16();
          ushort code = msbr.ReadUInt16();
          ushort polygonId = msbr.ReadUInt16();
        }
      }
    }

    private struct AddMeshToCacheJob : IJobChunk {
      [ReadOnly] public EntityTypeHandle EntityTypeHandle;
      [WriteOnly] public EntityCommandBuffer.ParallelWriter CommandBuffer;
      public ConcurrentDictionary<Entity, Mesh> EntityMeshes;

      public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask) {
        var entities = chunk.GetNativeArray(EntityTypeHandle);

        for (int i = 0; i < chunk.Count; ++i) {
          var entity = entities[i];
          var mesh = new Mesh();
          mesh.MarkDynamic();
          EntityMeshes.TryAdd(entity, mesh);
          CommandBuffer.AddComponent<MeshCachedTag>(unfilteredChunkIndex, entity);
        }
      }
    }

    private struct RemoveMeshFromCacheJob : IJobChunk {
      [ReadOnly] public EntityTypeHandle EntityTypeHandle;
      [WriteOnly] public EntityCommandBuffer.ParallelWriter CommandBuffer;

      public EntitiesGraphicsSystem EntitiesGraphicsSystem;
      public ConcurrentDictionary<Entity, Mesh> EntityMeshes;
      public NativeHashMap<Entity, BatchMeshID> EntityMeshIDs;

      public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask) {
        var entities = chunk.GetNativeArray(EntityTypeHandle);

        for (int i = 0; i < chunk.Count; ++i) {
          var entity = entities[i];
          if (EntityMeshes.TryRemove(entity, out Mesh mesh)) {
            CommandBuffer.RemoveComponent<MeshCachedTag>(unfilteredChunkIndex, entity);

            if (EntityMeshIDs.TryGetValue(entity, out BatchMeshID meshID))
              EntitiesGraphicsSystem.UnregisterMesh(meshID);

            UnityEngine.Object.Destroy(mesh);
          }
        }
      }
    }

    public struct ModelPart : IComponentData { }

    public struct ModelPartRebuildTag : IComponentData { }

    internal struct MeshCachedTag : ICleanupComponentData { }

    internal enum OpCode : ushort {
      eof,
      jnorm,
      lnres,
      multires,
      polyres,
      setcolor,
      sortnorm,
      debug,
      setshade,
      goursurf,
      x_rel,
      y_rel,
      z_rel,
      xy_rel,
      xz_rel,
      yz_rel,
      icall_p,
      icall_b,
      icall_h,
      _reserved,
      sfcal,
      defres,
      defres_i,
      getparms,
      getparms_i,
      gour_p,
      gour_vc,
      getvcolor,
      getvscolor,
      rgbshades,
      draw_mode,
      getpcolor,
      getpscolor,
      scaleres,
      vpnt_p,
      vpnt_v,
      setuv,
      uvlist,
      tmap,
      dbg
    }

    internal enum Gouraud : byte {
      normal,
      tluc_poly,
      spoly,
      tluc_spoly,
      cpoly
    }

    [Flags]
    internal enum VertexFlag : byte {
      U = 1,
      V = 2,
      I = 4,
      PROJECTED = 8,
      RGB = 16,
      CLIPPNT = 32,
      LIT = 64
    }

    internal struct VertexState {
      public float3 position;
      public float2 uv;
      public ushort i;
      public uint rgb;
      public VertexFlag flags;
    }

    internal struct DrawState {
      public byte color;
      public bool wire;
      public bool check;
      public Gouraud gouraud;
      public ushort gouraudColorBase;
    }

    private struct g3s_point {
      public uint x, y, z;
      public uint sx, sy;
      public byte codes;
      public byte p3_flags;
      public uint u, v;
      public uint i;
    }

    internal struct Vertex {
      public float3 pos;
      public float3 normal;
      public float3 tangent;
      public half2 uv;
    }

    private struct AsyncLoadTag : IComponentData { }
  }
}
