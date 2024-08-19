using SS.Resources;
using SS.Physics;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace SS.System {
  [UpdateBefore(typeof(SpriteSystem))]
  [UpdateBefore(typeof(MeshInterpeterSystem))]
  [UpdateInGroup(typeof(VariableRateSimulationSystemGroup))]
  public partial class CameraSystem : SystemBase {
    protected override void OnCreate() {
      base.OnCreate();

      RequireForUpdate<Level>();
    }

    protected override void OnUpdate() {
      var player = SystemAPI.GetSingletonEntity<Hacker>();
      var hacker = SystemAPI.GetComponentRO<Hacker>(player);
      var controller = SystemAPI.GetComponentRO<HackerControllerInternalData>(player);
      var localTransform = SystemAPI.GetComponentRO<LocalTransform>(player);

      var rotation = controller.ValueRO.HeadRotation;
      var offset = controller.ValueRO.HeadOffset;
      
      var cameraRotation = math.mul(localTransform.ValueRO.Rotation, quaternion.Euler(
        (.1f * rotation.x) - hacker.ValueRO.eyeAngle * math.PI2 / 65536f,
        rotation.y,
        -.03f * rotation.z
      ));
      
      // TODO cyberspace

      if (Camera.main) {
        Camera.main.transform.SetPositionAndRotation(localTransform.ValueRO.Position + math.mul(localTransform.ValueRO.Rotation, offset), cameraRotation);
        Camera.main.nearClipPlane = 0.03f;
      }
    }
  }
}