using SS.Resources;
using Unity.Burst;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SS.System {
  [CreateAfter(typeof(EntitiesGraphicsSystem))]
  [UpdateInGroup(typeof(InitializationSystemGroup))]
  public partial struct SurveillanceSystem : ISystem {
    /*
    private EntityQuery newSurveillanceSourceQuery;
    private EntityQuery activeSurveillanceSourceQuery;
    private EntityQuery removedSurveillanceSourceQuery;
    */

    [BurstCompile]
    public void OnCreate(ref SystemState state) {
      state.RequireForUpdate<EndInitializationEntityCommandBufferSystem.Singleton>();
      state.RequireForUpdate<MaterialProviderSystem.MaterialProviderSystemData>();
      state.RequireForUpdate<Level>();

      /*
      newSurveillanceSourceQuery = new EntityQueryBuilder(Allocator.Temp)
        .WithAll<SurveillanceSource>()
        .WithNone<CameraAdded>()
        .Build(ref state);

      activeSurveillanceSourceQuery = new EntityQueryBuilder(Allocator.Temp)
        .WithAll<ObjectInstance, Camera, CameraAdded>()
        .Build(ref state);

      removedSurveillanceSourceQuery = new EntityQueryBuilder(Allocator.Temp)
        .WithAll<CameraAdded>()
        .WithNone<SurveillanceSource>()
        .Build(ref state);
      */
    }
    
    [BurstCompile]
    public void OnDestroy(ref SystemState state) { }

    // [BurstCompile]
    public void OnUpdate(ref SystemState state) {
      var ecbSingleton = SystemAPI.GetSingleton<EndInitializationEntityCommandBufferSystem.Singleton>();
      var commandBuffer = ecbSingleton.CreateCommandBuffer(state.World.Unmanaged);
      
      var materialSystemData = SystemAPI.GetSingleton<MaterialProviderSystem.MaterialProviderSystemData>();
      var cameraTextureSets = materialSystemData.CameraTextureSets;
      var cameraSourceCount = materialSystemData.CameraSourceCount;

      foreach (var (surveillanceSourceRef, localToWorldRef, entity) in SystemAPI.Query<RefRO<SurveillanceSource>, RefRO<LocalToWorld>>().WithNone<CameraAdded>().WithEntityAccess()) {
        var surveillanceSource = surveillanceSourceRef.ValueRO;
        
        var gameObject = new GameObject {
          name = $"Surveillance Camera {surveillanceSource.CameraIndex}"
        };

        var camera = gameObject.AddComponent<Camera>();
        var urpCameraData = gameObject.AddComponent<UniversalAdditionalCameraData>();
        
        var localToWorld = localToWorldRef.ValueRO;
        camera.transform.SetPositionAndRotation(localToWorld.Position, localToWorld.Rotation);
        camera.targetTexture = cameraTextureSets[surveillanceSource.CameraIndex].Texture;
        camera.clearFlags = CameraClearFlags.Depth;
        
        commandBuffer.AddComponent(entity, camera);
        commandBuffer.AddComponent(entity, urpCameraData);
        commandBuffer.AddComponent(entity, new CameraAdded() {
          GameObject = gameObject,
          CameraIndex = surveillanceSource.CameraIndex
        });

        ++cameraSourceCount[surveillanceSource.CameraIndex];
      }

      foreach (var (cameraRef, localToWorldRef) in
               // ReSharper disable once Unity.Entities.MustBeSurroundedWithRefRwRo
               SystemAPI.Query<SystemAPI.ManagedAPI.UnityEngineComponent<Camera>, RefRO<LocalToWorld>>()
                 .WithChangeFilter<LocalToWorld>())
      {
        var localToWorld = localToWorldRef.ValueRO;
        cameraRef.Value.transform.SetPositionAndRotation(localToWorld.Position, localToWorld.Rotation);
      }

      foreach (var (cameraDataRef, entity) in
               SystemAPI.Query<RefRO<CameraAdded>>()
                 .WithNone<SurveillanceSource>()
                 .WithEntityAccess())
      {
        var cameraData = cameraDataRef.ValueRO;
        
        --cameraSourceCount[cameraData.CameraIndex];
        Object.Destroy(cameraData.GameObject);
        commandBuffer.RemoveComponent<CameraAdded>(entity);
      }
    }
  }

  public struct SurveillanceSource : IComponentData {
    public byte CameraIndex;
  }

  internal struct CameraAdded : ICleanupComponentData {
    public UnityObjectRef<GameObject> GameObject;
    public byte CameraIndex;
  }
}
