using SS.Resources;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Unity.Burst;
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
using Object = UnityEngine.Object;

namespace SS.System {
  [BurstCompile]
  [CreateAfter(typeof(MaterialProviderSystem))]
  [UpdateInGroup(typeof(VariableRateSimulationSystemGroup))]
  public partial class MeshInterpreterSystem : SystemBase {

    private EntityQuery newMeshQuery;
    private EntityQuery activeMeshQuery;
    private EntityQuery removedMeshQuery;

    private EntityArchetype viewPartArchetype;

    private readonly ConcurrentDictionary<Entity, Mesh> entityMeshes = new();
    private NativeHashMap<Entity, BatchMeshID> entityMeshIDs = new(ObjectConstants.NUM_OBJECTS, Allocator.Persistent);
    private NativeParallelHashMap<uint4, BatchMeshID> hashMeshIDs = new(ObjectConstants.NUM_OBJECTS * 10, Allocator.Persistent);

    private NativeArray<VertexAttributeDescriptor> vertexAttributes;
    private RenderMeshDescription renderMeshDescription;

    private ComponentLookup<ObjectInstance> instanceLookupRO;
    private ComponentLookup<ObjectInstance.Decoration> decorationLookupRO;

    private MaterialProviderSystem materialProviderSystem;

    private Resources.ObjectProperties objectProperties;
    private ShadeTableData shadeTable;

    protected override async void OnCreate() {
      base.OnCreate();

      RequireForUpdate<Level>();
      RequireForUpdate<AsyncLoadTag>();

      newMeshQuery = new EntityQueryBuilder(Allocator.Temp)
        .WithAll<MeshInfo, ObjectInstance>()
        .WithNone<MeshCachedTag>()
        .Build(this);

      activeMeshQuery = new EntityQueryBuilder(Allocator.Temp)
        .WithAll<MeshInfo, ObjectInstance, LocalToWorld, MeshCachedTag, HashChangedTag>()
        .Build(this);

      removedMeshQuery = new EntityQueryBuilder(Allocator.Temp)
        .WithAll<MeshCachedTag>()
        .WithNone<MeshInfo>()
        .Build(this);

      viewPartArchetype = World.EntityManager.CreateArchetype(stackalloc[] { 
        ComponentType.ReadWrite<ModelPart>(),
        
        ComponentType.ReadWrite<LocalTransform>(),
        ComponentType.ReadWrite<Parent>(),
        
        ComponentType.ReadWrite<LocalToWorld>(),
        ComponentType.ReadWrite<RenderBounds>(),
      });

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

      instanceLookupRO = GetComponentLookup<ObjectInstance>(true);
      decorationLookupRO = GetComponentLookup<ObjectInstance.Decoration>(true);

      objectProperties = await Services.ObjectProperties;
      shadeTable = await Services.ShadeTable;

      EntityManager.AddComponent<AsyncLoadTag>(SystemHandle);
    }

