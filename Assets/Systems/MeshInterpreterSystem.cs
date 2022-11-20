using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using SS.Resources;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Rendering;
using UnityEngine.ResourceManagement.AsyncOperations;
using static SS.TextureUtils;

namespace SS.System {
  [CreateAfter(typeof(MaterialProviderSystem))]
  [UpdateInGroup(typeof(VariableRateSimulationSystemGroup))]
  public partial class MeshInterpeterSystem : SystemBase {

    private EntityQuery newMeshQuery;
    private EntityQuery activeMeshQuery;
    private EntityQuery removedMeshQuery;

    private EntityArchetype viewPartArchetype;
    // private EntityQuery viewPartQuery;

    private ConcurrentDictionary<Entity, Mesh> entityMeshes = new();
    private NativeHashMap<Entity, BatchMeshID> entityMeshIDs = new(ObjectConstants.NUM_OBJECTS, Allocator.Persistent);

    #region Dynamic mesh job variables
    private VertexState[] vertexBuffer = new VertexState[1000];

    private byte[] vertexColor = new byte[32];

    unsafe private byte* parameterData = (byte*)UnsafeUtility.Malloc(4 * 100, 4, Allocator.Persistent);

    private DrawState drawState;
    private NativeList<Vertex> subMeshVertices;
    private NativeMultiHashMap<ushort, ushort> subMeshIndices;
    #endregion

    private NativeArray<VertexAttributeDescriptor> vertexAttributes;
    private RenderMeshDescription renderMeshDescription;

    private ComponentLookup<ObjectInstance> instanceLookup;
    private ComponentLookup<ObjectInstance.Decoration> decorationLookup;

    private MaterialProviderSystem materialProviderSystem;

    private Resources.ObjectProperties objectProperties;

    protected override void OnCreate() {
      base.OnCreate();

      RequireForUpdate<Level>();

      newMeshQuery = GetEntityQuery(new EntityQueryDesc {
        All = new ComponentType[] { ComponentType.ReadOnly<MeshInfo>() },
        None = new ComponentType[] { ComponentType.ReadOnly<MeshCachedTag>() },
      });

      activeMeshQuery = GetEntityQuery(new EntityQueryDesc {
        All = new ComponentType[] {
          ComponentType.ReadOnly<MeshInfo>(),
          ComponentType.ReadOnly<ObjectInstance>(),
          ComponentType.ReadOnly<ObjectInstance.Decoration>(),
          ComponentType.ReadOnly<MeshCachedTag>(),
          // ComponentType.ReadOnly<ModelPartRebuildTag>()
        }
      });

      removedMeshQuery = GetEntityQuery(new EntityQueryDesc {
        All = new ComponentType[] { ComponentType.ReadOnly<MeshCachedTag>() },
        None = new ComponentType[] { ComponentType.ReadOnly<MeshInfo>() },
      });

      viewPartArchetype = World.EntityManager.CreateArchetype(
        typeof(ModelPart),
        typeof(Parent),
        typeof(LocalToParentTransform),
        typeof(LocalToWorldTransform)
      );

      /*
      viewPartQuery = GetEntityQuery(new EntityQueryDesc {
        All = new ComponentType[] {
          ComponentType.ReadOnly<ModelPart>(),
          ComponentType.ReadOnly<Parent>()
        }
      });
      */

      this.materialProviderSystem = World.GetOrCreateSystemManaged<MaterialProviderSystem>();

      this.vertexAttributes = new (4, Allocator.Persistent) {
        [0] = new VertexAttributeDescriptor(VertexAttribute.Position),
        [1] = new VertexAttributeDescriptor(VertexAttribute.Normal),
        [2] = new VertexAttributeDescriptor(VertexAttribute.Tangent),
        [3] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float16, 2)
      };

      this.renderMeshDescription = new RenderMeshDescription(
        shadowCastingMode: ShadowCastingMode.On,
        receiveShadows: true,
        staticShadowCaster: true
      );

      this.instanceLookup = GetComponentLookup<ObjectInstance>(true);
      this.decorationLookup = GetComponentLookup<ObjectInstance.Decoration>(true);

      objectProperties = Services.ObjectProperties.WaitForCompletion();
    }

    protected override void OnDestroy() {
      base.OnDestroy();

      this.entityMeshIDs.Dispose();
      this.vertexAttributes.Dispose();
    }

