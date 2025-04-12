using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.GraphicsIntegration;
using static CharacterControllerUtilities;

namespace SS.Physics {
    public struct HackerControllerComponentData : IComponentData {
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
        public float SkinWidth;
        public float ContactTolerance;
        public byte AffectsPhysicsBodies;
        public byte RaiseCollisionEvents;
        public byte RaiseTriggerEvents;

        // public Entity Head;
        public PhysicsCollider BodyCollider;
        public PhysicsCollider HeadCollider;
    }
    
    public struct HackerControllerInput : IComponentData
    {
        /** Z = Jump, Climb, Rocket */
        public float3 Movement;
        public float2 Looking;
        public float LeanAngle;
        public float Crouch;
    }
    
    [WriteGroup(typeof(PhysicsGraphicalInterpolationBuffer))]
    [WriteGroup(typeof(PhysicsGraphicalSmoothing))]
    public struct HackerControllerInternalData : IComponentData
    {
        public float3 UnsupportedVelocity;
        public PhysicsVelocity Velocity;
        public float CurrentRotationAngle;
        public float CurrentLeanAngle;
        public float CurrentCrouch;
        
        public Entity Entity;
        
        public bool IsJumping;
        public HackerControllerInput Input;
        public CharacterSupportState SupportedState;
        
        public float Height;
        public float3 HeadRotation;
        public float3 HeadOffset;
    }

    /*
    public struct HackerHead : IComponentData {
        public Entity Body;
    }
    */
}