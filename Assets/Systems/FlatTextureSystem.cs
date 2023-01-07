using SS.Data;
using SS.ObjectProperties;
using SS.Resources;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.ResourceManagement.AsyncOperations;
using static SS.TextureUtils;
using static Unity.Mathematics.math;

namespace SS.System {
  [CreateAfter(typeof(MaterialProviderSystem))]
  [UpdateInGroup(typeof(VariableRateSimulationSystemGroup))]
  public partial class FlatTextureSystem : SystemBase {
    private NativeParallelHashMap<(BatchMaterialID, ushort), MaterialMeshInfo> resourceMaterialMeshInfos;

    private EntityQuery newFlatTextureQuery;
    private EntityQuery activeFlatTextureQuery;
    private EntityQuery removedFlatTextureQuery;

    private EntityArchetype viewPartArchetype;

    private RenderMeshDescription renderMeshDescription;

    private ComponentLookup<ObjectInstance> instanceLookup;
    private ComponentLookup<ObjectInstance.Decoration> decorationLookup;

    private EntitiesGraphicsSystem entitiesGraphicsSystem;
    private MaterialProviderSystem materialProviderSystem;
    private SpriteSystem spriteSystem;

    private Resources.ObjectProperties objectProperties;

    protected override void OnCreate() {
      base.OnCreate();

      RequireForUpdate<Level>();
      RequireForUpdate<AsyncLoadTag>();

      resourceMaterialMeshInfos = new(512, Allocator.Persistent);

      newFlatTextureQuery = GetEntityQuery(new EntityQueryDesc {
        All = new ComponentType[] { ComponentType.ReadOnly<FlatTextureInfo>(), ComponentType.ReadOnly<ObjectInstance>() },
        None = new ComponentType[] { ComponentType.ReadOnly<FlatTextureAddedTag>() },
      });

      activeFlatTextureQuery = GetEntityQuery(new EntityQueryDesc {
        All = new ComponentType[] {
          ComponentType.ReadOnly<ObjectInstance>(),

          ComponentType.ReadOnly<FlatTextureInfo>(),
          ComponentType.ReadOnly<FlatTextureAddedTag>(),
          ComponentType.ReadOnly<AnimatedTag>(),
        }
      });

      removedFlatTextureQuery = GetEntityQuery(new EntityQueryDesc {
        All = new ComponentType[] { ComponentType.ReadOnly<FlatTextureAddedTag>() },
        None = new ComponentType[] { ComponentType.ReadOnly<FlatTextureInfo>() },
      });

      viewPartArchetype = World.EntityManager.CreateArchetype(
        typeof(FlatTexturePart),

        typeof(LocalTransform),
        typeof(WorldTransform),

        typeof(Parent),
        typeof(ParentTransform),

        typeof(LocalToWorld),
        typeof(RenderBounds)
      );

      this.renderMeshDescription = new RenderMeshDescription(
        shadowCastingMode: ShadowCastingMode.Off,
        receiveShadows: false,
        staticShadowCaster: false
      );

      this.instanceLookup = GetComponentLookup<ObjectInstance>(true);
      this.decorationLookup = GetComponentLookup<ObjectInstance.Decoration>(true);

      this.entitiesGraphicsSystem = World.GetOrCreateSystemManaged<EntitiesGraphicsSystem>();
      this.materialProviderSystem = World.GetOrCreateSystemManaged<MaterialProviderSystem>();
      this.spriteSystem = World.GetOrCreateSystemManaged<SpriteSystem>();

      var objectPropertiesOp = Services.ObjectProperties;
      objectPropertiesOp.Completed += op => {
        if (op.Status != AsyncOperationStatus.Succeeded)
          throw op.OperationException;

        objectProperties = objectPropertiesOp.Result;

        EntityManager.AddComponent<AsyncLoadTag>(this.SystemHandle);
      };
    }

