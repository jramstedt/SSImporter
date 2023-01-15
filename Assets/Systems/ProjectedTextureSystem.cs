﻿using Mono.Cecil;
using SS.Resources;
using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.ResourceManagement.AsyncOperations;
using static SS.TextureUtils;
using static Unity.Mathematics.math;

namespace SS.System {
  [CreateAfter(typeof(MaterialProviderSystem))]
  [UpdateInGroup(typeof(VariableRateSimulationSystemGroup))]
  public partial class ProjectedTextureSystem : SystemBase {
    private Dictionary<Entity, DecalProjector> resourceDecalProjectors;

    private EntityQuery newFlatTextureQuery;
    private EntityQuery activeDecalProjectorQuery;
    private EntityQuery removedDecalProjectoreQuery;

    private EntityArchetype viewPartArchetype;

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

      resourceDecalProjectors = new();

      newFlatTextureQuery = GetEntityQuery(new EntityQueryDesc {
        All = new ComponentType[] { ComponentType.ReadOnly<FlatTextureInfo>(), ComponentType.ReadOnly<ObjectInstance>() },
        None = new ComponentType[] { ComponentType.ReadOnly<FlatTextureMeshAddedTag>(), ComponentType.ReadOnly<DecalProjectorAddedTag>() },
      });

      activeDecalProjectorQuery = GetEntityQuery(new EntityQueryDesc {
        All = new ComponentType[] {
          ComponentType.ReadOnly<ObjectInstance>(),

          ComponentType.ReadOnly<FlatTextureInfo>(),
          ComponentType.ReadOnly<DecalProjectorAddedTag>(),
          ComponentType.ReadOnly<AnimatedTag>(),
        }
      });

      removedDecalProjectoreQuery = GetEntityQuery(new EntityQueryDesc {
        All = new ComponentType[] { ComponentType.ReadOnly<DecalProjectorAddedTag>() },
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
        var prototype = EntityManager.CreateEntity(viewPartArchetype); // Sync point

        foreach (var (instanceData, entity) in SystemAPI.Query<ObjectInstance>().WithAll<FlatTextureInfo>().WithNone<FlatTextureMeshAddedTag, DecalProjectorAddedTag>().WithEntityAccess()) {
          if (instanceData.Class == ObjectClass.DoorAndGrating) continue; // Double sided are handled in FlatTextureSystem

          var materialID = GetResource(
            entity,
            instanceData,
            level,
            objectProperties.ObjectDatasBlobAsset,
            materialProviderSystem,
            instanceLookup,
            decorationLookup,
            out ushort refWidthOverride);

          if (materialID == BatchMaterialID.Null) {
            var currentFrame = instanceData.Info.CurrentFrame != -1 ? instanceData.Info.CurrentFrame : 0;
            var spriteIndex = spriteSystem.GetSpriteIndex(instanceData, currentFrame);
            materialID = materialProviderSystem.GetMaterial($"{ArtResourceIdBase}:{spriteIndex}", true);
          }

          if (!resourceDecalProjectors.TryGetValue(entity, out DecalProjector decalProjector)) {
            var gameObject = new GameObject {
              name = $"Decal Projector {entity}"
            };
            decalProjector = gameObject.AddComponent<DecalProjector>();
            decalProjector.pivot = Vector3.zero;
            decalProjector.startAngleFade = 0.0f;
            decalProjector.endAngleFade = 5.0f;

            var material = new Material(Shader.Find(@"Shader Graphs/Decal 2"));
            decalProjector.material = material;

            var viewPart = commandBuffer.Instantiate(prototype);
            commandBuffer.SetComponent(viewPart, new Parent { Value = entity });
            commandBuffer.SetComponent(viewPart, LocalTransform.Identity);
            commandBuffer.AddComponent(viewPart, decalProjector);

            commandBuffer.AddComponent<DecalProjectorAddedTag>(entity);

            resourceDecalProjectors.Add(entity, decalProjector);
          }

          var loadOp = materialProviderSystem.GetBitmapSet(materialID);
          loadOp.Completed += loadOp => {
            if (loadOp.Status != AsyncOperationStatus.Succeeded)
              throw loadOp.OperationException;

            var bitmapset = loadOp.Result;
            var bitmapDesc = bitmapset.Description;

            float scale = 1f;
            if (refWidthOverride > 0)
              scale = refWidthOverride / bitmapDesc.Size.x;

            var realSize = scale * float2(bitmapDesc.Size.x, bitmapDesc.Size.y) / 64f;

            decalProjector.size = new() { x = realSize.x, y = realSize.y, z = 0.2f };
            decalProjector.material.SetTexture(Shader.PropertyToID(@"Base_Map"), bitmapset.Texture);
          };
        }

        var finalizeCommandBuffer = ecbSystem.CreateCommandBuffer();
        finalizeCommandBuffer.DestroyEntity(prototype);
      }

      Entities
        .WithAll<FlatTexturePart>()
        .ForEach((DecalProjector projector, in WorldTransform transform) => {
          projector.transform.SetPositionAndRotation(transform.Position, transform.Rotation);
        })
        .WithoutBurst()
        .Run();
    }

    private struct AsyncLoadTag : IComponentData { }

    internal struct DecalProjectorAddedTag : ICleanupComponentData { }
  }
}