    protected override void OnDestroy() {
      base.OnDestroy();

      // TODO Should deregister meshes and dispose them too?
      
      entityMeshIDs.Dispose();
      hashMeshIDs.Dispose();
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

      foreach (var entity in newMeshQuery.ToEntityArray(WorldUpdateAllocator)) {
        var mesh = new Mesh();
        mesh.MarkDynamic();
        if (entityMeshes.TryAdd(entity, mesh) && entityMeshIDs.TryAdd(entity, entitiesGraphicsSystem.RegisterMesh(mesh))) {
          commandBuffer.AddComponent<MeshCachedTag>(entity);
          commandBuffer.AddComponent<HashChangedTag>(entity);
        } else {
          entityMeshes.Remove(entity, out Mesh tmp);
          entityMeshIDs.Remove(entity);
          Object.Destroy(mesh);
        }
      }
      #endregion
      
      var cameraWorldPosition = Camera.main?.transform.position ?? Vector3.zero;
      
      // 1. Gather hashes
      // 2. Compare hashes
      // 3. Generate meshes (cache new ones) & Update renderers

      #region Gather mesh hashes
      new CompareHashJob() {
        CameraWorldPosition = cameraWorldPosition
      }.ScheduleParallel();
      #endregion
      
      CompleteDependency();
      
      // TODO MaterialMeshInfo needs to be updated if materialID changes!
      
      var entityCount = activeMeshQuery.CalculateEntityCount();
      if (entityCount > 0) {
        // Debug.Log($"MeshInterpreter c:{entityCount}");
        
        instanceLookupRO.Update(this);
        decorationLookupRO.Update(this);
        
        var level = SystemAPI.GetSingleton<Level>();
        var entities = activeMeshQuery.ToEntityListAsync(WorldUpdateAllocator, out var entitiesListJobHandle);
        Dependency = JobHandle.CombineDependencies(Dependency, entitiesListJobHandle);
        
        var meshDataArray = Mesh.AllocateWritableMeshData(entityCount); // No need to dispose

        var textureDatas = new NativeParallelHashMap<Entity, int>(entityCount, Allocator.TempJob);
        new UpdateTextureData {
          Level = level,
          InstanceLookupRO = instanceLookupRO,
          DecorationLookupRO = decorationLookupRO,
          
          TextureDatas = textureDatas.AsParallelWriter(),
          
          ObjectDatasBlobAsset = objectProperties.ObjectDatasBlobAsset,
        }.ScheduleParallel(activeMeshQuery);
        
        var textureIds = new NativeParallelHashMap<Entity, UnsafeList<ushort>>(entityCount * 8, Allocator.TempJob);
        new BuildMeshJob {
          TextureIds = textureIds.AsParallelWriter(),
          
          MeshDataArray = meshDataArray,
          CameraWorldPosition = cameraWorldPosition,
          
          VertexAttributes = vertexAttributes,
          
          ShadeTable = shadeTable
        }.ScheduleParallel(activeMeshQuery); // NOTE this job disables HashChangedTag
        
        CompleteDependency();

        // Debug.Log($"textureIdAccumulator {textureIdAccumulator} entities {entityCount} max {textureIds.Length}");
        
        var meshes = new Mesh[entityCount];
        for (var entityIndex = 0; entityIndex < entityCount; ++entityIndex) {
          var entity = entities[entityIndex];

          if (!entityMeshes.TryGetValue(entity, out meshes[entityIndex]))
            throw new Exception(@"No mesh in cache.");
        }

        Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, meshes);

        foreach (var mesh in meshes) {
          mesh.RecalculateNormals();
          mesh.RecalculateBounds();
          mesh.UploadMeshData(false);
        }
        
        foreach (var entity in entities) {
          if (!entityMeshIDs.TryGetValue(entity, out var meshID))
            continue;

          if (!entityMeshes.TryGetValue(entity, out var mesh))
            continue;
          
          if (!textureDatas.TryGetValue(entity, out var textureData))
            continue;
          
          if (!textureIds.TryGetValue(entity, out var submeshTextureIds))
            continue;

          var childCount = 0;
          DynamicBuffer<Child> children = default;

          if (EntityManager.HasBuffer<Child>(entity)) {
            children = EntityManager.GetBuffer<Child>(entity, true);
            childCount = children.Length;
          }

          var texturedMaterialID = materialProviderSystem.ParseTextureData(textureData, true, false, out var textureType, out var scale);
          
          var submeshCount = mesh.subMeshCount;
          for (ushort submeshIndex = 0; submeshIndex < Mathf.Max(submeshCount, childCount); ++submeshIndex) {
            var textureId = submeshTextureIds[submeshIndex];

            var materialID = textureId switch {
              ushort.MaxValue => materialProviderSystem.ColorMaterialID,
              0 => texturedMaterialID,
              _ => BatchMaterialID.Null
            };

            if (materialID == BatchMaterialID.Null)
              materialID = materialProviderSystem.GetMaterial((ushort)(ModelTextureIdBase + textureId), 0, true, false, false);

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
              SystemAPI.SetComponent(modelPart, new Parent { Value = entity });
              SystemAPI.SetComponent(modelPart, LocalTransform.Identity);
              RenderMeshUtility.AddComponents(
                modelPart,
                EntityManager,
                renderMeshDescription,
                new MaterialMeshInfo {
                  MeshID = meshID,
                  MaterialID = materialID,
                  SubMesh = submeshIndex
                }
              );
            }
          }
        }

        textureIds.Dispose(Dependency);
        textureDatas.Dispose(Dependency);
      }

      var removeMeshToCacheJob = new RemoveMeshFromCacheJob() {
        EntityTypeHandle = GetEntityTypeHandle(),
        CommandBuffer = commandBuffer.AsParallelWriter(),

        EntitiesGraphicsSystem = entitiesGraphicsSystem,
        EntityMeshes = entityMeshes,
        EntityMeshIDs = entityMeshIDs
      };
      Dependency = removeMeshToCacheJob.ScheduleParallelByRef(removedMeshQuery, Dependency);
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

