using SS.Resources;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static SS.TextureUtils;
using static Unity.Mathematics.math;

namespace SS.System {
  [CreateAfter(typeof(MaterialProviderSystem))]
  [UpdateInGroup(typeof(VariableRateSimulationSystemGroup))]
  public partial class ProjectedTextureSystem : SystemBase {
    private Dictionary<Entity, DecalProjector> entityDecalProjectors = new();

    private EntityQuery newFlatTextureQuery;
    private EntityQuery activeDecalProjectorQuery;
    private EntityQuery removedDecalProjectoreQuery;

    private EntityArchetype viewPartArchetype;

    private ComponentLookup<ObjectInstance> instanceLookup;
    private ComponentLookup<ObjectInstance.Decoration> decorationLookup;
    private ComponentLookup<ObjectInstance.DoorAndGrating> doorLookup;
    private ComponentLookup<ObjectInstance.Enemy> enemyLookup;

    private EntitiesGraphicsSystem entitiesGraphicsSystem;
    private MaterialProviderSystem materialProviderSystem;
    private SpriteSystem spriteSystem;

    protected override void OnCreate() {
      base.OnCreate();

      RequireForUpdate<Level>();

      newFlatTextureQuery = new EntityQueryBuilder(Allocator.Temp)
        .WithAll<FlatTextureInfo, ObjectInstance>()
        .WithNone<FlatTextureMeshAddedTag, DecalProjectorAddedTag>()
        .Build(this);

      activeDecalProjectorQuery = new EntityQueryBuilder(Allocator.Temp)
        .WithAll<ObjectInstance, FlatTextureInfo, DecalProjectorAddedTag, AnimatedTag>()
        .Build(this);

      removedDecalProjectoreQuery = new EntityQueryBuilder(Allocator.Temp)
        .WithAll<DecalProjectorAddedTag>()
        .WithNone<FlatTextureInfo>()
        .Build(this);

      viewPartArchetype = World.EntityManager.CreateArchetype(stackalloc[] {
        ComponentType.ReadWrite<FlatTexturePart>(),
        
        ComponentType.ReadWrite<LocalTransform>(),
        ComponentType.ReadWrite<Parent>(),
        
        ComponentType.ReadWrite<LocalToWorld>(),
      });

      instanceLookup = GetComponentLookup<ObjectInstance>(true);
      decorationLookup = GetComponentLookup<ObjectInstance.Decoration>(true);
      doorLookup = GetComponentLookup<ObjectInstance.DoorAndGrating>(true);
      enemyLookup = GetComponentLookup<ObjectInstance.Enemy>(true);

      entitiesGraphicsSystem = World.GetOrCreateSystemManaged<EntitiesGraphicsSystem>();
      materialProviderSystem = World.GetOrCreateSystemManaged<MaterialProviderSystem>();
      spriteSystem = World.GetOrCreateSystemManaged<SpriteSystem>();
    }

