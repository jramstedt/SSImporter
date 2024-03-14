using SS.Resources;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Mathematics;
using System;
using Unity.Collections;
using Unity.Physics;

namespace SS.System {
  public partial class CameraSystem : SystemBase {
    public const sbyte CONTROL_MAX_VAL = 100;

    public const ushort FIXANG_PI = 0x8000;
    public const ushort MAX_EYE_ANGLE = 8 * FIXANG_PI / 18;
    public const ushort MAX_PITCH_RATE = FIXANG_PI / 7;

    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction jumpAction;

    private float2 lookAccumulator;

    protected override void OnCreate() {
      base.OnCreate();

      RequireForUpdate<Level>();

      moveAction = InputSystem.actions.FindAction(@"Move");
      lookAction = InputSystem.actions.FindAction(@"Look");
      jumpAction = InputSystem.actions.FindAction(@"Jump");

      lookAccumulator = float2.zero;
    }

    protected override void OnUpdate() {
      var player = SystemAPI.GetSingletonEntity<Hacker>();
      var hacker = SystemAPI.GetComponentRW<Hacker>(player);
      var localTransform = SystemAPI.GetComponentRW<LocalTransform>(player);
      var physicsVelocity = SystemAPI.GetComponentRW<PhysicsVelocity>(player);

      float2 moveDelta = moveAction.ReadValue<Vector2>();
      float2 lookDelta = lookAction.ReadValue<Vector2>();
      float jumping = jumpAction.ReadValue<float>();

      float sensitivity = 1f;

      lookAccumulator += lookDelta * sensitivity;
      lookDelta = math.trunc(lookAccumulator);

      ReadOnlySpan<sbyte> controls;

      unsafe {
        hacker.ValueRW.controls[Hacker.CONTROL_XVEL] = (sbyte)(moveDelta.x * CONTROL_MAX_VAL);
        hacker.ValueRW.controls[Hacker.CONTROL_YVEL] = (sbyte)(moveDelta.y * CONTROL_MAX_VAL);
        hacker.ValueRW.controls[Hacker.CONTROL_ZVEL] = (sbyte)(jumping * CONTROL_MAX_VAL); // TODO FIXME?

        hacker.ValueRW.controls[Hacker.CONTROL_XYROT] = (sbyte)math.clamp(lookDelta.x, -CONTROL_MAX_VAL, CONTROL_MAX_VAL);
        hacker.ValueRW.controls[Hacker.CONTROL_YZROT] = (sbyte)math.clamp(lookDelta.y, -CONTROL_MAX_VAL, CONTROL_MAX_VAL);
        hacker.ValueRW.controls[Hacker.CONTROL_XZROT] = 0;

        lookAccumulator -= lookDelta;

        fixed (sbyte* c = hacker.ValueRO.controls) {
          controls = new ReadOnlySpan<sbyte>(c, Hacker.DEGREES_OF_FREEDOM);
        }
      }

      lookDelta *= .1f;

      // TODO This is for keyboard input
      long fixedAngleDelta = controls[Hacker.CONTROL_YZROT] * MAX_PITCH_RATE / CONTROL_MAX_VAL;

      // NOTE Not using controls[Hacker.CONTROL_YZROT] because it would limit aim speed and accuracy. However values are set above to update player data.

      hacker.ValueRW.eyeAngle = global::System.Math.Clamp(hacker.ValueRW.eyeAngle + (long)(lookDelta.y * SystemAPI.Time.DeltaTime * 65536f / math.PI2), -MAX_EYE_ANGLE, MAX_EYE_ANGLE);

      physicsVelocity.ValueRW.Angular = new float3(0f, lookDelta.x, 0f);

      if (math.lengthsq(moveDelta) > float.Epsilon || jumping > float.Epsilon) {
        var linerVel = math.mul(localTransform.ValueRO.Rotation, new float3(
          controls[Hacker.CONTROL_XVEL] * 0.01f,
          controls[Hacker.CONTROL_ZVEL] * 0.01f,
          controls[Hacker.CONTROL_YVEL] * 0.01f
        ));

        physicsVelocity.ValueRW.Linear = linerVel + physicsVelocity.ValueRW.Linear * 0.5f;
      } else {
        physicsVelocity.ValueRW.Linear *= 0.5f;
      }

      //TODO physic update

      var cameraRotation = math.mul(localTransform.ValueRO.Rotation, quaternion.RotateX(-hacker.ValueRW.eyeAngle * math.PI2 / 65536f));

      Camera.main.transform.SetPositionAndRotation(localTransform.ValueRO.Position, cameraRotation);
    }
  }
}