    protected override void OnUpdate() {
      this.instanceLookup.Update(this);
      this.decorationLookup.Update(this);

      var ecbSystem = World.GetExistingSystemManaged<EndVariableRateSimulationEntityCommandBufferSystem>();
      var commandBuffer = ecbSystem.CreateCommandBuffer();

      var level = SystemAPI.GetSingleton<Level>();

      {
        var animatedEntities = activeFlatTextureQuery.ToEntityArray(Allocator.TempJob);
        using var instanceDatas = activeFlatTextureQuery.ToComponentDataArray<ObjectInstance>(Allocator.TempJob);
        var entityMeshInfo = new NativeArray<MaterialMeshInfo>(animatedEntities.Length, Allocator.TempJob);

        ProcessEntities(level, animatedEntities, instanceDatas, entityMeshInfo);

        // GetBufferLookup<Child>(true);

        for (var index = 0; index < animatedEntities.Length; ++index) {
          var entity = animatedEntities[index];
          var instanceData = instanceDatas[index];

          if (EntityManager.HasBuffer<Child>(entity)) {
            DynamicBuffer<Child> children = EntityManager.GetBuffer<Child>(entity, true);

            var viewPart = children[0].Value;

            commandBuffer.SetComponent(viewPart, LocalTransform.Identity);
            commandBuffer.SetComponent(viewPart, new RenderBounds { Value = new AABB { Center = float3(0f), Extents = float3(0.5f) } });
            commandBuffer.SetComponent(viewPart, entityMeshInfo[index]);
          }
        }
      }

      {
        var newEntities = newFlatTextureQuery.ToEntityArray(Allocator.TempJob);
        using var instanceDatas = newFlatTextureQuery.ToComponentDataArray<ObjectInstance>(Allocator.TempJob);
        var entityMeshInfo = new NativeArray<MaterialMeshInfo>(newEntities.Length, Allocator.TempJob);

        ProcessEntities(level, newEntities, instanceDatas, entityMeshInfo);

        var prototype = EntityManager.CreateEntity(viewPartArchetype); // Sync point
        RenderMeshUtility.AddComponents(
          prototype,
          EntityManager,
          renderMeshDescription,
          new RenderMeshArray(new Material[0], new Mesh[0])
        );

        var createSpriteEntitiesJob = new CreateSpriteEntitiesJob() {
          commandBuffer = commandBuffer.AsParallelWriter(),

          prototype = prototype,

          entities = newEntities,
          meshInfo = entityMeshInfo
        };

        Dependency = createSpriteEntitiesJob.Schedule(newEntities.Length, 64, Dependency);

        var finalizeCommandBuffer = ecbSystem.CreateCommandBuffer();
        finalizeCommandBuffer.DestroyEntity(prototype);
      }

      // Dependency.Complete();
    }

    private void ProcessEntities(Level level, NativeArray<Entity> newEntities, NativeArray<ObjectInstance> instanceDatas, NativeArray<MaterialMeshInfo> entityMeshInfo) {
      for (int entityIndex = 0; entityIndex < newEntities.Length; ++entityIndex) {
        var entity = newEntities[entityIndex];
        var instanceData = instanceDatas[entityIndex];

        var materialID = GetResource(entity, instanceData, level, out ushort refWidthOverride);

        if (materialID == BatchMaterialID.Null) {
          var currentFrame = instanceData.Info.CurrentFrame != -1 ? instanceData.Info.CurrentFrame : 0;
          var spriteIndex = spriteSystem.GetSpriteIndex(instanceData, currentFrame);
          materialID = materialProviderSystem.GetMaterial($"{ArtResourceIdBase}:{spriteIndex}", true);
        }

        if (resourceMaterialMeshInfos.TryGetValue((materialID, refWidthOverride), out var materialMeshInfo)) {
          entityMeshInfo[entityIndex] = materialMeshInfo;
          continue; // Sprite already built. Skip loading.
        }

        var mesh = new Mesh();

        // TODO ref count and remove & unregister unneeded resources?

        materialMeshInfo = new MaterialMeshInfo {
          MaterialID = materialID,
          MeshID = this.entitiesGraphicsSystem.RegisterMesh(mesh),
          Submesh = 0
        };

        entityMeshInfo[entityIndex] = materialMeshInfo;

        var isDoubleSided = instanceData.Class == ObjectClass.DoorAndGrating;
        if (resourceMaterialMeshInfos.TryAdd((materialID, refWidthOverride), materialMeshInfo)) {
          var loadOp = materialProviderSystem.GetBitmapDesc(materialID);
          loadOp.Completed += loadOp => {
            if (loadOp.Status != AsyncOperationStatus.Succeeded)
              throw loadOp.OperationException;

            var bitmapDesc = loadOp.Result;

            float scale = 1f;
            if (refWidthOverride > 0)
              scale = refWidthOverride / bitmapDesc.Size.x;

            BuildPlaneMesh(mesh, scale * float2(bitmapDesc.Size.x, bitmapDesc.Size.y) / 128f, isDoubleSided);
          };
        }
      }
    }

