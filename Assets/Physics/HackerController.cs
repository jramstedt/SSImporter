using CharacterController;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.GraphicsIntegration;

namespace SS.Physics {
    public struct HackerController : IComponentData {
        public float3 Gravity;
        public float MovementSpeed;
        public float MaxMovementSpeed;
        public float RotationSpeed;
        public float JumpUpwardsSpeed;
        /** Radians */
        public float MaxSlope;
        public int MaxIterations;
        public float CharacterMass;
        public float CharacterHeight;
        public float CharacterSize;
        public float SkinWidth;
        public float ContactTolerance;
        public byte AffectsPhysicsBodies;
        public byte RaiseCollisionEvents;
        public byte RaiseTriggerEvents;
    }
    
    [WriteGroup(typeof(PhysicsGraphicalInterpolationBuffer))]
    [WriteGroup(typeof(PhysicsGraphicalSmoothing))]
    public struct HackerControllerInternal : IComponentData
    {
        public float3 UnsupportedVelocity;
        public float CurrentRotationAngle;
        public float CurrentLeanAngle;
        public float CurrentCrouch;
        public PhysicsVelocity Velocity;
        
        public float LeanAngle;
        public float Crouch;
        
        public float Height;
        public float3 InputMoveDelta; // Z = Jump, Climb, Rocket
        public float2 InputLookDelta;
        public float InputLeanDelta;

        public bool IsJumping;
        public Util.CharacterSupportState SupportedState;

        public PhysicsCollider HeadCollider;
        
        public float3 HeadRotation;
        public float3 HeadOffset;
    }
}