    protected override void OnUpdate() {
      instanceLookup.Update(this);
      decorationLookup.Update(this);
      doorLookup.Update(this);

      var ecbSystem = World.GetExistingSystemManaged<EndVariableRateSimulationEntityCommandBufferSystem>();
      var commandBuffer = ecbSystem.CreateCommandBuffer();

      var level = SystemAPI.GetSingleton<Level>();

      // Update if animated
      foreach (var (instanceDataRef, entity) in
        SystemAPI.Query<RefRO<ObjectInstance>>()
        .WithAll<FlatTextureInfo, DecalProjectorAddedTag, AnimatedTag>()
        .WithEntityAccess()) {

        var instanceData = instanceDataRef.ValueRO;
        
        if (instanceData.Class == ObjectClass.DoorAndGrating) continue; // Double sided are handled in FlatTextureSystem

        var materialID = materialProviderSystem.GetResource(
         entity,
         instanceData,
         level,
         instanceLookup,
         decorationLookup,
         doorLookup,
         enemyLookup,
         true,
         out ushort refWidthOverride);

        if (materialID == BatchMaterialID.Null) {
          var currentFrame = instanceData.Info.CurrentFrame != -1 ? instanceData.Info.CurrentFrame : 0;
          var spriteIndex = spriteSystem.GetSpriteIndex(instanceData, currentFrame);
          materialID = materialProviderSystem.GetMaterial(ArtResourceIdBase, spriteIndex, true, true, false);
        }

        if (entityDecalProjectors.TryGetValue(entity, out DecalProjector decalProjector)) {
          UpdateProjectorAsync(decalProjector, materialID, refWidthOverride);

          commandBuffer.SetComponentEnabled<AnimatedTag>(entity, false);
        }
      }

      { // New
        var prototype = EntityManager.CreateEntity(viewPartArchetype); // Sync point

        foreach (var (instanceDataRef, entity) in
          SystemAPI.Query<RefRO<ObjectInstance>>()
          .WithAll<FlatTextureInfo>()
          .WithNone<FlatTextureMeshAddedTag, DecalProjectorAddedTag>()
          .WithEntityAccess())
        {
          var instanceData = instanceDataRef.ValueRO;

          if (instanceData.Class == ObjectClass.DoorAndGrating) continue; // Double sided are handled in FlatTextureSystem

          var materialID = materialProviderSystem.GetResource(
            entity,
            instanceData,
            level,
            instanceLookup,
            decorationLookup,
            doorLookup,
            enemyLookup,
            true,
            out ushort refWidthOverride);

          if (materialID == BatchMaterialID.Null) {
            var currentFrame = instanceData.Info.CurrentFrame != -1 ? instanceData.Info.CurrentFrame : 0;
            var spriteIndex = spriteSystem.GetSpriteIndex(instanceData, currentFrame);
            materialID = materialProviderSystem.GetMaterial(ArtResourceIdBase, spriteIndex, true, true, false);
          }

          if (!entityDecalProjectors.TryGetValue(entity, out DecalProjector decalProjector)) {
            var gameObject = new GameObject {
              name = $"Decal Projector {entity}"
            };
            decalProjector = gameObject.AddComponent<DecalProjector>();
            decalProjector.pivot = Vector3.zero;
            decalProjector.startAngleFade = 0.0f;
            decalProjector.endAngleFade = 5.0f;

            var viewPart = commandBuffer.Instantiate(prototype);
            commandBuffer.SetComponent(viewPart, new Parent { Value = entity });
            commandBuffer.SetComponent(viewPart, LocalTransform.Identity);
            commandBuffer.AddComponent(viewPart, decalProjector);

            commandBuffer.AddComponent<DecalProjectorAddedTag>(entity);
            commandBuffer.AddComponent<AnimatedTag>(entity);

            entityDecalProjectors.Add(entity, decalProjector);
          }

          UpdateProjectorAsync(decalProjector, materialID, refWidthOverride);
        }

        var finalizeCommandBuffer = ecbSystem.CreateCommandBuffer();
        finalizeCommandBuffer.DestroyEntity(prototype);
      }

      // Update transform
      foreach (var (decalProjectorRef, transformRef) in 
               // ReSharper disable once Unity.Entities.MustBeSurroundedWithRefRwRo
               SystemAPI.Query<SystemAPI.ManagedAPI.UnityEngineComponent<DecalProjector>, RefRO<LocalToWorld>>()
                 .WithChangeFilter<LocalToWorld>())
      {
        var transform = transformRef.ValueRO;
        decalProjectorRef.Value.transform.SetPositionAndRotation(transform.Position, transform.Rotation);
      }
    }

    private async void UpdateProjectorAsync(DecalProjector decalProjector, BatchMaterialID materialID, ushort refWidthOverride) {
      decalProjector.material = entitiesGraphicsSystem.GetMaterial(materialID);

      var bitmapDesc = await materialProviderSystem.GetBitmapDesc(materialID);

      float scale = 1f;
      if (refWidthOverride > 0)
        scale = refWidthOverride / (float)bitmapDesc.Size.x;

      var realSize = scale * float2(bitmapDesc.Size.x, bitmapDesc.Size.y) / 64f;

      decalProjector.size = new(realSize.x, realSize.y, 0.2f);
    }

    internal struct DecalProjectorAddedTag : ICleanupComponentData { }
  }
}