    protected override void OnDestroy() {
      base.OnDestroy();

      resourceMaterialMeshInfos.Dispose();
    }

    private BatchMaterialID GetResource(in Entity entity, in ObjectInstance instanceData, in Level level, out ushort refWidthOverride) {
      var baseProperties = objectProperties.BasePropertyData(instanceData);

      refWidthOverride = 0;

      if (baseProperties.DrawType == DrawType.TerrainPolygon) {
        const int DESTROYED_SCREEN_ANIM_BASE = 0x1B;

        if (instanceData.Class == ObjectClass.Decoration) {
          this.decorationLookup.Update(this); // TODO FIXME hack

          var decorationData = this.decorationLookup.GetRefRO(entity).ValueRO;
          var textureData = CalculateTextureData(baseProperties, instanceData, decorationData, level, instanceLookup, decorationLookup);

          if (instanceData.Triple == 0x70207) { // TMAP_TRIPLE
            refWidthOverride = 128;

            unsafe {
              return materialProviderSystem.GetMaterial($"{0x03E8 + level.TextureMap.blockIndex[textureData]}", true);
            }
          } else if (instanceData.Triple == 0x70208) { // SUPERSCREEN_TRIPLE
            var lightmapped = decorationData.Data2 == DESTROYED_SCREEN_ANIM_BASE + 3; // screen is full bright if not destroyed
            refWidthOverride = 128; // 1 << 7
            return materialProviderSystem.ParseTextureData(textureData, lightmapped, out var textureType, out var scale);
          } else if (instanceData.Triple == 0x70209) { // BIGSCREEN_TRIPLE
            var lightmapped = decorationData.Data2 == DESTROYED_SCREEN_ANIM_BASE + 3; // screen is full bright if not destroyed
            refWidthOverride = 64; // 1 << 6
            return materialProviderSystem.ParseTextureData(textureData, lightmapped, out var textureType, out var scale);
          } else if (instanceData.Triple == 0x70206) { // SCREEN_TRIPLE
            var lightmapped = decorationData.Data2 == DESTROYED_SCREEN_ANIM_BASE + 3; // screen is full bright if not destroyed
            refWidthOverride = 32; // 1 << 5
            return materialProviderSystem.ParseTextureData(textureData, lightmapped, out var textureType, out var scale);
          } else {
            var materialID = materialProviderSystem.ParseTextureData(textureData, true, out var textureType, out var scale);
            refWidthOverride = (ushort)(1 << scale);
            return materialID;
          }
        }
      } else if (baseProperties.DrawType == DrawType.FlatTexture) {
        if (instanceData.Class == ObjectClass.Decoration) {
          if (instanceData.Triple == 0x70203) { // WORDS_TRIPLE
            // TODO
            return BatchMaterialID.Null;
          } else if (instanceData.Triple == 0x70201) { // ICON_TRIPLE
            return materialProviderSystem.GetMaterial($"{IconResourceIdBase}:{instanceData.Info.CurrentFrame}", true);
          } else if (instanceData.Triple == 0x70202) { // GRAF_TRIPLE
            return materialProviderSystem.GetMaterial($"{GraffitiResourceIdBase}:{instanceData.Info.CurrentFrame}", true);
          } else if (instanceData.Triple == 0x7020a) { // REPULSWALL_TRIPLE
            return materialProviderSystem.GetMaterial($"{RepulsorResourceIdBase}:{instanceData.Info.CurrentFrame}", true);
          }
        } else if (instanceData.Class == ObjectClass.DoorAndGrating) {
          // Debug.Log($"{DoorResourceIdBase} {objectProperties.ClassPropertyIndex(instanceData)} : {instanceData.Info.CurrentFrame}");
          return materialProviderSystem.GetMaterial($"{DoorResourceIdBase + objectProperties.ClassPropertyIndex(instanceData)}:{instanceData.Info.CurrentFrame}", true);
        }
      }

      return BatchMaterialID.Null;
    }

