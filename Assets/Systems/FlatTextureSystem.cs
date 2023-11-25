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
using static SS.System.ProjectedTextureSystem;
using static SS.TextureUtils;
using static Unity.Mathematics.math;
using static UnityEditor.MaterialProperty;
using Vertex = SS.Data.Vertex;

namespace SS.System {
  [CreateAfter(typeof(MaterialProviderSystem))]
  [UpdateInGroup(typeof(VariableRateSimulationSystemGroup))]
  public partial class FlatTextureSystem : SystemBase {
    private NativeParallelHashMap<(BatchMaterialID, ushort), MaterialMeshInfo> resourceMaterialMeshInfos;

    private EntityQuery newFlatTextureQuery;
    private EntityQuery animatedFlatTextureQuery;
    private EntityQuery removedFlatTextureQuery;
    private EntityQuery animatedQuery;

    private EntityArchetype viewPartArchetype;

    private RenderMeshDescription renderMeshDescription;

    private ComponentLookup<ObjectInstance> instanceLookup;
    private ComponentLookup<ObjectInstance.Decoration> decorationLookup;
    private ComponentLookup<ObjectInstance.DoorAndGrating> doorLookup;
    private BufferLookup<Child> childLookup;

    private EntitiesGraphicsSystem entitiesGraphicsSystem;
    private MaterialProviderSystem materialProviderSystem;
    private SpriteSystem spriteSystem;

    private Resources.ObjectProperties objectProperties;

    protected override async void OnCreate() {
      base.OnCreate();

      RequireForUpdate<Level>();
      RequireForUpdate<AsyncLoadTag>();

      resourceMaterialMeshInfos = new(512, Allocator.Persistent);

      newFlatTextureQuery = new EntityQueryBuilder(Allocator.Temp)
        .WithAll<FlatTextureInfo, ObjectInstance>()
        .WithNone<FlatTextureMeshAddedTag, DecalProjectorAddedTag>()
        .Build(this);

      animatedFlatTextureQuery = new EntityQueryBuilder(Allocator.Temp)
        .WithAll< FlatTextureMeshAddedTag, AnimatedTag, FlatTextureInfo, ObjectInstance>()
        .Build(this);

      removedFlatTextureQuery = new EntityQueryBuilder(Allocator.Temp)
        .WithAll<FlatTextureMeshAddedTag>()
        .WithNone<FlatTextureInfo>()
        .Build(this);

      animatedQuery = new EntityQueryBuilder(Allocator.Temp)
        .WithAll<AnimationData>()
        .Build(this);

      viewPartArchetype = World.EntityManager.CreateArchetype(
        typeof(FlatTexturePart),

        typeof(LocalTransform),
        typeof(Parent),

        typeof(LocalToWorld),
        typeof(RenderBounds)
      );

      renderMeshDescription = new RenderMeshDescription(
        shadowCastingMode: ShadowCastingMode.Off,
        receiveShadows: false,
        staticShadowCaster: false
      );

      instanceLookup = GetComponentLookup<ObjectInstance>(true);
      decorationLookup = GetComponentLookup<ObjectInstance.Decoration>(true);
      doorLookup = GetComponentLookup<ObjectInstance.DoorAndGrating>(true);
      childLookup = GetBufferLookup<Child>(true);

      entitiesGraphicsSystem = World.GetOrCreateSystemManaged<EntitiesGraphicsSystem>();
      materialProviderSystem = World.GetOrCreateSystemManaged<MaterialProviderSystem>();
      spriteSystem = World.GetOrCreateSystemManaged<SpriteSystem>();

      objectProperties = await Services.ObjectProperties;

      EntityManager.AddComponent<AsyncLoadTag>(SystemHandle);
    }

    protected override void OnDestroy() {
      base.OnDestroy();

      resourceMaterialMeshInfos.Dispose();
    }

