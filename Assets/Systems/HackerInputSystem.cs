using System;
using SS.Physics;
using SS.Resources;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SS.System {
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class HackerInputSystem : SystemBase {
        public const sbyte CONTROL_MAX_VAL = 100;
        
        public const ushort FIXANG_PI = 0x8000;
        public const ushort MAX_EYE_ANGLE = 8 * FIXANG_PI / 18;
        public const ushort MAX_PITCH_RATE = FIXANG_PI / 4;
        public const ushort MAX_LEAN_RATE = 400;
        
        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction jumpAction;
        private InputAction leanAction;
        private InputAction leanCenterAction;
        
        private InputAction standAction;
        private InputAction stoopAction;
        private InputAction proneAction;
        
        private float2 lookAccumulator;

        protected override void OnCreate() {
            base.OnCreate();
            
            RequireForUpdate<Hacker>();

            moveAction = InputSystem.actions.FindAction(@"Move");
            lookAction = InputSystem.actions.FindAction(@"Look");
            jumpAction = InputSystem.actions.FindAction(@"Jump");
            leanAction = InputSystem.actions.FindAction(@"Lean");
            leanCenterAction = InputSystem.actions.FindAction(@"LeanCenter");
            
            standAction = InputSystem.actions.FindAction(@"Stand");
            stoopAction = InputSystem.actions.FindAction(@"Stoop");
            proneAction = InputSystem.actions.FindAction(@"Prone");
        }

        protected override void OnUpdate() {
            var player = SystemAPI.GetSingletonEntity<Hacker>();
            var hacker = SystemAPI.GetComponentRW<Hacker>(player);
            var controller = SystemAPI.GetComponentRO<HackerControllerComponentData>(player);
            var controllerInternal = SystemAPI.GetComponentRW<HackerControllerInternalData>(player);
            
            float sensitivity = 1f;
            
            float2 moveDelta = moveAction.ReadValue<Vector2>();
            float2 lookDelta = lookAction.ReadValue<Vector2>();
            float jumping = jumpAction.ReadValue<float>();
            float lean = leanAction.ReadValue<float>();
            
            controllerInternal.ValueRW.Input.Movement = new float3(moveDelta, jumping);
            controllerInternal.ValueRW.Input.Looking = lookDelta;
            
            lookAccumulator += lookDelta * sensitivity;
            lookDelta = math.trunc(lookAccumulator);
                
            ReadOnlySpan<sbyte> controls;
            
            unsafe {
                hacker.ValueRW.controls[Hacker.CONTROL_XVEL] = (sbyte)(moveDelta.x * CONTROL_MAX_VAL);
                hacker.ValueRW.controls[Hacker.CONTROL_YVEL] = (sbyte)(moveDelta.y * CONTROL_MAX_VAL);
                hacker.ValueRW.controls[Hacker.CONTROL_ZVEL] = (sbyte)(jumping * CONTROL_MAX_VAL); // TODO FIXME?

                hacker.ValueRW.controls[Hacker.CONTROL_XYROT] = (sbyte)math.clamp(lookDelta.x, -CONTROL_MAX_VAL, CONTROL_MAX_VAL);
                hacker.ValueRW.controls[Hacker.CONTROL_YZROT] = (sbyte)math.clamp(lookDelta.y, -CONTROL_MAX_VAL, CONTROL_MAX_VAL);
                hacker.ValueRW.controls[Hacker.CONTROL_XZROT] = (sbyte)math.clamp(lean * CONTROL_MAX_VAL, -CONTROL_MAX_VAL, CONTROL_MAX_VAL);

                lookAccumulator -= lookDelta;

                fixed (sbyte* c = hacker.ValueRO.controls) {
                    controls = new ReadOnlySpan<sbyte>(c, Hacker.DEGREES_OF_FREEDOM);
                }
            }
            
            lookDelta *= .1f;
            
            // TODO This is for keyboard input
            long fixedAngleDelta = controls[Hacker.CONTROL_YZROT] * MAX_PITCH_RATE / CONTROL_MAX_VAL;
            long fixedAngleLean = controls[Hacker.CONTROL_XZROT] * MAX_LEAN_RATE / CONTROL_MAX_VAL;
            
            // NOTE Not using controls[Hacker.CONTROL_YZROT] because it would limit aim speed and accuracy. However, values are set above to update player data.

            hacker.ValueRW.leanX = (sbyte)math.clamp(hacker.ValueRO.leanX + (int)(lean * SystemAPI.Time.DeltaTime * CONTROL_MAX_VAL), -CONTROL_MAX_VAL, CONTROL_MAX_VAL);
            hacker.ValueRW.eyeAngle = math.clamp(hacker.ValueRW.eyeAngle + (long)(lookDelta.y * SystemAPI.Time.DeltaTime * 65536f / math.PI2), -MAX_EYE_ANGLE, MAX_EYE_ANGLE);
            
            if (standAction.WasPerformedThisFrame())
                hacker.ValueRW.posture = Hacker.Posture.Stand;
            if (stoopAction.WasPerformedThisFrame())
                hacker.ValueRW.posture = Hacker.Posture.Stoop;
            if (proneAction.WasPerformedThisFrame())
                hacker.ValueRW.posture = Hacker.Posture.Prone;
            if (leanCenterAction.WasPerformedThisFrame())
                hacker.ValueRW.leanX = 0; // Center lean
            
            ReadOnlySpan<byte> crouchValue = stackalloc byte[] { 0, 6, 10 };
            
            controllerInternal.ValueRW.Input.LeanAngle = .04f * (hacker.ValueRO.leanX / 3f);
            controllerInternal.ValueRW.Input.Crouch = (1f / 1.5f) * .20f * crouchValue[(int)hacker.ValueRO.posture]; // 1/5 because of 1.5 multiplier in original code?
        }
    }
}