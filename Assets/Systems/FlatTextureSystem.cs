using System.Collections.Generic;
using SS.ObjectProperties;
using SS.Resources;
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
using UnityEngine.AddressableAssets;
using UnityEngine.Rendering;
using UnityEngine.ResourceManagement.AsyncOperations;
using static Unity.Mathematics.math;
using static SS.TextureUtils;

namespace SS.System {
  [CreateAfter(typeof(MaterialProviderSystem))]
  [UpdateInGroup(typeof(VariableRateSimulationSystemGroup))]
  public partial class FlatTextureSystem : SystemBase {
    private NativeParallelHashMap<BatchMaterialID, MaterialMeshInfo> resourceMaterialMeshInfos;

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
        typeof(Parent),
        typeof(LocalToParentTransform),
        typeof(LocalToWorldTransform),
        typeof(RenderBounds)
      );

      this.renderMeshDescription = new RenderMeshDescription(
        shadowCastingMode: ShadowCastingMode.On,
        receiveShadows: true,
        staticShadowCaster: true
      );

      this.instanceLookup = GetComponentLookup<ObjectInstance>(true);
      this.decorationLookup = GetComponentLookup<ObjectInstance.Decoration>(true);

      this.entitiesGraphicsSystem = World.GetOrCreateSystemManaged<EntitiesGraphicsSystem>();
      this.materialProviderSystem = World.GetOrCreateSystemManaged<MaterialProviderSystem>();
      this.spriteSystem = World.GetOrCreateSystemManaged<SpriteSystem>();