    protected override void OnUpdate() {
      this.instanceLookup.Update(this);
      this.decorationLookup.Update(this);

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

      var addMeshToCacheJobHandle = addMeshToCacheJob.ScheduleParallel(newMeshQuery, dependsOn: Dependency);
      Dependency = addMeshToCacheJobHandle;
      ecbSystem.AddJobHandleForProducer(addMeshToCacheJobHandle);
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

      ecbSystem.AddJobHandleForProducer(Dependency);
      #endregion

      var entityCount = activeMeshQuery.CalculateEntityCount();
      if (entityCount > 0) {
        // TODO Jobify

        using var entities = activeMeshQuery.ToEntityArray(Allocator.Temp);
        using var instanceDatas = activeMeshQuery.ToComponentDataArray<ObjectInstance>(Allocator.Temp);
        using var decorationDatas = activeMeshQuery.ToComponentDataArray<ObjectInstance.Decoration>(Allocator.Temp);

        var meshDataArray = Mesh.AllocateWritableMeshData(entityCount); // No need to dispose
        var meshes = new Mesh[entityCount];

        var textureIds = new NativeArray<ushort>(entityCount * 4, Allocator.Temp);

        subMeshIndices = new NativeMultiHashMap<ushort, ushort>(256, Allocator.Temp);
        subMeshVertices = new NativeList<Vertex>(64, Allocator.Temp);

        var textureIdAccumulator = 0;
        for (int entityIndex = 0; entityIndex < entityCount; ++entityIndex) {
          var entity = entities[entityIndex];

          if (entityMeshes.TryGetValue(entity, out meshes[entityIndex]) == false)
            throw new Exception(@"No mesh in cache.");

          var meshInfo = GetComponent<MeshInfo>(entity);
          var localToWorld = GetComponent<LocalToWorldTransform>(entity);

          #region Interpret, copy vertices and reorder indices, assing texture ids to submeshes
          {
            using (MemoryStream ms = new MemoryStream(meshInfo.Commands.Value.ToArray())) {
              BinaryReader msbr = new BinaryReader(ms);

              subMeshIndices.Clear();
              subMeshVertices.Clear();
              drawState = default;
              
              intepreterLoop(ms, msbr, localToWorld.Value.ToMatrix());
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

        var prototype = EntityManager.CreateEntity(viewPartArchetype); // Sync point
        EntityManager.SetComponentData(prototype, new LocalToWorldTransform { Value = UniformScaleTransform.Identity });
        RenderMeshUtility.AddComponents(
          prototype,
          EntityManager,
          renderMeshDescription,
          new RenderMeshArray(new Material[0], new Mesh[0])
        );
        EntityManager.RemoveComponent<RenderMeshArray>(prototype);

        var level = GetSingleton<Level>();

        textureIdAccumulator = 0;
        for (int entityIndex = 0; entityIndex < entityCount; ++entityIndex) {
          var entity = entities[entityIndex];
          var instanceData = instanceDatas[entityIndex];
          var decorationData = decorationDatas[entityIndex];

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
          
          var baseProperties = objectProperties.BasePropertyData(instanceData);

          // Debug.Log($"{instanceData.Class}:{instanceData.SubClass}:{instanceData.Info.Type} DrawType {baseProperties.DrawType} CurrentFrame {instanceData.Info.CurrentFrame}");

          var submeshCount = mesh.subMeshCount;
          for (sbyte submeshIndex = 0; submeshIndex < Mathf.Max(submeshCount, childCount); ++submeshIndex) {
            Entity modelPart;
            if (submeshIndex < childCount) {
              modelPart = children[submeshIndex].Value;

              if (submeshIndex >= submeshCount) {
                commandBuffer.DestroyEntity(modelPart);
                continue;
              }
            } else {
              modelPart = commandBuffer.Instantiate(prototype);
              commandBuffer.SetComponent(modelPart, new Parent { Value = entity });
              commandBuffer.SetComponent(modelPart, new LocalToParentTransform { Value = UniformScaleTransform.Identity });
            }

            var textureId = textureIds[textureIdAccumulator++];

            var materialID = textureId switch {
              ushort.MaxValue => materialProviderSystem.ColorMaterialID,
              0 => materialProviderSystem.ParseTextureData(CalculateTextureData(instanceData, decorationData, level, instanceLookup, decorationLookup), true, out var textureType, out var scale),
              _ => BatchMaterialID.Null
            };

            if (materialID == BatchMaterialID.Null)
              materialID = materialProviderSystem.GetMaterial($"{ModelTextureIdBase + textureId}:{0}", true);

            commandBuffer.SetComponent(modelPart, new RenderBounds { Value = mesh.bounds.ToAABB() });
            commandBuffer.SetComponent(modelPart, new MaterialMeshInfo {
              MeshID = meshID,
              MaterialID = materialID,
              Submesh = submeshIndex
            });
            
            // commandBuffer.SetSharedComponent(viewPart, sceneTileTag);
          }
        }

        textureIds.Dispose();

        commandBuffer.DestroyEntity(prototype);

        // entityManager.RemoveComponent<ModelPartRebuildTag>(activeMeshQuery);
      }

      var removeMeshToCacheJob = new RemoveMeshFromCacheJob() {
        EntityTypeHandle = GetEntityTypeHandle(),
        CommandBuffer = commandBuffer.AsParallelWriter(),

        EntitiesGraphicsSystem = entitiesGraphicsSystem,
        EntityMeshes = entityMeshes,
        EntityMeshIDs = entityMeshIDs
      };

      var removeMeshToCacheJobHandle = removeMeshToCacheJob.ScheduleParallel(removedMeshQuery, Dependency);
      Dependency = removeMeshToCacheJobHandle;
      ecbSystem.AddJobHandleForProducer(removeMeshToCacheJobHandle);
    }

    private unsafe void intepreterLoop(MemoryStream ms, BinaryReader msbr, float4x4 objectLocalToWorld, int[] customParams = null) {
      float3 eyePositionLocal = math.transform(math.inverse(objectLocalToWorld), Camera.main.transform.position); // Camera position in object space.

      while (ms.Position < ms.Length) {
        long dataPos = ms.Position;
        OpCode command = (OpCode)msbr.ReadUInt16();

        // Debug.Log(command);

        if (command == OpCode.eof || command == OpCode.debug) {
          break;
        } else if (command == OpCode.jnorm) {
          ushort skipBytes = msbr.ReadUInt16();

          float3 normal = new float3(msbr.ReadFixed1616(), -msbr.ReadFixed1616(), msbr.ReadFixed1616());
          float3 point = new float3(msbr.ReadFixed1616(), -msbr.ReadFixed1616(), msbr.ReadFixed1616());

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
            vertexBuffer[vertexStart + i].position = new float3(msbr.ReadFixed1616(), -msbr.ReadFixed1616(), msbr.ReadFixed1616());
            vertexBuffer[vertexStart + i].flags = 0;
          }

        } else if (command == OpCode.polyres) {
          ushort count = msbr.ReadUInt16();
          ushort vertexCount = count;

          var vertexIndices = new NativeArray<ushort>(vertexCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
          var colorUV = new half2(new half((drawState.color & 0xFF) / 255f), new half((drawState.color >> 8) / 255f));

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
          float3 normal = new float3(msbr.ReadFixed1616(), -msbr.ReadFixed1616(), msbr.ReadFixed1616());
          float3 point = new float3(msbr.ReadFixed1616(), -msbr.ReadFixed1616(), msbr.ReadFixed1616());

          long firstOpcodePosition = dataPos + msbr.ReadUInt16();
          long secondOpcodePosition = dataPos + msbr.ReadUInt16();

          long continuePosition = ms.Position;

          var viewVec = point - eyePositionLocal;

          if (math.dot(viewVec, normal) < 0f) { // is normal pointin towards camera?
            ms.Position = firstOpcodePosition;
            intepreterLoop(ms, msbr, objectLocalToWorld);
            ms.Position = secondOpcodePosition;
            intepreterLoop(ms, msbr, objectLocalToWorld);
          } else {
            ms.Position = secondOpcodePosition;
            intepreterLoop(ms, msbr, objectLocalToWorld);
            ms.Position = firstOpcodePosition;
            intepreterLoop(ms, msbr, objectLocalToWorld);
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

          var subObjectPosition = new float3(msbr.ReadFixed1616(), -msbr.ReadFixed1616(), msbr.ReadFixed1616());
          var angle = (*(ushort*)(parameterData + msbr.ReadUInt16()) * 2f * math.PI) / 255f;
          var subobjectLocalToWorld = math.mul(objectLocalToWorld, math.mul(float4x4.RotateX(angle), float4x4.Translate(subObjectPosition)));

          var continuePosition = ms.Position;
          ms.Position = nextOpcode;
          intepreterLoop(ms, msbr, subobjectLocalToWorld);

          ms.Position = continuePosition;
        } else if (command == OpCode.icall_b) {
          long nextOpcode = dataPos + msbr.ReadUInt32();

          var subObjectPosition = new float3(msbr.ReadFixed1616(), -msbr.ReadFixed1616(), msbr.ReadFixed1616());
          var angle = (*(ushort*)(parameterData + msbr.ReadUInt16()) * 2f * math.PI) / 255f;
          var subobjectLocalToWorld = math.mul(objectLocalToWorld, math.mul(float4x4.RotateZ(angle), float4x4.Translate(subObjectPosition)));

          var continuePosition = ms.Position;
          ms.Position = nextOpcode;
          intepreterLoop(ms, msbr, subobjectLocalToWorld);

          ms.Position = continuePosition;
        } else if (command == OpCode.icall_h) {
          long nextOpcode = dataPos + msbr.ReadUInt32();

          var subObjectPosition = new float3(msbr.ReadFixed1616(), -msbr.ReadFixed1616(), msbr.ReadFixed1616());
          var angle = (*(ushort*)(parameterData + msbr.ReadUInt16()) * 2f * math.PI) / 255f;
          var subobjectLocalToWorld = math.mul(objectLocalToWorld, math.mul(float4x4.RotateY(angle), float4x4.Translate(subObjectPosition)));

          var continuePosition = ms.Position;
          ms.Position = nextOpcode;
          intepreterLoop(ms, msbr, subobjectLocalToWorld);

          ms.Position = continuePosition;
        } else if (command == OpCode.sfcal) {
          long nextOpcode = dataPos + msbr.ReadUInt16();
          var continuePosition = ms.Position;
          intepreterLoop(ms, msbr, objectLocalToWorld);
          ms.Position = continuePosition;
        } else if (command == OpCode.defres) {
          ushort vertexIndex = msbr.ReadUInt16();
          vertexBuffer[vertexIndex].position = new float3(msbr.ReadFixed1616(), -msbr.ReadFixed1616(), msbr.ReadFixed1616());
          vertexBuffer[vertexIndex].flags = 0;
        } else if (command == OpCode.defres_i) {
          ushort vertexIndex = msbr.ReadUInt16();
          VertexState vertex = default;
          vertex.position = new float3(msbr.ReadFixed1616(), -msbr.ReadFixed1616(), msbr.ReadFixed1616());
          vertex.flags = 0;
          
          vertex.i = msbr.ReadUInt16();
          vertex.flags |= VertexFlag.I;
          vertexBuffer[vertexIndex] = vertex;
        } else if (command == OpCode.getparms) {
          var dest = (int*)(parameterData + msbr.ReadUInt16());
          ushort src = msbr.ReadUInt16();
          ushort count = msbr.ReadUInt16();
          // In SS object rendering is called with variable amount of params. This copies them to array
          while (count-->0)
	          *(dest++) = customParams[src++];
        } else if (command == OpCode.getparms_i) {
          var dest = *(int**)(parameterData + msbr.ReadUInt16()); // Notice, pointer of pointer.
          ushort src = msbr.ReadUInt16();
          ushort count = msbr.ReadUInt16();
          // In SS object rendering is called with variable amount of params. This copies them to array
          while (count-->0)
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
          drawState.color = (ushort)((shade << 8) | vertexColor[colorIndex]);
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
          ushort colorIndex = msbr.ReadUInt16();
          ushort shade = msbr.ReadUInt16();
          drawState.color = (ushort)((shade << 8) | vertexColor[colorIndex]);
        } else if (command == OpCode.scaleres) {
          break;
        } else if (command == OpCode.vpnt_p) {
          ushort paramByteOffset = msbr.ReadUInt16();
          ushort vertexIndex = msbr.ReadUInt16();

          var p = *(g3s_point*)(parameterData + paramByteOffset);

          vertexBuffer[vertexIndex] = new VertexState {
            position = new float3(p.x / 65536f, p.y / 65536f, p.z / 65536f),
            uv = new float2(p.u / 65536f, 1f - p.v / 65536f),
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
          vertex.uv = new float2(msbr.ReadFixed1616(), 1f - msbr.ReadFixed1616());
          vertex.flags |= VertexFlag.U | VertexFlag.V;
        } else if (command == OpCode.uvlist) {
          ushort count = msbr.ReadUInt16();

          while (count-- > 0) {
            ref var vertex = ref vertexBuffer[msbr.ReadUInt16()];
            vertex.uv = new float2(msbr.ReadFixed1616(), 1f - msbr.ReadFixed1616());
            vertex.flags |= VertexFlag.U | VertexFlag.V;
          }
        } else if (command == OpCode.tmap) {
          ushort textureId = msbr.ReadUInt16();
          ushort count = msbr.ReadUInt16();
          ushort vertexCount = count;

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
      public ushort color; // SSCC, SS = shade, CC = color index
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
  }
}
