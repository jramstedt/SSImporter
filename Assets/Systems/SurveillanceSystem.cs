using SS.Resources;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SS.System {
  [CreateAfter(typeof(EntitiesGraphicsSystem))]
  [UpdateInGroup(typeof(InitializationSystemGroup))]
  public partial class SurveillanceSystem : SystemBase {
    private EntityQuery newSurveillanceSourceQuery;
    private EntityQuery activeSurveillanceSourceQuery;
    private EntityQuery removedSurveillanceSourceQuery;

    private MaterialProviderSystem materialProviderSystem;

    protected override void OnCreate() {
      base.OnCreate();

      RequireForUpdate<Level>();

      newSurveillanceSourceQuery = new EntityQueryBuilder(Allocator.Temp)
        .WithAll<SurveillanceSource>()
        .WithNone<CameraAdded>()
        .Build(this);

      activeSurveillanceSourceQuery = new EntityQueryBuilder(Allocator.Temp)
        .WithAll<ObjectInstance, Camera, CameraAdded>()
        .Build(this);

      removedSurveillanceSourceQuery = new EntityQueryBuilder(Allocator.Temp)
        .WithAll<CameraAdded>()
        .WithNone<SurveillanceSource>()
        .Build(this);

      materialProviderSystem = World.GetOrCreateSystemManaged<MaterialProviderSystem>();
    }

    protected override void OnUpdate() {
      Entities
        .WithAll<SurveillanceSource>()
        .WithNone<CameraAdded>()
        .ForEach((Entity entity, in SurveillanceSource surveillanceSource) => {
          var gameObject = new GameObject {
            name = $"Surveillance Camera {surveillanceSource.CameraIndex}"
          };

          var camera = gameObject.AddComponent<Camera>();
          var urpCameraData = gameObject.AddComponent<UniversalAdditionalCameraData>();

          EntityManager.AddComponentObject(entity, camera);
          EntityManager.AddComponentObject(entity, urpCameraData);
          EntityManager.AddComponentData(entity, new CameraAdded() {
            go = gameObject
          });

          camera.targetTexture = materialProviderSystem.GetCameraRenderTexture(surveillanceSource.CameraIndex);
        })
        .WithoutBurst()
        .WithStructuralChanges()
        .Run();

      Entities
        .ForEach((Camera camera, in LocalToWorld transform) => {
          camera.transform.SetPositionAndRotation(transform.Position, transform.Rotation);
        })
        .WithoutBurst()
        .Run();

      Entities
        .WithAll<CameraAdded>()
        .WithNone<SurveillanceSource>()
        .ForEach((Entity entity, CameraAdded cameraData) => {
          GameObject.Destroy(cameraData.go);
          EntityManager.RemoveComponent<CameraAdded>(entity);
        })
        .WithoutBurst()
        .WithStructuralChanges()
        .Run();
    }
  }

  public struct SurveillanceSource : IComponentData {
    public int CameraIndex;
  }

  internal class CameraAdded : ICleanupComponentData {
    public GameObject go;
  }
}
