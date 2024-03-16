﻿using SS.Resources;
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
    private EntityQuery animatedQuery;

    private EntityArchetype viewPartArchetype;

    private ComponentLookup<ObjectInstance> instanceLookup;
    private ComponentLookup<ObjectInstance.Decoration> decorationLookup;
    private ComponentLookup<ObjectInstance.DoorAndGrating> doorLookup;

    private EntitiesGraphicsSystem entitiesGraphicsSystem;
    private MaterialProviderSystem materialProviderSystem;
    private SpriteSystem spriteSystem;

    private Resources.ObjectProperties objectProperties;

    protected override async void OnCreate() {
      base.OnCreate();

      RequireForUpdate<Level>();
      RequireForUpdate<AsyncLoadTag>();

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

      animatedQuery = new EntityQueryBuilder(Allocator.Temp)
        .WithAll<AnimationData>()
        .Build(this);

      viewPartArchetype = World.EntityManager.CreateArchetype(
        typeof(FlatTexturePart),

        typeof(LocalTransform),
        typeof(Parent),

        typeof(LocalToWorld)
      );

      instanceLookup = GetComponentLookup<ObjectInstance>(true);
      decorationLookup = GetComponentLookup<ObjectInstance.Decoration>(true);
      doorLookup = GetComponentLookup<ObjectInstance.DoorAndGrating>(true);

      entitiesGraphicsSystem = World.GetOrCreateSystemManaged<EntitiesGraphicsSystem>();
      materialProviderSystem = World.GetOrCreateSystemManaged<MaterialProviderSystem>();
      spriteSystem = World.GetOrCreateSystemManaged<SpriteSystem>();

      objectProperties = await Services.ObjectProperties;

      EntityManager.AddComponent<AsyncLoadTag>(SystemHandle);
    }

    protected override void OnUpdate() {
      instanceLookup.Update(this);
      decorationLookup.Update(this);
      doorLookup.Update(this);

      var ecbSystem = World.GetExistingSystemManaged<EndVariableRateSimulationEntityCommandBufferSystem>();
      var commandBuffer = ecbSystem.CreateCommandBuffer();

      var level = SystemAPI.GetSingleton<Level>();

      using var animationData = animatedQuery.ToComponentDataArray<AnimationData>(Allocator.Temp);

      {
        foreach (var (instanceData, entity) in
          SystemAPI.Query<ObjectInstance>()
          .WithAll<FlatTextureInfo, DecalProjectorAddedTag, AnimatedTag>()
          .WithEntityAccess()) {

          if (instanceData.Class == ObjectClass.DoorAndGrating) continue; // Double sided are handled in FlatTextureSystem

          var materialID = GetResource(
           entity,
           instanceData,
           level,
           objectProperties.ObjectDatasBlobAsset,
           materialProviderSystem,
           instanceLookup,
           decorationLookup,
           doorLookup,
           animationData.AsReadOnly(),
           true,
           out ushort refWidthOverride);

          if (materialID == BatchMaterialID.Null) {
            var currentFrame = instanceData.Info.CurrentFrame != -1 ? instanceData.Info.CurrentFrame : 0;
            var spriteIndex = spriteSystem.GetSpriteIndex(instanceData, currentFrame);
            materialID = materialProviderSystem.GetMaterial(ArtResourceIdBase, spriteIndex, true, true);
          }

          if (entityDecalProjectors.TryGetValue(entity, out DecalProjector decalProjector)) {
            UpdateProjectorAsync(decalProjector, materialID, refWidthOverride);

            commandBuffer.RemoveComponent<AnimatedTag>(entity);
          }
        }
      }

      {
        var prototype = EntityManager.CreateEntity(viewPartArchetype); // Sync point

        foreach (var (instanceData, entity) in
          SystemAPI.Query<ObjectInstance>()
          .WithAll<FlatTextureInfo>()
          .WithNone<FlatTextureMeshAddedTag, DecalProjectorAddedTag>()
          .WithEntityAccess()) {

          if (instanceData.Class == ObjectClass.DoorAndGrating) continue; // Double sided are handled in FlatTextureSystem

          var materialID = GetResource(
            entity,
            instanceData,
            level,
            objectProperties.ObjectDatasBlobAsset,
            materialProviderSystem,
            instanceLookup,
            decorationLookup,
            doorLookup,
            animationData.AsReadOnly(),
            true,
            out ushort refWidthOverride);

          if (materialID == BatchMaterialID.Null) {
            var currentFrame = instanceData.Info.CurrentFrame != -1 ? instanceData.Info.CurrentFrame : 0;
            var spriteIndex = spriteSystem.GetSpriteIndex(instanceData, currentFrame);
            materialID = materialProviderSystem.GetMaterial(ArtResourceIdBase, spriteIndex, true, true);
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

            entityDecalProjectors.Add(entity, decalProjector);
          }

          UpdateProjectorAsync(decalProjector, materialID, refWidthOverride);
        }

        var finalizeCommandBuffer = ecbSystem.CreateCommandBuffer();
        finalizeCommandBuffer.DestroyEntity(prototype);
      }

      Entities
        .WithAll<FlatTexturePart>()
        .ForEach((DecalProjector projector, in LocalToWorld transform) => {
          projector.transform.SetPositionAndRotation(transform.Position, transform.Rotation);
        })
        .WithoutBurst()
        .Run();
    }

    async void UpdateProjectorAsync(DecalProjector decalProjector, BatchMaterialID materialID, ushort refWidthOverride) {
      decalProjector.material = entitiesGraphicsSystem.GetMaterial(materialID);

      var bitmapDesc = await materialProviderSystem.GetBitmapDesc(materialID);

      float scale = 1f;
      if (refWidthOverride > 0)
        scale = refWidthOverride / (float)bitmapDesc.Size.x;

      var realSize = scale * float2(bitmapDesc.Size.x, bitmapDesc.Size.y) / 64f;

      decalProjector.size = new(realSize.x, realSize.y, 0.2f);
    }

    private struct AsyncLoadTag : IComponentData { }

    internal struct DecalProjectorAddedTag : ICleanupComponentData { }
  }
}