            Object.Destroy(mesh);
          }
        }
      }
    }

    [BurstCompile]
    [WithPresent(typeof(HashChangedTag))]
    private partial struct CompareHashJob : IJobEntity {
      [ReadOnly] public float3 CameraWorldPosition;
      
      private void Execute(EnabledRefRW<HashChangedTag> hashChanged, in LocalToWorld localToWorld, ref MeshInfo meshInfo) {
        var xxHash3 = new xxHash3.StreamingState();
        
        unsafe {
          var parameterDataPtr = (byte*)UnsafeUtility.Malloc(4 * 100, UnsafeUtility.AlignOf<byte>(), Allocator.Temp);
          var bbr = new BufferBinaryReader((byte*)meshInfo.Commands.Value.GetUnsafePtr(), meshInfo.Commands.Value.Length);
          InterpreterLoopHash(ref bbr, ref xxHash3, parameterDataPtr, localToWorld.Value);
        }

        var hash = xxHash3.DigestHash128();
        // Debug.Log($"m.vh:{meshInfo.VariantHash} h:{hash}");

        if (meshInfo.VariantHash.Equals(hash)) return;
        
        hashChanged.ValueRW = true;
        meshInfo.VariantHash = hash;
      }
      
      [BurstCompile]
      private unsafe void InterpreterLoopHash(ref BufferBinaryReader bbr, ref xxHash3.StreamingState xxHash3, byte* parameterDataPtr, float4x4 objectLocalToWorld) {
        float3 eyePositionLocal = math.transform(math.inverse(objectLocalToWorld), CameraWorldPosition); // Camera position in object space.
        
        while (bbr.Position < bbr.Length) {
          long dataPos = bbr.Position;
          OpCode command = bbr.Read<OpCode>();
          
          xxHash3.Update(command);
  
          if (command is OpCode.eof or OpCode.debug) {
            break;
          } else if (command == OpCode.jnorm) {
            ushort skipBytes = bbr.Read<ushort>();
  
            float3 normal = new(bbr.ReadFixed1616(), -bbr.ReadFixed1616(), bbr.ReadFixed1616());
            float3 point = new(bbr.ReadFixed1616(), -bbr.ReadFixed1616(), bbr.ReadFixed1616());
  
            var viewVec = point - eyePositionLocal;
  
            // is normal pointin towards camera?
            if (math.dot(viewVec, normal) >= 0f) { // Not facing.
              bbr.Position = dataPos + skipBytes;
              xxHash3.Update(skipBytes);
            }
          } else if (command == OpCode.lnres) {
            bbr.Skip<ushort>();
            bbr.Skip<ushort>();
            // draw line a -> b
          } else if (command == OpCode.multires) {
            ushort count = bbr.Read<ushort>();
            bbr.Skip<ushort>();
  
            for (ushort i = 0; i < count; ++i) {
              bbr.SkipFixed1616();
              bbr.SkipFixed1616();
              bbr.SkipFixed1616();
            }
          } else if (command == OpCode.polyres) {
            ushort count = bbr.Read<ushort>();
            while (count-- > 0)
              bbr.Skip<ushort>();
          } else if (command == OpCode.setcolor) {
            bbr.Skip<ushort>();
          } else if (command == OpCode.sortnorm) {
            float3 normal = new(bbr.ReadFixed1616(), -bbr.ReadFixed1616(), bbr.ReadFixed1616());
            float3 point = new(bbr.ReadFixed1616(), -bbr.ReadFixed1616(), bbr.ReadFixed1616());
  
            long firstOpcodePosition = dataPos + bbr.Read<ushort>();
            long secondOpcodePosition = dataPos + bbr.Read<ushort>();
  
            long continuePosition = bbr.Position;
  
            var viewVec = point - eyePositionLocal;
  
            if (math.dot(viewVec, normal) < 0f) { // is normal pointin towards camera?
              bbr.Position = firstOpcodePosition;
              xxHash3.Update(firstOpcodePosition);
              InterpreterLoopHash(ref bbr, ref xxHash3, parameterDataPtr, objectLocalToWorld);
              bbr.Position = secondOpcodePosition;
              xxHash3.Update(secondOpcodePosition);
              InterpreterLoopHash(ref bbr, ref xxHash3, parameterDataPtr, objectLocalToWorld);
            } else {
              bbr.Position = secondOpcodePosition;
              xxHash3.Update(secondOpcodePosition);
              InterpreterLoopHash(ref bbr, ref xxHash3, parameterDataPtr, objectLocalToWorld);
              bbr.Position = firstOpcodePosition;
              xxHash3.Update(firstOpcodePosition);
              InterpreterLoopHash(ref bbr, ref xxHash3, parameterDataPtr, objectLocalToWorld);
            }
  
            bbr.Position = continuePosition;
          } else if (command == OpCode.setshade) {
            ushort count = bbr.Read<ushort>();
            while (count-- > 0) {
              bbr.Skip<ushort>();
              bbr.Skip<ushort>();
            }
          } else if (command == OpCode.goursurf) {
            bbr.Skip<ushort>();
          } else if (command == OpCode.x_rel) {
            bbr.Skip<ushort>();
            bbr.Skip<ushort>();
            bbr.SkipFixed1616();
          } else if (command == OpCode.y_rel) {
            bbr.Skip<ushort>();
            bbr.Skip<ushort>();
            bbr.SkipFixed1616();
          } else if (command == OpCode.z_rel) {
            bbr.Skip<ushort>();
            bbr.Skip<ushort>();
            bbr.SkipFixed1616();
          } else if (command == OpCode.xy_rel) {
            bbr.Skip<ushort>();
            bbr.Skip<ushort>();
            bbr.SkipFixed1616();
            bbr.SkipFixed1616();
          } else if (command == OpCode.xz_rel) {
            bbr.Skip<ushort>();
            bbr.Skip<ushort>();
            bbr.SkipFixed1616();
            bbr.SkipFixed1616();
          } else if (command == OpCode.yz_rel) {
            bbr.Skip<ushort>();
            bbr.Skip<ushort>();
            bbr.SkipFixed1616();
            bbr.SkipFixed1616();
          } else if (command == OpCode.icall_p) {
            long nextOpcode = dataPos + bbr.Read<uint>();
  
            float3 subObjectPosition = new(bbr.ReadFixed1616(), -bbr.ReadFixed1616(), bbr.ReadFixed1616());
            var angle = (*(ushort*)(parameterDataPtr + bbr.Read<ushort>()) * 2f * math.PI) / 255f;
            var subobjectLocalToWorld = math.mul(objectLocalToWorld, math.mul(float4x4.RotateX(angle), float4x4.Translate(subObjectPosition)));
  
            var continuePosition = bbr.Position;
            bbr.Position = nextOpcode;
            xxHash3.Update(nextOpcode);
            InterpreterLoopHash(ref bbr, ref xxHash3, parameterDataPtr, subobjectLocalToWorld);
  
            bbr.Position = continuePosition;
          } else if (command == OpCode.icall_b) {
            long nextOpcode = dataPos + bbr.Read<uint>();
  
            float3 subObjectPosition = new(bbr.ReadFixed1616(), -bbr.ReadFixed1616(), bbr.ReadFixed1616());
            var angle = (*(ushort*)(parameterDataPtr + bbr.Read<ushort>()) * 2f * math.PI) / 255f;
            var subobjectLocalToWorld = math.mul(objectLocalToWorld, math.mul(float4x4.RotateZ(angle), float4x4.Translate(subObjectPosition)));
  
            var continuePosition = bbr.Position;
            bbr.Position = nextOpcode;
            xxHash3.Update(nextOpcode);
            InterpreterLoopHash(ref bbr, ref xxHash3, parameterDataPtr, subobjectLocalToWorld);
  
            bbr.Position = continuePosition;
          } else if (command == OpCode.icall_h) {
            long nextOpcode = dataPos + bbr.Read<uint>();
  
            float3 subObjectPosition = new(bbr.ReadFixed1616(), -bbr.ReadFixed1616(), bbr.ReadFixed1616());
            var angle = (*(ushort*)(parameterDataPtr + bbr.Read<ushort>()) * 2f * math.PI) / 255f;
            var subobjectLocalToWorld = math.mul(objectLocalToWorld, math.mul(float4x4.RotateY(angle), float4x4.Translate(subObjectPosition)));
  
            var continuePosition = bbr.Position;
            bbr.Position = nextOpcode;
            xxHash3.Update(nextOpcode);
            InterpreterLoopHash(ref bbr, ref xxHash3, parameterDataPtr, subobjectLocalToWorld);
  
            bbr.Position = continuePosition;
          } else if (command == OpCode.sfcal) {
            long nextOpcode = dataPos + bbr.Read<ushort>();
            
            var continuePosition = bbr.Position;
            bbr.Position = nextOpcode;
            xxHash3.Update(nextOpcode);
            InterpreterLoopHash(ref bbr, ref xxHash3, parameterDataPtr, objectLocalToWorld);
            
            bbr.Position = continuePosition;
          } else if (command == OpCode.defres) {
            bbr.Skip<ushort>();
            bbr.SkipFixed1616();
            bbr.SkipFixed1616();
            bbr.SkipFixed1616();
          } else if (command == OpCode.defres_i) {
            bbr.Skip<ushort>();
            bbr.SkipFixed1616();
            bbr.SkipFixed1616();
            bbr.SkipFixed1616();
            bbr.Skip<ushort>();
          } else if (command == OpCode.getparms) {
            bbr.Skip<ushort>();
            bbr.Skip<ushort>();
            bbr.Skip<ushort>();
          } else if (command == OpCode.getparms_i) {
            bbr.Skip<ushort>();
            bbr.Skip<ushort>();
            bbr.Skip<ushort>();
          } else if (command == OpCode.gour_p) {
            bbr.Skip<ushort>();
          } else if (command == OpCode.gour_vc) {
            bbr.Skip<ushort>();
          } else if (command == OpCode.getvcolor) {
            bbr.Skip<ushort>();
          } else if (command == OpCode.getvscolor) {
            bbr.Skip<ushort>();
            bbr.Skip<ushort>();
          } else if (command == OpCode.rgbshades) {
            ushort count = bbr.Read<ushort>();
            while (count-- > 0) {
              bbr.Skip<ushort>();
              bbr.Skip<uint>();
              bbr.Position += 4;
            }
          } else if (command == OpCode.draw_mode) {
            bbr.Skip<ushort>();
          } else if (command == OpCode.getpcolor) {
            bbr.Skip<ushort>();
          } else if (command == OpCode.getpscolor) {
            bbr.Skip<ushort>();
            bbr.Skip<ushort>();
          } else if (command == OpCode.scaleres) {
            break;
          } else if (command == OpCode.vpnt_p) {
            bbr.Skip<ushort>();
            bbr.Skip<ushort>();
          } else if (command == OpCode.vpnt_v) {
            bbr.Skip<ushort>();
            bbr.Skip<ushort>();
          } else if (command == OpCode.setuv) {
            bbr.Skip<ushort>();
            bbr.SkipFixed1616();
            bbr.SkipFixed1616();
          } else if (command == OpCode.uvlist) {
            ushort count = bbr.Read<ushort>();
            while (count-- > 0) {
              bbr.Skip<ushort>();
              bbr.SkipFixed1616();
              bbr.SkipFixed1616();
            }
          } else if (command == OpCode.tmap) {
            bbr.Skip<ushort>();
            ushort vertexCount = bbr.Read<ushort>();
            for (int i = 0; i < vertexCount; ++i)
              bbr.Skip<ushort>();
          } else if (command == OpCode.dbg) {
            bbr.Skip<ushort>();
            bbr.Skip<ushort>();
            bbr.Skip<ushort>();
          }
        }
      }
    }

    [BurstCompile]
    private partial struct UpdateTextureData : IJobEntity {
      [ReadOnly] public Level Level;
            
      [ReadOnly] public ComponentLookup<ObjectInstance> InstanceLookupRO;
      [ReadOnly] public ComponentLookup<ObjectInstance.Decoration> DecorationLookupRO;
      
      [ReadOnly] public BlobAssetReference<ObjectPropertiesBlob> ObjectDatasBlobAsset;
      
      [WriteOnly] public NativeParallelHashMap<Entity, int>.ParallelWriter TextureDatas;
      
      private void Execute(Entity entity, in ObjectInstance instanceData) {
        var baseProperties = ObjectDatasBlobAsset.Value.BasePropertyData(instanceData);

        // Debug.Log($"{instanceData.Class}:{instanceData.SubClass}:{instanceData.Info.Type} DrawType {baseProperties.DrawType} CurrentFrame {instanceData.Info.CurrentFrame}");

        TextureDatas.TryAdd(entity, CalculateTextureData(entity, baseProperties, instanceData, Level, ref InstanceLookupRO, ref DecorationLookupRO));
      }
    }
    
    [BurstCompile]
    private partial struct BuildMeshJob : IJobEntity {
      [WriteOnly] public NativeParallelHashMap<Entity, UnsafeList<ushort>>.ParallelWriter TextureIds;
      
      public Mesh.MeshDataArray MeshDataArray;
      [ReadOnly] public float3 CameraWorldPosition;
      
      [ReadOnly] public NativeArray<VertexAttributeDescriptor> VertexAttributes;
      
      [ReadOnly] public ShadeTableData ShadeTable;
      
      private unsafe struct BuildContext {
        public DrawState drawState;
        public NativeParallelMultiHashMap<ushort, ushort> subMeshIndices;
        public NativeList<Vertex> subMeshVertices;
        public NativeArray<VertexState> vertexBuffer;
        public NativeArray<byte> vertexColor;
        public byte* parameterDataPtr;
      }
      
      private void Execute([EntityIndexInQuery] int entityIndexInQuery, EnabledRefRW<HashChangedTag> hashChanged, Entity entity, in ObjectInstance instanceData, in MeshInfo meshInfo, in LocalToWorld localToWorld) {
        #region Interpret, copy vertices and reorder indices, assing texture ids to submeshes
        var buildContext = new BuildContext {
          drawState = default,
          subMeshIndices = new NativeParallelMultiHashMap<ushort, ushort>(256, Allocator.Temp),
          subMeshVertices = new NativeList<Vertex>(64, Allocator.Temp),
          vertexBuffer = new NativeArray<VertexState>(512, Allocator.Temp),
          vertexColor = new NativeArray<byte>(32, Allocator.Temp)
        };
        
        unsafe {
          buildContext.parameterDataPtr = (byte*)UnsafeUtility.Malloc(4 * 100, UnsafeUtility.AlignOf<byte>(), Allocator.Temp);
          var bbr = new BufferBinaryReader((byte*)meshInfo.Commands.Value.GetUnsafePtr(), meshInfo.Commands.Value.Length);
          InterpreterLoop(ref bbr, ref buildContext, localToWorld.Value);
        }

        ref var subMeshIndices = ref buildContext.subMeshIndices;
        ref var subMeshVertices = ref buildContext.subMeshVertices;
        
        var (submeshKeys, submeshCount) = subMeshIndices.GetUniqueKeyArray(Allocator.Temp);
        var totalVertexCount = subMeshVertices.Length;
        var totalIndexCount = subMeshIndices.Count();

        // Debug.Log($"totalVertexCount {submeshCount} {totalVertexCount}");

        var meshData = MeshDataArray[entityIndexInQuery];
        meshData.subMeshCount = submeshCount;
        meshData.SetVertexBufferParams(totalVertexCount, VertexAttributes);
        meshData.SetIndexBufferParams(totalIndexCount, IndexFormat.UInt16);

        // Debug.Log($"{entityIndex} Indice count requested {totalIndexCount} tvc {totalVertexCount}");

        ushort submeshIndexStart = 0;
        ushort indexCount = 0;
        var indices = meshData.GetIndexData<ushort>();
        var vertices = meshData.GetVertexData<Vertex>();
        vertices.CopyFrom(subMeshVertices.AsArray());

        for (var submeshIndex = 0; submeshIndex < submeshCount; ++submeshIndex) {
          if (subMeshIndices.TryGetFirstValue(submeshKeys[submeshIndex], out var index, out var indexIterator)) {
            do {
              indices[indexCount++] = index;
            } while (subMeshIndices.TryGetNextValue(out index, ref indexIterator));
          }

          meshData.SetSubMesh(submeshIndex, new SubMeshDescriptor(submeshIndexStart, indexCount - submeshIndexStart, MeshTopology.Triangles));

          unsafe {
            TextureIds.TryAdd(entity, new UnsafeList<ushort>((ushort*)submeshKeys.GetUnsafePtr(), submeshKeys.Length));
          }
          
          submeshIndexStart = indexCount;
        }

        // Debug.Log($"{entityIndex} Indice count allocated {indexCount} vc {vertexCount}");
        #endregion

        hashChanged.ValueRW = false;
      }
      
      [BurstCompile]
      private unsafe void InterpreterLoop(ref BufferBinaryReader bbr, ref BuildContext context, float4x4 objectLocalToWorld, ReadOnlySpan<int> customParams = default) {
        float3 eyePositionLocal = math.transform(math.inverse(objectLocalToWorld), CameraWorldPosition); // Camera position in object space.
        
        ref var drawState = ref context.drawState;
        ref var subMeshIndices = ref context.subMeshIndices;
        ref var subMeshVertices = ref context.subMeshVertices;
        ref var vertexBuffer = ref context.vertexBuffer;
        ref var vertexColor = ref context.vertexColor;
        var parameterDataPtr = context.parameterDataPtr;
        
        while (bbr.Position < bbr.Length) {
          long dataPos = bbr.Position;
          OpCode command = bbr.Read<OpCode>();
  
          // Debug.Log(command);
  
          if (command is OpCode.eof or OpCode.debug) {
            break;
          } else if (command == OpCode.jnorm) {
            ushort skipBytes = bbr.Read<ushort>();
  
            float3 normal = new(bbr.ReadFixed1616(), -bbr.ReadFixed1616(), bbr.ReadFixed1616());
            float3 point = new(bbr.ReadFixed1616(), -bbr.ReadFixed1616(), bbr.ReadFixed1616());
  
            var viewVec = point - eyePositionLocal;
  
            // Debug.Log($"n {normal} p {point} view {viewVec} dot {math.dot(viewVec, normal)}");
  
            // is normal pointin towards camera?
            if (math.dot(viewVec, normal) >= 0f) // Not facing.
              bbr.Position = dataPos + skipBytes;
          } else if (command == OpCode.lnres) {
            ushort vertexA = bbr.Read<ushort>();
            ushort vertexB = bbr.Read<ushort>();
  
            // draw line a -> b
          } else if (command == OpCode.multires) {
            ushort count = bbr.Read<ushort>();
            ushort vertexStart = bbr.Read<ushort>();
  
            for (ushort i = 0; i < count; ++i) {
              var vertex = vertexBuffer[vertexStart + i];
              vertex.position = new(bbr.ReadFixed1616(), -bbr.ReadFixed1616(), bbr.ReadFixed1616());
              vertex.flags = 0;
              vertexBuffer[vertexStart + i] = vertex;
            }
  
          } else if (command == OpCode.polyres) {
            ushort count = bbr.Read<ushort>();
            ushort vertexCount = count;
  
            var vertexIndices = new NativeArray<ushort>(vertexCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            // var colorUV = new half2(new((drawState.color & 0xFF) / 255f), new((drawState.color >> 8) / 255f));
            var colorUV = new half2(new((drawState.color & 0xFF) / 255f), half.zero);
  
            if (drawState.gouraud != Gouraud.normal)
              Debug.LogWarning($"Non implemented drawState.gouraud {drawState.gouraud}");
  
            while (count-- > 0)
              vertexIndices[count] = bbr.Read<ushort>();
  
            var origin = vertexBuffer[vertexIndices[0]].position;
            var normal = math.cross(vertexBuffer[vertexIndices[1]].position - origin, vertexBuffer[vertexIndices[2]].position - origin);
            var viewVec = origin - eyePositionLocal;
  
            if (drawState.check == false && math.dot(viewVec, normal) >= 0f) { // TODO check if math.dot this is really needed.
              int vertexStart = subMeshVertices.Length;
  
              for (int i = 0; i < vertexCount; ++i) {
                var vertexState = vertexBuffer[vertexIndices[i]];
  
                // TODO can we use 0 instead of MaxValue?
  
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
          } else if (command == OpCode.setcolor) {
            drawState.color = (byte)bbr.Read<ushort>();
            drawState.gouraud = Gouraud.normal;
          } else if (command == OpCode.sortnorm) {
            float3 normal = new(bbr.ReadFixed1616(), -bbr.ReadFixed1616(), bbr.ReadFixed1616());
            float3 point = new(bbr.ReadFixed1616(), -bbr.ReadFixed1616(), bbr.ReadFixed1616());
  
            long firstOpcodePosition = dataPos + bbr.Read<ushort>();
            long secondOpcodePosition = dataPos + bbr.Read<ushort>();
  
            long continuePosition = bbr.Position;
  
            var viewVec = point - eyePositionLocal;
  
            if (math.dot(viewVec, normal) < 0f) { // is normal pointin towards camera?
              bbr.Position = firstOpcodePosition;
              InterpreterLoop(ref bbr, ref context, objectLocalToWorld);
              bbr.Position = secondOpcodePosition;
              InterpreterLoop(ref bbr, ref context, objectLocalToWorld);
            } else {
              bbr.Position = secondOpcodePosition;
              InterpreterLoop(ref bbr, ref context, objectLocalToWorld);
              bbr.Position = firstOpcodePosition;
              InterpreterLoop(ref bbr, ref context, objectLocalToWorld);
            }
  
            bbr.Position = continuePosition;
          } else if (command == OpCode.setshade) {
            ushort count = bbr.Read<ushort>();
            while (count-- > 0) {
              var vertexIndex = bbr.Read<ushort>();
              var vertex = vertexBuffer[vertexIndex];
              vertex.i = bbr.Read<ushort>();
              vertex.flags |= VertexFlag.I;
              vertexBuffer[vertexIndex] = vertex;
            }
          } else if (command == OpCode.goursurf) {
            drawState.gouraudColorBase = (ushort)(bbr.Read<ushort>() << 8);
            drawState.gouraud = Gouraud.spoly;
          } else if (command == OpCode.x_rel) {
            ushort vertexIndex = bbr.Read<ushort>();
            ushort referenceVertex = bbr.Read<ushort>();
  
            var vertex = vertexBuffer[referenceVertex];
            vertex.position.x += bbr.ReadFixed1616();
            vertex.flags = 0;
            vertexBuffer[vertexIndex] = vertex;
          } else if (command == OpCode.y_rel) {
            ushort vertexIndex = bbr.Read<ushort>();
            ushort referenceVertex = bbr.Read<ushort>();
  
            var vertex = vertexBuffer[referenceVertex];
            vertex.position.y += -bbr.ReadFixed1616();
            vertex.flags = 0;
            vertexBuffer[vertexIndex] = vertex;
          } else if (command == OpCode.z_rel) {
            ushort vertexIndex = bbr.Read<ushort>();
            ushort referenceVertex = bbr.Read<ushort>();
  
            var vertex = vertexBuffer[referenceVertex];
            vertex.position.z += bbr.ReadFixed1616();
            vertex.flags = 0;
            vertexBuffer[vertexIndex] = vertex;
          } else if (command == OpCode.xy_rel) {
            ushort vertexIndex = bbr.Read<ushort>();
            ushort referenceVertex = bbr.Read<ushort>();
  
            var vertex = vertexBuffer[referenceVertex];
            vertex.position.x += bbr.ReadFixed1616();
            vertex.position.y += -bbr.ReadFixed1616();
            vertex.flags = 0;
            vertexBuffer[vertexIndex] = vertex;
          } else if (command == OpCode.xz_rel) {
            ushort vertexIndex = bbr.Read<ushort>();
            ushort referenceVertex = bbr.Read<ushort>();
  
            var vertex = vertexBuffer[referenceVertex];
            vertex.position.x += bbr.ReadFixed1616();
            vertex.position.z += bbr.ReadFixed1616();
            vertex.flags = 0;
            vertexBuffer[vertexIndex] = vertex;
          } else if (command == OpCode.yz_rel) {
            ushort vertexIndex = bbr.Read<ushort>();
            ushort referenceVertex = bbr.Read<ushort>();
  
            var vertex = vertexBuffer[referenceVertex];
            vertex.position.y += -bbr.ReadFixed1616();
            vertex.position.z += bbr.ReadFixed1616();
            vertex.flags = 0;
            vertexBuffer[vertexIndex] = vertex;
          } else if (command == OpCode.icall_p) {
            long nextOpcode = dataPos + bbr.Read<uint>();
  
            float3 subObjectPosition = new(bbr.ReadFixed1616(), -bbr.ReadFixed1616(), bbr.ReadFixed1616());
            var angle = (*(ushort*)(parameterDataPtr + bbr.Read<ushort>()) * 2f * math.PI) / 255f;
            var subobjectLocalToWorld = math.mul(objectLocalToWorld, math.mul(float4x4.RotateX(angle), float4x4.Translate(subObjectPosition)));
  
            var continuePosition = bbr.Position;
            bbr.Position = nextOpcode;
            InterpreterLoop(ref bbr, ref context, subobjectLocalToWorld);
  
            bbr.Position = continuePosition;
          } else if (command == OpCode.icall_b) {
            long nextOpcode = dataPos + bbr.Read<uint>();
  
            float3 subObjectPosition = new(bbr.ReadFixed1616(), -bbr.ReadFixed1616(), bbr.ReadFixed1616());
            var angle = (*(ushort*)(parameterDataPtr + bbr.Read<ushort>()) * 2f * math.PI) / 255f;
            var subobjectLocalToWorld = math.mul(objectLocalToWorld, math.mul(float4x4.RotateZ(angle), float4x4.Translate(subObjectPosition)));
  
            var continuePosition = bbr.Position;
            bbr.Position = nextOpcode;
            InterpreterLoop(ref bbr, ref context, subobjectLocalToWorld);
  
            bbr.Position = continuePosition;
          } else if (command == OpCode.icall_h) {
            long nextOpcode = dataPos + bbr.Read<uint>();
  
            float3 subObjectPosition = new(bbr.ReadFixed1616(), -bbr.ReadFixed1616(), bbr.ReadFixed1616());
            var angle = (*(ushort*)(parameterDataPtr + bbr.Read<ushort>()) * 2f * math.PI) / 255f;
            var subobjectLocalToWorld = math.mul(objectLocalToWorld, math.mul(float4x4.RotateY(angle), float4x4.Translate(subObjectPosition)));
  
            var continuePosition = bbr.Position;
            bbr.Position = nextOpcode;
            InterpreterLoop(ref bbr, ref context, subobjectLocalToWorld);
  
            bbr.Position = continuePosition;
          } else if (command == OpCode.sfcal) {
            long nextOpcode = dataPos + bbr.Read<ushort>();
            
            var continuePosition = bbr.Position;
            bbr.Position = nextOpcode;
            InterpreterLoop(ref bbr, ref context, objectLocalToWorld);
            
            bbr.Position = continuePosition;
          } else if (command == OpCode.defres) {
            ushort vertexIndex = bbr.Read<ushort>();
            var vertex = vertexBuffer[vertexIndex];
            vertex.position = new(bbr.ReadFixed1616(), -bbr.ReadFixed1616(), bbr.ReadFixed1616());
            vertex.flags = 0;
            vertexBuffer[vertexIndex] = vertex;
          } else if (command == OpCode.defres_i) {
            ushort vertexIndex = bbr.Read<ushort>();
            VertexState vertex = default;
            vertex.position = new(bbr.ReadFixed1616(), -bbr.ReadFixed1616(), bbr.ReadFixed1616());
            vertex.flags = 0;
            vertex.i = bbr.Read<ushort>();
            vertex.flags |= VertexFlag.I;
            vertexBuffer[vertexIndex] = vertex;
          } else if (command == OpCode.getparms) {
            var dest = (int*)(parameterDataPtr + bbr.Read<ushort>());
            ushort src = bbr.Read<ushort>();
            ushort count = bbr.Read<ushort>();
            // In SS object rendering is called with variable amount of params. This copies them to array
            while (count-- > 0)
              *(dest++) = customParams[src++];
          } else if (command == OpCode.getparms_i) {
            var dest = *(int**)(parameterDataPtr + bbr.Read<ushort>()); // Notice, pointer of pointer.
            ushort src = bbr.Read<ushort>();
            ushort count = bbr.Read<ushort>();
            // In SS object rendering is called with variable amount of params. This copies them to array
            while (count-- > 0)
              *(dest++) = customParams[src++];
          } else if (command == OpCode.gour_p) {
            drawState.gouraudColorBase = (ushort)(*(parameterDataPtr + bbr.Read<ushort>()) << 8);
            drawState.gouraud = Gouraud.spoly;
          } else if (command == OpCode.gour_vc) {
            drawState.gouraudColorBase = (ushort)(vertexColor[bbr.Read<ushort>()] << 8);
            drawState.gouraud = Gouraud.spoly;
          } else if (command == OpCode.getvcolor) {
            ushort colorIndex = bbr.Read<ushort>();
            drawState.color = vertexColor[colorIndex];
            drawState.gouraud = Gouraud.normal;
          } else if (command == OpCode.getvscolor) {
            ushort colorIndex = bbr.Read<ushort>();
            ushort shade = bbr.Read<ushort>();
            drawState.color = ShadeTable[(shade << 8) | vertexColor[colorIndex]];
          } else if (command == OpCode.rgbshades) {
            ushort count = bbr.Read<ushort>();
            while (count-- > 0) {
              var vertexIndex = bbr.Read<ushort>();
              
              var vertex = vertexBuffer[vertexIndex];
              vertex.rgb = bbr.Read<uint>();
              vertex.flags |= VertexFlag.RGB;
              vertexBuffer[vertexIndex] = vertex;

              bbr.Position += 4;
            }
          } else if (command == OpCode.draw_mode) {
            ushort flags = bbr.Read<ushort>();
            drawState.wire = ((flags >> 8) & 1) == 1;
            flags &= 0x00FF;
            flags <<= 1;
            drawState.check = ((flags >> 8) & 1) == 1;
            flags &= 0x00FF;
            flags <<= 2;
            drawState.gouraud = (Gouraud)(flags - 1);
          } else if (command == OpCode.getpcolor) {
            drawState.color = *(parameterDataPtr + bbr.Read<ushort>());
            drawState.gouraud = Gouraud.normal;
          } else if (command == OpCode.getpscolor) {
            ushort colorIndex = *(parameterDataPtr + bbr.Read<ushort>());
            ushort shade = bbr.Read<ushort>();
            drawState.color = ShadeTable[(shade << 8) | (colorIndex & 0xFF)];
          } else if (command == OpCode.scaleres) {
            break;
          } else if (command == OpCode.vpnt_p) {
            ushort paramByteOffset = bbr.Read<ushort>();
            ushort vertexIndex = bbr.Read<ushort>();
  
            var p = *(g3s_point*)(* (long *) (parameterDataPtr + paramByteOffset));
  
            vertexBuffer[vertexIndex] = new VertexState {
              position = new(p.x / 65536f, p.y / 65536f, p.z / 65536f),
              uv = new(p.u / 65536f, 1f - p.v / 65536f),
              flags = (VertexFlag)p.p3_flags,
              i = (ushort)p.i,
              rgb = p.u, // if gouroud
            };
          } else if (command == OpCode.vpnt_v) {
            ushort vpointIndex = bbr.Read<ushort>();
            ushort vertexIndex = bbr.Read<ushort>();
            // vertexBuffer[vertexIndex] = _vpoint_tab[vpointIndex>>2];
          } else if (command == OpCode.setuv) {
            var vertexIndex = bbr.Read<ushort>();
            var vertex = vertexBuffer[vertexIndex];
            vertex.uv = new(bbr.ReadFixed1616(), 1f - bbr.ReadFixed1616());
            vertex.flags |= VertexFlag.U | VertexFlag.V;
            vertexBuffer[vertexIndex] = vertex;
          } else if (command == OpCode.uvlist) {
            ushort count = bbr.Read<ushort>();
  
            while (count-- > 0) {
              var vertexIndex = bbr.Read<ushort>();
              var vertex = vertexBuffer[vertexIndex];
              vertex.uv = new(bbr.ReadFixed1616(), 1f - bbr.ReadFixed1616());
              vertex.flags |= VertexFlag.U | VertexFlag.V;
              vertexBuffer[vertexIndex] = vertex;
            }
          } else if (command == OpCode.tmap) {
            ushort textureId = bbr.Read<ushort>();
            ushort vertexCount = bbr.Read<ushort>();
  
            int vertexStart = subMeshVertices.Length;
  
            for (int i = 0; i < vertexCount; ++i) {
              var vertexState = vertexBuffer[bbr.Read<ushort>()];
              subMeshVertices.Add(new Vertex { pos = vertexState.position, uv = new half2(vertexState.uv) });
            }
  
            for (int i = 0; i < vertexCount - 2; ++i) {
              subMeshIndices.Add(textureId, (ushort)(vertexStart + i + 2));
              subMeshIndices.Add(textureId, (ushort)(vertexStart + i + 1));
              subMeshIndices.Add(textureId, (ushort)vertexStart);
            }
          } else if (command == OpCode.dbg) {
            ushort skip = bbr.Read<ushort>();
            ushort code = bbr.Read<ushort>();
            ushort polygonId = bbr.Read<ushort>();
          }
        }
      }
    }

    public struct ModelPart : IComponentData { }

    private struct HashChangedTag : IComponentData, IEnableableComponent { }
    
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