    // TODO almost identical to one in SpriteSystem
    private void BuildPlaneMesh(Mesh mesh, float2 extent, bool doubleSided) {
      mesh.SetVertexBufferParams(4,
        new VertexAttributeDescriptor(VertexAttribute.Position),
        new VertexAttributeDescriptor(VertexAttribute.Normal),
        new VertexAttributeDescriptor(VertexAttribute.Tangent),
        new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float16, 2),
        new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 1)
      );

      mesh.SetVertexBufferData(new[] {
        new Vertex { pos = float3(-extent.x, extent.y, 0f), uv = half2(half(0f), half(1f)), light = 1f },
        new Vertex { pos = float3(-extent.x, -extent.y, 0f), uv = half2(half(0f), half(0f)), light = 0f },
        new Vertex { pos = float3(extent.x, -extent.y, 0f), uv = half2(half(1f), half(0f)), light = 0f },
        new Vertex { pos = float3(extent.x, extent.y, 0f), uv = half2(half(1f), half(1f)), light = 1f },
      }, 0, 0, 4);

      mesh.subMeshCount = 1;

      if (doubleSided) {
        mesh.SetIndexBufferParams(12, IndexFormat.UInt16);
        mesh.SetIndexBufferData(new ushort[] { 0, 1, 2, 2, 3, 0, 2, 1, 0, 0, 3, 2 }, 0, 0, 12);
        mesh.SetSubMesh(0, new SubMeshDescriptor(0, 12, MeshTopology.Triangles));
      } else {
        mesh.SetIndexBufferParams(6, IndexFormat.UInt16);
        mesh.SetIndexBufferData(new ushort[] { 2, 1, 0, 0, 3, 2 }, 0, 0, 6);
        mesh.SetSubMesh(0, new SubMeshDescriptor(0, 6, MeshTopology.Triangles));
      }


      mesh.RecalculateNormals();
      // mesh.RecalculateTangents();
      mesh.RecalculateBounds();
      mesh.UploadMeshData(true);
    }

    private struct AsyncLoadTag : IComponentData { }
  }

  [BurstCompile]
  struct CreateSpriteEntitiesJob : IJobParallelFor {
    public EntityCommandBuffer.ParallelWriter commandBuffer;

    [ReadOnly] public Entity prototype;

    [ReadOnly, DeallocateOnJobCompletion] public NativeArray<Entity> entities;
    [ReadOnly, DeallocateOnJobCompletion] public NativeArray<MaterialMeshInfo> meshInfo;

    public void Execute(int index) {
      var entity = entities[index];

      var viewPart = commandBuffer.Instantiate(index, prototype);

      commandBuffer.SetComponent(index, viewPart, new FlatTexturePart { CurrentFrame = 0 }); // TODO FIXME
      commandBuffer.SetComponent(index, viewPart, new Parent { Value = entity });
      commandBuffer.SetComponent(index, viewPart, LocalTransform.Identity);
      commandBuffer.SetComponent(index, viewPart, new RenderBounds { Value = new AABB { Center = float3(0f), Extents = float3(0.5f) } });
      commandBuffer.SetComponent(index, viewPart, meshInfo[index]);

      commandBuffer.AddComponent<FlatTextureAddedTag>(index, entity);
    }
  }

  public struct FlatTextureInfo : IComponentData { }

  public struct FlatTexturePart : IComponentData {
    public int CurrentFrame;
  }

  internal struct FlatTextureAddedTag : ICleanupComponentData { }
}
