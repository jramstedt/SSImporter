using SS.ObjectProperties;
using SS.Resources;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Rendering;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace SS.System {
  [UpdateInGroup(typeof(LateSimulationSystemGroup))]
  public partial class SpriteSystem : SystemBase {
    private EntityQuery newSpriteQuery;
    private EntityQuery activeSpriteQuery;
    private EntityQuery removedSpriteQuery;

    private EntityArchetype viewPartArchetype;

    private SpriteLibrary spriteLibrary;
    private Resources.ObjectProperties objectProperties;

    protected override async void OnCreate() {
      base.OnCreate();

      newSpriteQuery = GetEntityQuery(new EntityQueryDesc {
        All = new ComponentType[] { ComponentType.ReadOnly<SpriteInfo>() },
        None = new ComponentType[] { ComponentType.ReadOnly<SpriteAddedTag>() },
      });

      activeSpriteQuery = GetEntityQuery(new EntityQueryDesc {
        All = new ComponentType[] {
          ComponentType.ReadOnly<SpriteInfo>(),
          ComponentType.ReadOnly<SpriteAddedTag>()
        }
      });

      removedSpriteQuery = GetEntityQuery(new EntityQueryDesc {
        All = new ComponentType[] { ComponentType.ReadOnly<SpriteAddedTag>() },
        None = new ComponentType[] { ComponentType.ReadOnly<SpriteInfo>() },
      });

      viewPartArchetype = World.EntityManager.CreateArchetype(
        typeof(SpritePart),
        typeof(Parent),
        typeof(LocalToWorld),
        typeof(LocalToParent),
        typeof(Translation),
        typeof(Rotation),
        typeof(Scale),
        typeof(RenderBounds),
        typeof(RenderMesh)
      );

      spriteLibrary = await Services.SpriteLibrary;
      objectProperties = await Services.ObjectProperties;
    }

    protected override void OnUpdate() {
      if (spriteLibrary == null || objectProperties == null) return;

      var ecbSystem = World.GetExistingSystem<EndInitializationEntityCommandBufferSystem>();
      var commandBuffer = ecbSystem.CreateCommandBuffer();

      Entities
        .WithAll<SpriteInfo, ObjectInstance>()
        .WithNone<SpriteAddedTag>()
        .ForEach((Entity entity, in ObjectInstance instanceData) => {
          var currentFrame = instanceData.Info.CurrentFrame != byte.MaxValue ? instanceData.Info.CurrentFrame : 0;
          var (spriteMesh, material) = spriteLibrary.GetSprite(instanceData, currentFrame);
          if (spriteMesh == null || material == null) return;

          var bitmapSet = spriteMesh.BitmapSet;
          var mesh = spriteMesh.Mesh;

          var baseData = objectProperties.BasePropertyData(instanceData);

          var scale = (float)(2048 / 3) / (float)ushort.MaxValue;
          var radius = (float)baseData.Radius / (float)MapElement.PHYSICS_RADIUS_UNIT;

          if (bitmapSet.AnchorPoint.x > 0 || bitmapSet.AnchorPoint.y > 0)
            radius = 0f;

          if (baseData.IsDoubleSize)
            scale *= 2f;

          var viewPart = commandBuffer.CreateEntity(viewPartArchetype);
          commandBuffer.SetComponent(viewPart, new SpritePart { CurrentFrame = currentFrame });
          commandBuffer.SetComponent(viewPart, default(LocalToWorld));
          commandBuffer.SetComponent(viewPart, new Parent { Value = entity });
          //commandBuffer.SetComponent(viewPart, new LocalToParent { Value = math.mul(Unity.Mathematics.float4x4.Translate(new float3(0f, -radius, 0f)), Unity.Mathematics.float4x4.Scale(new float3(scale, scale, scale))) });
          commandBuffer.SetComponent(viewPart, default(LocalToParent));
          commandBuffer.SetComponent(viewPart, new Translation { Value = new float3(0f, -radius, 0f) });
          commandBuffer.SetComponent(viewPart, new Rotation { Value = quaternion.identity });
          commandBuffer.SetComponent(viewPart, new Scale { Value = scale });
          commandBuffer.SetComponent(viewPart, new RenderBounds { Value = new AABB { Center = mesh.bounds.center, Extents = mesh.bounds.extents } });
          commandBuffer.SetSharedComponent(viewPart, new RenderMesh {
            mesh = mesh,
            material = material,
            subMesh = 0,
            layer = 0,
            castShadows = ShadowCastingMode.On,
            receiveShadows = true,
            needMotionVectorPass = false,
            layerMask = uint.MaxValue
          });

          commandBuffer.AddComponent<SpriteAddedTag>(entity);
        })
        .WithoutBurst()
        .Run();

      var towardsCameraRotation = quaternion.LookRotation(-Camera.main.transform.forward, Vector3.up);

      Entities
        .WithAll<SpritePart, LocalToParent>()
        .ForEach((ref Rotation rotation) => {
          rotation.Value = towardsCameraRotation;
        })
        .Run();

      ecbSystem.AddJobHandleForProducer(Dependency);
    }
  }

  public struct SpriteInfo : IComponentData {}

  public struct SpritePart : IComponentData {
    public int CurrentFrame;
  }

  internal struct SpriteAddedTag : ISystemStateComponentData { }
}