    protected override void OnUpdate() {
      instanceLookup.Update(this);
      decorationLookup.Update(this);
      doorLookup.Update(this);
      childLookup.Update(this);

      var ecbSystem = World.GetExistingSystemManaged<EndVariableRateSimulationEntityCommandBufferSystem>();
      var commandBuffer = ecbSystem.CreateCommandBuffer();

      var level = SystemAPI.GetSingleton<Level>();

      using var animationData = animatedQuery.ToComponentDataArray<AnimationData>(Allocator.Temp);

      { // Update animated mesh
        var animatedEntities = animatedFlatTextureQuery.ToEntityArray(Allocator.TempJob);
        var entityMeshInfo = new NativeArray<MaterialMeshInfo>(animatedEntities.Length, Allocator.TempJob);

        ProcessEntities(level, animatedEntities, entityMeshInfo, animationData);

        for (var index = 0; index < animatedEntities.Length; ++index) {
          var entity = animatedEntities[index];
          var instanceData = instanceLookup.GetRefRO(entity).ValueRO;

          if (instanceData.Class != ObjectClass.DoorAndGrating) continue; // Only ObjectClass.DoorAndGrating are handled here.

          if (childLookup.TryGetBuffer(entity, out DynamicBuffer<Child> children)) {
            // TODO should use AddComponents to make sure bounds and other stuff is updated?

            var viewPart = children[0].Value;
            commandBuffer.SetComponent(viewPart, entityMeshInfo[index]);
            commandBuffer.RemoveComponent<AnimatedTag>(entity);
          }
        }
      }

      {
        var newEntities = newFlatTextureQuery.ToEntityArray(Allocator.TempJob);
        var entityMeshInfos = new NativeArray<MaterialMeshInfo>(newEntities.Length, Allocator.TempJob);

        ProcessEntities(level, newEntities, entityMeshInfos, animationData);

        for (var index = 0; index < newEntities.Length; ++index) {
          var entity = newEntities[index];
          var meshInfo = entityMeshInfos[index];

          if (meshInfo.MeshID == BatchMeshID.Null) continue;
          if (meshInfo.MaterialID == BatchMaterialID.Null) continue;

          var viewPart = EntityManager.CreateEntity(viewPartArchetype); // Sync point

          RenderMeshUtility.AddComponents(
            viewPart,
            EntityManager,
            renderMeshDescription,
            meshInfo
          );

          commandBuffer.SetComponent(viewPart, new Parent { Value = entity });
          commandBuffer.SetComponent(viewPart, LocalTransform.Identity);

          commandBuffer.AddComponent<FlatTextureMeshAddedTag>(entity);
        }
      }
    }

    private void ProcessEntities(Level level, NativeArray<Entity> entities, NativeArray<MaterialMeshInfo> entityMeshInfos, NativeArray<AnimationData> animationData) {
      for (int entityIndex = 0; entityIndex < entities.Length; ++entityIndex) {
        var entity = entities[entityIndex];
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
          doorLookup,
          animationData.AsReadOnly(),
          false,
          out ushort refWidthOverride);

        if (materialID == BatchMaterialID.Null) {
          var currentFrame = instanceData.Info.CurrentFrame != -1 ? instanceData.Info.CurrentFrame : 0;
          var spriteIndex = spriteSystem.GetSpriteIndex(instanceData, currentFrame);
          materialID = materialProviderSystem.GetMaterial(ArtResourceIdBase, spriteIndex, true, false);
        }

        if (resourceMaterialMeshInfos.TryGetValue((materialID, refWidthOverride), out var materialMeshInfo)) {
          entityMeshInfos[entityIndex] = materialMeshInfo;
          continue; // Sprite already built. Skip loading.
        }

        var mesh = new Mesh();

        // TODO ref count and remove & unregister unneeded resources?

        materialMeshInfo = new MaterialMeshInfo {
          MaterialID = materialID,
          MeshID = entitiesGraphicsSystem.RegisterMesh(mesh),
          SubMesh = 0
        };

        entityMeshInfos[entityIndex] = materialMeshInfo;

        if (resourceMaterialMeshInfos.TryAdd((materialID, refWidthOverride), materialMeshInfo))
          UpdatePlaneMeshAsync(materialID, refWidthOverride, mesh);
      }
    }

    private async void UpdatePlaneMeshAsync (BatchMaterialID materialID, ushort refWidthOverride, Mesh mesh) {
      var bitmapDesc = await materialProviderSystem.GetBitmapDesc(materialID);

      float scale = 1f;
      if (refWidthOverride > 0)
        scale = refWidthOverride / bitmapDesc.Size.x;

      BuildPlaneMesh(mesh, scale * float2(bitmapDesc.Size.x, bitmapDesc.Size.y) / 128f, true); // double sided, instanceData.Class == ObjectClass.DoorAndGrating
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