      objectProperties = Services.ObjectProperties.WaitForCompletion();
    }

    protected override void OnUpdate() {
      this.instanceLookup.Update(this);
      this.decorationLookup.Update(this);

      var ecbSystem = World.GetExistingSystemManaged<EndVariableRateSimulationEntityCommandBufferSystem>();
      var commandBuffer = ecbSystem.CreateCommandBuffer();

      var level = GetSingleton<Level>();

      {
        var animatedEntities = activeFlatTextureQuery.ToEntityArray(Allocator.TempJob);
        using var instanceDatas = activeFlatTextureQuery.ToComponentDataArray<ObjectInstance>(Allocator.TempJob);
        var entityMeshInfo = new NativeArray<MaterialMeshInfo>(animatedEntities.Length, Allocator.TempJob);
        var entityRefWidth = new NativeArray<float>(animatedEntities.Length, Allocator.TempJob);

        processEntities(level, animatedEntities, instanceDatas, entityMeshInfo, entityRefWidth);

        // GetBufferLookup<Child>(true);

        for (var index = 0; index < animatedEntities.Length; ++index) {
          var entity = animatedEntities[index];
          var instanceData = instanceDatas[index];

          if (EntityManager.HasBuffer<Child>(entity)) {
            DynamicBuffer<Child> children = EntityManager.GetBuffer<Child>(entity, true);

            var viewPart = children[0].Value;

            commandBuffer.SetComponent(viewPart, new LocalToParentTransform { Value = UniformScaleTransform.FromScale(1f / entityRefWidth[index]) });
            commandBuffer.SetComponent(viewPart, new RenderBounds { Value = new AABB { Center = float3(0f), Extents = float3(0.5f * entityRefWidth[index]) } });
            commandBuffer.SetComponent(viewPart, entityMeshInfo[index]);
          }
        }
      }

      {
        var newEntities = newFlatTextureQuery.ToEntityArray(Allocator.TempJob);
        using var instanceDatas = newFlatTextureQuery.ToComponentDataArray<ObjectInstance>(Allocator.TempJob);
        var entityMeshInfo = new NativeArray<MaterialMeshInfo>(newEntities.Length, Allocator.TempJob);
        var entityRefWidth = new NativeArray<float>(newEntities.Length, Allocator.TempJob);

        processEntities(level, newEntities, instanceDatas, entityMeshInfo, entityRefWidth);

        var prototype = EntityManager.CreateEntity(viewPartArchetype); // Sync point
        EntityManager.SetComponentData(prototype, new LocalToWorldTransform { Value = UniformScaleTransform.Identity });
        RenderMeshUtility.AddComponents(
          prototype,
          EntityManager,
          renderMeshDescription,
          new RenderMeshArray(new Material[0], new Mesh[0])
        );
        EntityManager.RemoveComponent<RenderMeshArray>(prototype);

        var createSpriteEntitiesJob = new CreateSpriteEntitiesJob() {
          commandBuffer = commandBuffer.AsParallelWriter(),

          prototype = prototype,

          entities = newEntities,
          meshInfo = entityMeshInfo,
          refWidth = entityRefWidth
        };

        Dependency = createSpriteEntitiesJob.Schedule(newEntities.Length, 64, Dependency);

        var finalizeCommandBuffer = ecbSystem.CreateCommandBuffer();
        finalizeCommandBuffer.DestroyEntity(prototype);
      }
    }

    private void processEntities(Level level, NativeArray<Entity> newEntities, NativeArray<ObjectInstance> instanceDatas, NativeArray<MaterialMeshInfo> entityMeshInfo, NativeArray<float> entityRefWidth) {
      for (int entityIndex = 0; entityIndex < newEntities.Length; ++entityIndex) {
        var entity = newEntities[entityIndex];
        var instanceData = instanceDatas[entityIndex];

        float refWidth = 64f;
        var materialID = GetResource(entity, instanceData, level, ref refWidth);

        entityRefWidth[entityIndex] = refWidth;

        if (materialID == BatchMaterialID.Null) {
          var currentFrame = instanceData.Info.CurrentFrame != -1 ? instanceData.Info.CurrentFrame : 0;
          var spriteIndex = spriteSystem.GetSpriteIndex(instanceData, currentFrame);
          materialID = materialProviderSystem.GetMaterial($"{ArtResourceIdBase}:{spriteIndex}", true);
        }

        if (resourceMaterialMeshInfos.TryGetValue(materialID, out var materialMeshInfo)) {
          entityMeshInfo[entityIndex] = materialMeshInfo;
          continue; // Sprite already built. Skip loading.
        }

        Mesh mesh = new Mesh();

        // TODO ref count and remove & unregister unneeded resources?

        materialMeshInfo = new MaterialMeshInfo {
          MaterialID = materialID,
          MeshID = this.entitiesGraphicsSystem.RegisterMesh(mesh),
          Submesh = 0
        };

        entityMeshInfo[entityIndex] = materialMeshInfo;

        var isDoubleSided = instanceData.Class == ObjectClass.DoorAndGrating;
        if (resourceMaterialMeshInfos.TryAdd(materialID, materialMeshInfo)) {
          var loadOp = materialProviderSystem.GetBitmapDesc(materialID);
          loadOp.Completed += loadOp => {
            if (loadOp.Status != AsyncOperationStatus.Succeeded)
              throw loadOp.OperationException;

            var bitmapDesc = loadOp.Result;

            BuildPlaneMesh(mesh, float2(bitmapDesc.Size.x, bitmapDesc.Size.y) / 2f, isDoubleSided);
          };
        }
      }
    }

    protected override void OnDestroy() {
      base.OnDestroy();

      resourceMaterialMeshInfos.Dispose();
    }

    private BatchMaterialID GetResource(in Entity entity, in ObjectInstance instanceData, in Level level, ref float refWidth) {
      var baseProperties = objectProperties.BasePropertyData(instanceData);

      if (baseProperties.DrawType == DrawType.TerrainPolygon) {
        const int DESTROYED_SCREEN_ANIM_BASE = 0x1B;

        if (instanceData.Class == ObjectClass.Decoration) {
          this.decorationLookup.Update(this); // TODO FIXME hack

          var decorationData = this.decorationLookup.GetRefRO(entity).ValueRO;
          var textureData = CalculateTextureData(instanceData, decorationData, level, instanceLookup, decorationLookup);

          if (instanceData.Triple == 0x70207) { // TMAP_TRIPLE
            refWidth = 128f;

            unsafe {
              return materialProviderSystem.GetMaterial($"{0x03E8 + level.TextureMap.blockIndex[textureData]}", true);
            }
          } else if (instanceData.Triple == 0x70208) { // SUPERSCREEN_TRIPLE
            var lightmapped = decorationData.Data2 == DESTROYED_SCREEN_ANIM_BASE + 3; // screen is full bright if not destroyed
            refWidth = 64f;
            return materialProviderSystem.ParseTextureData(textureData, lightmapped, out var textureType, out var scale);
          } else if (instanceData.Triple == 0x70209) { // BIGSCREEN_TRIPLE
            var lightmapped = decorationData.Data2 == DESTROYED_SCREEN_ANIM_BASE + 3; // screen is full bright if not destroyed
            refWidth = 32f;
            return materialProviderSystem.ParseTextureData(textureData, lightmapped, out var textureType, out var scale);
          } else if (instanceData.Triple == 0x70206) { // SCREEN_TRIPLE
            var lightmapped = decorationData.Data2 == DESTROYED_SCREEN_ANIM_BASE + 3; // screen is full bright if not destroyed
            refWidth = 64f;
            return materialProviderSystem.ParseTextureData(textureData, lightmapped, out var textureType, out var scale);
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
    private void BuildPlaneMesh (Mesh mesh, float2 extent, bool doubleSided) {
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
  }

  public enum TextureType {
    Alt,
    Custom,
    Text,
    ScrollText
  }

  [BurstCompile]
  struct CreateSpriteEntitiesJob : IJobParallelFor {
    public EntityCommandBuffer.ParallelWriter commandBuffer;

    [ReadOnly] public Entity prototype;

    [ReadOnly, DeallocateOnJobCompletion] public NativeArray<Entity> entities;
    [ReadOnly, DeallocateOnJobCompletion] public NativeArray<MaterialMeshInfo> meshInfo;
    [ReadOnly, DeallocateOnJobCompletion] public NativeArray<float> refWidth;
    
    public void Execute(int index) {
      var entity = entities[index];

      var viewPart = commandBuffer.Instantiate(index, prototype);

      commandBuffer.SetComponent(index, viewPart, new FlatTexturePart { CurrentFrame = 0 }); // TODO FIXME
      commandBuffer.SetComponent(index, viewPart, new Parent { Value = entity });
      commandBuffer.SetComponent(index, viewPart, new LocalToParentTransform { Value = UniformScaleTransform.FromScale(1f / refWidth[index]) });
      commandBuffer.SetComponent(index, viewPart, new RenderBounds { Value = new AABB { Center = float3(0f), Extents = float3(0.5f * refWidth[index]) } });
      commandBuffer.SetComponent(index, viewPart, meshInfo[index]);

      commandBuffer.AddComponent<FlatTextureAddedTag>(index, entity);
    }
  }

  public struct FlatTextureInfo : IComponentData {}

  public struct FlatTexturePart : IComponentData {
    public int CurrentFrame;
  }

  internal struct FlatTextureAddedTag : ICleanupComponentData { }
}
