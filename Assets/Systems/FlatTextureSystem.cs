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
using static SS.System.ProjectedTextureSystem;
using static SS.TextureUtils;
using static Unity.Mathematics.math;
using Vertex = SS.Data.Vertex;

namespace SS.System {
  [CreateAfter(typeof(MaterialProviderSystem))]
  [UpdateInGroup(typeof(VariableRateSimulationSystemGroup))]
  public partial class FlatTextureSystem : SystemBase {
    private NativeParallelHashMap<(BatchMaterialID, ushort), MaterialMeshInfo> resourceMaterialMeshInfos;

    private EntityQuery newFlatTextureQuery;
    private EntityQuery animatedFlatTextureQuery;
    private EntityQuery removedFlatTextureQuery;

    private EntityArchetype viewPartArchetype;

    private RenderMeshDescription renderMeshDescription;

    private ComponentLookup<ObjectInstance> instanceLookup;
    private ComponentLookup<ObjectInstance.Decoration> decorationLookup;
    private BufferLookup<Child> childLookup;

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
        None = new ComponentType[] { ComponentType.ReadOnly<FlatTextureMeshAddedTag>(), ComponentType.ReadOnly<DecalProjectorAddedTag>() },
      });

      animatedFlatTextureQuery = GetEntityQuery(new EntityQueryDesc {
        All = new ComponentType[] {
          ComponentType.ReadOnly<ObjectInstance>(),

          ComponentType.ReadOnly<FlatTextureInfo>(),
          ComponentType.ReadOnly<FlatTextureMeshAddedTag>(),
          ComponentType.ReadOnly<AnimatedTag>(),
        }
      });

      removedFlatTextureQuery = GetEntityQuery(new EntityQueryDesc {
        All = new ComponentType[] { ComponentType.ReadOnly<FlatTextureMeshAddedTag>() },
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
      this.childLookup = GetBufferLookup<Child>(true);

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
      this.childLookup.Update(this);

      var ecbSystem = World.GetExistingSystemManaged<EndVariableRateSimulationEntityCommandBufferSystem>();
      var commandBuffer = ecbSystem.CreateCommandBuffer();

      var level = SystemAPI.GetSingleton<Level>();

      { // Update animated mesh
        var animatedEntities = animatedFlatTextureQuery.ToEntityArray(Allocator.TempJob);
        var entityMeshInfo = new NativeArray<MaterialMeshInfo>(animatedEntities.Length, Allocator.TempJob);

        ProcessEntities(level, animatedEntities, entityMeshInfo);

        for (var index = 0; index < animatedEntities.Length; ++index) {
          var entity = animatedEntities[index];
          var instanceData = instanceLookup.GetRefRO(entity).ValueRO;

          if (instanceData.Class != ObjectClass.DoorAndGrating) continue; // Only ObjectClass.DoorAndGrating are handled here.

          if (childLookup.TryGetBuffer(entity, out DynamicBuffer<Child> children)) {
            var viewPart = children[0].Value;
            commandBuffer.SetComponent(viewPart, entityMeshInfo[index]);
            commandBuffer.RemoveComponent<AnimatedTag>(entity);
          }
        }
      }

      {
        var newEntities = newFlatTextureQuery.ToEntityArray(Allocator.TempJob);
        var entityMeshInfos = new NativeArray<MaterialMeshInfo>(newEntities.Length, Allocator.TempJob);

        ProcessEntities(level, newEntities, entityMeshInfos);

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
          meshInfos = entityMeshInfos
        };

        Dependency = createSpriteEntitiesJob.Schedule(newEntities.Length, 64, Dependency);

        var finalizeCommandBuffer = ecbSystem.CreateCommandBuffer();
        finalizeCommandBuffer.DestroyEntity(prototype);
      }

      // Dependency.Complete();
    }

    private void ProcessEntities(Level level, NativeArray<Entity> newEntities, NativeArray<MaterialMeshInfo> entityMeshInfos) {
      for (int entityIndex = 0; entityIndex < newEntities.Length; ++entityIndex) {
        var entity = newEntities[entityIndex];
        var instanceData = instanceLookup.GetRefRO(entity).ValueRO;

        if (instanceData.Class != ObjectClass.DoorAndGrating) continue; // Non doublesided are handled in ProjectedTextureSystem

        var materialID = TextureUtils.GetResource(
          entity,
          instanceData,
          level,
          objectProperties.ObjectDatasBlobAsset,
          materialProviderSystem,
          instanceLookup,
          decorationLookup,
          false,
          out ushort refWidthOverride);

        if (materialID == BatchMaterialID.Null) {
          var currentFrame = instanceData.Info.CurrentFrame != -1 ? instanceData.Info.CurrentFrame : 0;
          var spriteIndex = spriteSystem.GetSpriteIndex(instanceData, currentFrame);
          materialID = materialProviderSystem.GetMaterial($"{ArtResourceIdBase}:{spriteIndex}", true, false);
        }

        if (resourceMaterialMeshInfos.TryGetValue((materialID, refWidthOverride), out var materialMeshInfo)) {
          entityMeshInfos[entityIndex] = materialMeshInfo;
          continue; // Sprite already built. Skip loading.
        }

        var mesh = new Mesh();

        // TODO ref count and remove & unregister unneeded resources?

        materialMeshInfo = new MaterialMeshInfo {
          MaterialID = materialID,
          MeshID = this.entitiesGraphicsSystem.RegisterMesh(mesh),
          Submesh = 0
        };

        entityMeshInfos[entityIndex] = materialMeshInfo;

        if (resourceMaterialMeshInfos.TryAdd((materialID, refWidthOverride), materialMeshInfo)) {
          var loadOp = materialProviderSystem.GetBitmapDesc(materialID);
          loadOp.Completed += loadOp => {
            if (loadOp.Status != AsyncOperationStatus.Succeeded)
              throw loadOp.OperationException;

            var bitmapDesc = loadOp.Result;

            float scale = 1f;
            if (refWidthOverride > 0)
              scale = refWidthOverride / bitmapDesc.Size.x;

            BuildPlaneMesh(mesh, scale * float2(bitmapDesc.Size.x, bitmapDesc.Size.y) / 128f, true); // double sided, instanceData.Class == ObjectClass.DoorAndGrating
          };
        }
      }
    }

    protected override void OnDestroy() {
      base.OnDestroy();

      resourceMaterialMeshInfos.Dispose();
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
    [ReadOnly, DeallocateOnJobCompletion] public NativeArray<MaterialMeshInfo> meshInfos;

    public void Execute(int index) {
      var entity = entities[index];
      var meshInfo = meshInfos[index];

      if (meshInfo.MeshID == BatchMeshID.Null) return;

      var viewPart = commandBuffer.Instantiate(index, prototype);

      commandBuffer.SetComponent(index, viewPart, new Parent { Value = entity });
      commandBuffer.SetComponent(index, viewPart, LocalTransform.Identity);
      commandBuffer.SetComponent(index, viewPart, new RenderBounds { Value = new AABB { Center = float3(0f), Extents = float3(0.5f) } });
      commandBuffer.SetComponent(index, viewPart, meshInfo);

      commandBuffer.AddComponent<FlatTextureMeshAddedTag>(index, entity);
    }
  }

  public struct FlatTextureInfo : IComponentData { }

  public struct FlatTexturePart : IComponentData { }

  internal struct FlatTextureMeshAddedTag : ICleanupComponentData { }
}
