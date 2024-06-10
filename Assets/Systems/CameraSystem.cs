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
      var localTransform = SystemAPI.GetComponentRO<LocalTransform>(player);
      var controller = SystemAPI.GetComponentRO<HackerControllerInternal>(player);
      
      float3 rot = new float3(0f, 0f, controller.ValueRO.CurrentLeanAngle);
      
      float3 offset = math.mul(localTransform.ValueRO.Rotation, new float3(
        1.5f * controller.ValueRO.Height * math.sin(rot.z),
        controller.ValueRO.Height * math.cos(rot.x) * math.cos(rot.z),
        controller.ValueRO.Height * math.sin(rot.x)
      ));

      var cameraRotation = math.mul(localTransform.ValueRO.Rotation, quaternion.Euler(
        -hacker.ValueRO.eyeAngle * math.PI2 / 65536f,
        .1f * rot.y,
        -.03f * rot.z
      ));
      
      // TODO cyberspace
      
      if (Camera.main)
        Camera.main.transform.SetPositionAndRotation(localTransform.ValueRO.Position + offset, cameraRotation);
    }
  }
}