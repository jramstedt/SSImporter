using System;
using SS.Physics;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Extensions;
using Unity.Physics.GraphicsIntegration;
using Unity.Physics.Stateful;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Assertions;
using static CharacterControllerUtilities;
using static Unity.Physics.PhysicsStep;
using Math = Unity.Physics.Math;

// override the behavior of BufferInterpolatedRigidBodiesMotion
[UpdateInGroup(typeof(PhysicsSystemGroup))]
[UpdateAfter(typeof(PhysicsInitializeGroup)), UpdateBefore(typeof(ExportPhysicsWorld))]
[UpdateAfter(typeof(BufferInterpolatedRigidBodiesMotion))]
[RequireMatchingQueriesForUpdate]
public partial struct BufferInterpolatedCharacterControllerMotion : ISystem
{
    public partial struct UpdateCCInterpolationBuffersJobParallel : IJobEntity
    {
        public void Execute(ref PhysicsGraphicalInterpolationBuffer interpolationBuffer, in HackerControllerInternalData ccInternalData, in LocalTransform localTransform)
        {
            interpolationBuffer = new PhysicsGraphicalInterpolationBuffer
            {
                PreviousTransform = new RigidTransform(localTransform.Rotation, localTransform.Position),

                PreviousVelocity = ccInternalData.Velocity,
            };
        }
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        state.Dependency = new UpdateCCInterpolationBuffersJobParallel()
            .ScheduleParallel(state.Dependency);
    }
}

[RequireMatchingQueriesForUpdate]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(PhysicsSystemGroup))]
public partial struct CharacterControllerSystem : ISystem
{
    const float k_DefaultTau = 0.4f;
    const float k_DefaultDamping = 0.9f;

    private CharacterControllerTypeHandles m_Handles;

    struct CharacterControllerTypeHandles
    {
        public ComponentTypeHandle<HackerControllerInternalData> CharacterControllerInternalType;

        public ComponentTypeHandle<LocalTransform> LocalTransformType;

        public BufferTypeHandle<StatefulCollisionEvent> CollisionEventBufferType;
        public BufferTypeHandle<StatefulTriggerEvent> TriggerEventBufferType;
        public ComponentTypeHandle<HackerControllerComponentData> CharacterControllerComponentType;
        public ComponentTypeHandle<PhysicsCollider> PhysicsColliderType;
        public ComponentTypeHandle<PhysicsGraphicalSmoothing> PhysicsGraphicalSmoothingType;

        public ComponentLookup<PhysicsVelocity> PhysicsVelocityData;
        public ComponentLookup<PhysicsMass> PhysicsMassData;

        public ComponentLookup<LocalTransform> LocalTransformData;


        public CharacterControllerTypeHandles(ref SystemState state)
        {
            CharacterControllerInternalType = state.GetComponentTypeHandle<HackerControllerInternalData>();

            LocalTransformType = state.GetComponentTypeHandle<LocalTransform>();

            CollisionEventBufferType = state.GetBufferTypeHandle<StatefulCollisionEvent>();
            TriggerEventBufferType = state.GetBufferTypeHandle<StatefulTriggerEvent>();
            PhysicsGraphicalSmoothingType = state.GetComponentTypeHandle<PhysicsGraphicalSmoothing>();
            CharacterControllerComponentType = state.GetComponentTypeHandle<HackerControllerComponentData>(true);
            PhysicsColliderType = state.GetComponentTypeHandle<PhysicsCollider>(true);

            PhysicsVelocityData = state.GetComponentLookup<PhysicsVelocity>();
            PhysicsMassData = state.GetComponentLookup<PhysicsMass>(true);

            LocalTransformData = state.GetComponentLookup<LocalTransform>(true);
        }

        public void Update(ref SystemState state)
        {
            CharacterControllerInternalType.Update(ref state);

            LocalTransformType.Update(ref state);

            CollisionEventBufferType.Update(ref state);
            TriggerEventBufferType.Update(ref state);
            PhysicsGraphicalSmoothingType.Update(ref state);
            CharacterControllerComponentType.Update(ref state);
            PhysicsColliderType.Update(ref state);

            PhysicsVelocityData.Update(ref state);
            PhysicsMassData.Update(ref state);

            LocalTransformData.Update(ref state);
        }
    }

    [BurstCompile]
    struct CharacterControllerJob : IJobChunk
    {
        public float DeltaTime;

        [ReadOnly] public PhysicsWorldSingleton PhysicsWorldSingleton;
        public ComponentTypeHandle<HackerControllerInternalData> CharacterControllerInternalType;

        public ComponentTypeHandle<LocalTransform> LocalTransformType;

        public BufferTypeHandle<StatefulCollisionEvent> CollisionEventBufferType;
        public BufferTypeHandle<StatefulTriggerEvent> TriggerEventBufferType;
        [ReadOnly] public ComponentTypeHandle<HackerControllerComponentData> CharacterControllerComponentType;
        [ReadOnly] public ComponentTypeHandle<PhysicsCollider> PhysicsColliderType;

        // Stores impulses we wish to apply to dynamic bodies the character is interacting with.
        // This is needed to avoid race conditions when 2 characters are interacting with the
        // same body at the same time.
        [NativeDisableParallelForRestriction] public NativeStream.Writer DeferredImpulseWriter;

        public unsafe void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            Assert.IsFalse(useEnabledMask);
            var chunkCCData = chunk.GetNativeArray(ref CharacterControllerComponentType);
            var chunkCCInternalData = chunk.GetNativeArray(ref CharacterControllerInternalType);
            var chunkPhysicsColliderData = chunk.GetNativeArray(ref PhysicsColliderType);

            var chunkLocalTransformData = chunk.GetNativeArray(ref LocalTransformType);


            var hasChunkCollisionEventBufferType = chunk.Has(ref CollisionEventBufferType);
            var hasChunkTriggerEventBufferType = chunk.Has(ref TriggerEventBufferType);

            BufferAccessor<StatefulCollisionEvent> collisionEventBuffers = default;
            BufferAccessor<StatefulTriggerEvent> triggerEventBuffers = default;
            if (hasChunkCollisionEventBufferType)
            {
                collisionEventBuffers = chunk.GetBufferAccessor(ref CollisionEventBufferType);
            }
            if (hasChunkTriggerEventBufferType)
            {
                triggerEventBuffers = chunk.GetBufferAccessor(ref TriggerEventBufferType);
            }

            DeferredImpulseWriter.BeginForEachIndex(unfilteredChunkIndex);

            for (int i = 0; i < chunk.Count; i++)
            {
                var ccComponentData = chunkCCData[i];
                var ccInternalData = chunkCCInternalData[i];
                var collider = chunkPhysicsColliderData[i];
                var headCollider = ccComponentData.HeadCollider;
                
                var localTransform = chunkLocalTransformData[i];

                DynamicBuffer<StatefulCollisionEvent> collisionEventBuffer = default;
                DynamicBuffer<StatefulTriggerEvent> triggerEventBuffer = default;

                if (hasChunkCollisionEventBufferType)
                {
                    collisionEventBuffer = collisionEventBuffers[i];
                }

                if (hasChunkTriggerEventBufferType)
                {
                    triggerEventBuffer = triggerEventBuffers[i];
                }
                
                /*
                 Nope
                if (bodyCollider.Value.Value.Type == ColliderType.Compound) {
                    var compoundPtr = (CompoundCollider*)bodyCollider.Value.GetUnsafePtr();
                    var key = compoundPtr->ConvertChildIndexToColliderKey(1);
                    if (bodyCollider.Value.Value.GetChild(ref key, out var child)) {
                        //child.TransformFromChild = new RigidTransform(quaternion.identity, ccInternalData.HeadOffset);
                    }
                }
                */

                // Collision filter must be valid
                if (!collider.IsValid || collider.Value.Value.GetCollisionFilter().IsEmpty)
                    continue;
                if (!headCollider.IsValid || headCollider.Value.Value.GetCollisionFilter().IsEmpty)
                    continue;

                var up = math.select(math.up(), -math.normalize(ccComponentData.Gravity),
                    math.lengthsq(ccComponentData.Gravity) > 0f);

                // Character step input
                CharacterControllerStepInput stepInput = new CharacterControllerStepInput
                {
                    PhysicsWorldSingleton = PhysicsWorldSingleton,
                    DeltaTime = DeltaTime,
                    Up = up,
                    Gravity = ccComponentData.Gravity,
                    MaxIterations = ccComponentData.MaxIterations,
                    Tau = k_DefaultTau,
                    Damping = k_DefaultDamping,
                    SkinWidth = ccComponentData.SkinWidth,
                    ContactTolerance = ccComponentData.ContactTolerance,
                    MaxSlope = ccComponentData.MaxSlope,
                    RigidBodyIndex = PhysicsWorldSingleton.PhysicsWorld.GetRigidBodyIndex(ccInternalData.Entity),
                    CurrentVelocity = ccInternalData.Velocity.Linear,
                    MaxMovementSpeed = ccComponentData.MaxMovementSpeed
                };

                // Character transform
                RigidTransform transform = new RigidTransform
                {
                    pos = localTransform.Position,
                    rot = localTransform.Rotation
                };
                
                RigidTransform headTransform = new RigidTransform
                {
                    pos = transform.pos + math.mul(transform.rot, ccInternalData.HeadOffset),
                    rot = math.mul(transform.rot, quaternion.identity)
                };

                NativeList<StatefulCollisionEvent> currentFrameCollisionEvents = default;
                NativeList<StatefulTriggerEvent> currentFrameTriggerEvents = default;

                if (ccComponentData.RaiseCollisionEvents != 0)
                {
                    currentFrameCollisionEvents = new NativeList<StatefulCollisionEvent>(Allocator.Temp);
                }

                if (ccComponentData.RaiseTriggerEvents != 0)
                {
                    currentFrameTriggerEvents = new NativeList<StatefulTriggerEvent>(Allocator.Temp);
                }
                
                // Check unsupported hit
                if (ccInternalData.SupportedState == CharacterSupportState.Unsupported)
                    CheckUnsupported(ref headCollider, ref ccInternalData.UnsupportedVelocity, stepInput, headTransform);

                // Check support
                CheckSupport(ref collider, stepInput, transform,
                    out ccInternalData.SupportedState, out float3 surfaceNormal, out float3 surfaceVelocity,
                    currentFrameCollisionEvents);

                // User input
                HandleUserInput(ccComponentData, stepInput.Up, surfaceVelocity, ref ccInternalData, out var desiredVelocity, out var headVelocity);
                
                // Calculate actual velocity with respect to surface
                if (ccInternalData.SupportedState == CharacterSupportState.Supported)
                {
                    CalculateMovement(ccInternalData.CurrentRotationAngle, stepInput.Up, ccInternalData.IsJumping,
                        ccInternalData.Velocity.Linear, desiredVelocity, surfaceNormal, surfaceVelocity, out ccInternalData.Velocity.Linear);
                }
                else
                {
                    ccInternalData.Velocity.Linear = desiredVelocity;
                }
                
                // World collision + integrate
                CollideAndIntegrate(stepInput, ccComponentData.CharacterMass, ccComponentData.AffectsPhysicsBodies != 0,
                    ref collider,
                    ref transform,
                    ref ccInternalData.Velocity.Linear,
                    
                    ref headCollider,
                    ref headTransform,
                    ref headVelocity,
                    ccInternalData.HeadOffset,
                    
                    ref DeferredImpulseWriter,
                    currentFrameCollisionEvents, currentFrameTriggerEvents);

                // Update collision event status
                if (currentFrameCollisionEvents.IsCreated)
                {
                    UpdateCollisionEvents(currentFrameCollisionEvents, collisionEventBuffer);
                }

                if (currentFrameTriggerEvents.IsCreated)
                {
                    UpdateTriggerEvents(currentFrameTriggerEvents, triggerEventBuffer);
                }

                // Write back and orientation integration

                localTransform.Position = transform.pos;
                localTransform.Rotation = quaternion.AxisAngle(up, ccInternalData.CurrentRotationAngle);

                // Write back to chunk data
                {
                    chunkCCInternalData[i] = ccInternalData;
                    chunkLocalTransformData[i] = localTransform;
                }
            }

            DeferredImpulseWriter.EndForEachIndex();
        }

        private void HandleUserInput(
            HackerControllerComponentData ccComponentData, 
            float3 up, 
            float3 surfaceVelocity,
            ref HackerControllerInternalData ccInternalData,
            out float3 linearVelocity,
            out float3 headVelocity)
        {
            // Reset jumping state and unsupported velocity
            if (ccInternalData.SupportedState == CharacterSupportState.Supported)
            {
                ccInternalData.IsJumping = false;
                ccInternalData.UnsupportedVelocity = float3.zero;
            }

            // Movement and jumping
            bool shouldJump = false;
            float3 requestedMovementDirection = float3.zero;
            {
                float3 forward = math.forward(quaternion.identity);
                float3 right = math.cross(up, forward);

                float horizontal = ccInternalData.Input.Movement.x;
                float vertical = ccInternalData.Input.Movement.y;
                bool jumpRequested = ccInternalData.Input.Movement.z != 0;
                //ccInternalData.Input.Jumped = 0; // "consume" the event
                
                bool haveInput = (math.abs(horizontal) > float.Epsilon) || (math.abs(vertical) > float.Epsilon);
                if (haveInput)
                {
                    float3 localSpaceMovement = forward * vertical + right * horizontal;
                    float3 worldSpaceMovement = math.rotate(quaternion.AxisAngle(up, ccInternalData.CurrentRotationAngle), localSpaceMovement);
                    requestedMovementDirection = math.normalize(worldSpaceMovement);
                }
                shouldJump = jumpRequested && ccInternalData.SupportedState == CharacterSupportState.Supported;
            }

            // Turning
            {
                float horizontal = ccInternalData.Input.Looking.x;
                bool haveInput = (math.abs(horizontal) > float.Epsilon);
                if (haveInput)
                {
                    var userRotationSpeed = horizontal * ccComponentData.RotationSpeed;
                    ccInternalData.Velocity.Angular = -userRotationSpeed * up;
                    ccInternalData.CurrentRotationAngle += userRotationSpeed * DeltaTime;
                }
                else
                {
                    ccInternalData.Velocity.Angular = 0f;
                }
            }
            
            // Leaning
            {
                ccInternalData.CurrentLeanAngle += (ccInternalData.Input.LeanAngle - ccInternalData.CurrentLeanAngle) * DeltaTime * 1.5f;
            }
                
            // Crouching
            {
                ccInternalData.CurrentCrouch += (ccInternalData.Input.Crouch - ccInternalData.CurrentCrouch) * DeltaTime * 1.5f;
                    
                    
                if (ccInternalData.Input.Crouch > 0 || ccInternalData.Height < ccComponentData.CharacterHeight)
                    ccInternalData.Height = ccComponentData.CharacterHeight * (1f - .636f * ccInternalData.CurrentCrouch);
                else
                    ccInternalData.Height = ccComponentData.CharacterHeight;
            }
                    
            { // Head
                var prevHeadOffset = ccInternalData.HeadOffset;

                // Used for motion head angle
                //var leapZ = .1f * ccInternalData.Height * math.cos(ccInternalData.HeadRotation.x) * math.cos(ccInternalData.HeadRotation.z);
                //Debug.Log($"LeapZ {leapZ}");
            
                ccInternalData.HeadRotation = new float3(ccInternalData.CurrentCrouch, 0f, ccInternalData.CurrentLeanAngle);

                ccInternalData.HeadOffset = new float3(
                    1.5f * ccInternalData.Height * math.sin(ccInternalData.HeadRotation.z), 
                    ccInternalData.Height * math.cos(ccInternalData.HeadRotation.x) * math.cos(ccInternalData.HeadRotation.z), 
                    ccInternalData.Height * math.sin(ccInternalData.HeadRotation.x)
                );

                headVelocity = (ccInternalData.HeadOffset - prevHeadOffset) / DeltaTime;
                headVelocity += math.cross(-ccInternalData.Velocity.Angular, ccInternalData.HeadOffset);

                /*
                var rotVel = math.cross(-ccInternalData.Velocity.Angular, ccInternalData.HeadOffset);
                if (math.lengthsq(rotVel) > float.Epsilon)
                    Debug.Log($"HV {rotVel}");
                else
                    Debug.Log($"No rotation velocity {ccInternalData.Velocity.Angular} offset velocity {(prevHeadOffset - ccInternalData.HeadOffset) / DeltaTime}");
                */
            }

            // Apply input velocities
            {
                if (shouldJump) // TODO jump jet
                {
                    // Add jump speed to surface velocity and make character unsupported
                    ccInternalData.IsJumping = true;
                    ccInternalData.SupportedState = CharacterSupportState.Unsupported;
                    ccInternalData.UnsupportedVelocity = surfaceVelocity + ccComponentData.JumpUpwardsSpeed * up;
                }
                else if (ccInternalData.SupportedState != CharacterSupportState.Supported)
                {
                    // Apply gravity
                    ccInternalData.UnsupportedVelocity += ccComponentData.Gravity * DeltaTime;
                }
                // If unsupported then keep jump and surface momentum
                linearVelocity = requestedMovementDirection * ccComponentData.MovementSpeed +
                    (ccInternalData.SupportedState != CharacterSupportState.Supported ? ccInternalData.UnsupportedVelocity : float3.zero);
            }
        }

        private static void CalculateMovement(float currentRotationAngle, float3 up, bool isJumping,
            float3 currentVelocity, float3 desiredVelocity, float3 surfaceNormal, float3 surfaceVelocity, out float3 linearVelocity)
        {
            float3 forward = math.forward(quaternion.AxisAngle(up, currentRotationAngle));

            quaternion surfaceFrame;

            float3 binorm;
            {
                binorm = math.cross(forward, up);
                binorm = math.normalize(binorm);

                float3 tangent = math.cross(binorm, surfaceNormal);
                tangent = math.normalize(tangent);

                binorm = math.cross(tangent, surfaceNormal);
                binorm = math.normalize(binorm);

                surfaceFrame = new quaternion(new float3x3(binorm, tangent, surfaceNormal));
            }

            float3 relative = currentVelocity - surfaceVelocity;

            relative = math.rotate(math.inverse(surfaceFrame), relative);

            float3 diff;
            {
                float3 sideVec = math.cross(forward, up);
                float fwd = math.dot(desiredVelocity, forward);
                float side = math.dot(desiredVelocity, sideVec);
                float len = math.length(desiredVelocity);
                float3 desiredVelocitySF = new float3(-side, -fwd, 0.0f);
                desiredVelocitySF = math.normalizesafe(desiredVelocitySF, float3.zero);
                desiredVelocitySF *= len;
                diff = desiredVelocitySF - relative;
            }

            relative += diff;

            linearVelocity = math.rotate(surfaceFrame, relative) + surfaceVelocity +
                (isJumping ? math.dot(desiredVelocity, up) * up : float3.zero);
        }

        private static void UpdateTriggerEvents(NativeList<StatefulTriggerEvent> triggerEvents,
            DynamicBuffer<StatefulTriggerEvent> triggerEventBuffer)
        {
            var previousFrameTriggerEvents = new NativeList<StatefulTriggerEvent>(triggerEventBuffer.Length, Allocator.Temp);

            for (int i = 0; i < triggerEventBuffer.Length; i++)
            {
                var triggerEvent = triggerEventBuffer[i];
                if (triggerEvent.State != StatefulEventState.Exit)
                {
                    previousFrameTriggerEvents.Add(triggerEvent);
                }
            }

            var eventsWithState = new NativeList<StatefulTriggerEvent>(triggerEvents.Length, Allocator.Temp);

            StatefulSimulationEventBuffers<StatefulTriggerEvent>.GetStatefulEvents(previousFrameTriggerEvents, triggerEvents, eventsWithState);

            triggerEventBuffer.Clear();

            for (int i = 0; i < eventsWithState.Length; i++)
            {
                triggerEventBuffer.Add(eventsWithState[i]);
            }
        }

        private static void UpdateCollisionEvents(NativeList<StatefulCollisionEvent> collisionEvents,
            DynamicBuffer<StatefulCollisionEvent> collisionEventBuffer)
        {
            var previousFrameCollisionEvents = new NativeList<StatefulCollisionEvent>(collisionEventBuffer.Length, Allocator.Temp);

            for (int i = 0; i < collisionEventBuffer.Length; i++)
            {
                var collisionEvent = collisionEventBuffer[i];
                if (collisionEvent.State != StatefulEventState.Exit)
                {
                    previousFrameCollisionEvents.Add(collisionEvent);
                }
            }

            var eventsWithState = new NativeList<StatefulCollisionEvent>(collisionEvents.Length, Allocator.Temp);
            StatefulSimulationEventBuffers<StatefulCollisionEvent>.GetStatefulEvents(previousFrameCollisionEvents, collisionEvents, eventsWithState);

            collisionEventBuffer.Clear();
            for (int i = 0; i < eventsWithState.Length; i++)
            {
                collisionEventBuffer.Add(eventsWithState[i]);
            }
        }
    }

    [BurstCompile]
    private struct ApplyDeferredPhysicsUpdatesJob : IJob
    {
        // Chunks can be deallocated at this point
        [DeallocateOnJobCompletion] public NativeArray<ArchetypeChunk> Chunks;

        public NativeStream.Reader DeferredImpulseReader;

        public ComponentLookup<PhysicsVelocity> PhysicsVelocityData;
        [ReadOnly] public ComponentLookup<PhysicsMass> PhysicsMassData;

        [ReadOnly] public ComponentLookup<LocalTransform> LocalTransformData;

        public void Execute()
        {
            int index = 0;
            int maxIndex = DeferredImpulseReader.ForEachCount;
            DeferredImpulseReader.BeginForEachIndex(index++);
            while (DeferredImpulseReader.RemainingItemCount == 0 && index < maxIndex)
            {
                DeferredImpulseReader.BeginForEachIndex(index++);
            }

            while (DeferredImpulseReader.RemainingItemCount > 0)
            {
                // Read the data
                var impulse = DeferredImpulseReader.Read<DeferredCharacterControllerImpulse>();
                while (DeferredImpulseReader.RemainingItemCount == 0 && index < maxIndex)
                {
                    DeferredImpulseReader.BeginForEachIndex(index++);
                }

                PhysicsVelocity pv = PhysicsVelocityData[impulse.Entity];
                PhysicsMass pm = PhysicsMassData[impulse.Entity];

                LocalTransform t = LocalTransformData[impulse.Entity];


                // Don't apply on kinematic bodies
                if (pm.InverseMass > 0.0f)
                {
                    // Apply impulse

                    pv.ApplyImpulse(pm, t.Position, t.Rotation, impulse.Impulse, impulse.Point);

                    // Write back
                    PhysicsVelocityData[impulse.Entity] = pv;
                }
            }
        }
    }

    // override the behavior of CopyPhysicsVelocityToSmoothing
    [BurstCompile]
    private partial struct CopyVelocityToGraphicalSmoothingJob : IJobEntity
    {
        public void Execute(in HackerControllerInternalData ccInternalData, ref PhysicsGraphicalSmoothing smoothing)
        {
            smoothing.CurrentVelocity = ccInternalData.Velocity;
        }
    }

    EntityQuery m_CharacterControllersGroup;
    EntityQuery m_SmoothedCharacterControllersGroup;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        EntityQueryBuilder queryBuilder = new EntityQueryBuilder(Allocator.Temp)
            .WithAllRW<HackerControllerComponentData, HackerControllerInternalData>()
            .WithAllRW<LocalTransform>()
            .WithAll<PhysicsCollider>();

        m_CharacterControllersGroup = state.GetEntityQuery(queryBuilder);
        state.RequireForUpdate(m_CharacterControllersGroup);

        queryBuilder = new EntityQueryBuilder(Allocator.Temp)
            .WithAllRW<HackerControllerInternalData, PhysicsGraphicalSmoothing>();

        m_SmoothedCharacterControllersGroup = state.GetEntityQuery(queryBuilder);

        m_Handles = new CharacterControllerTypeHandles(ref state);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        m_Handles.Update(ref state);

        var chunks = m_CharacterControllersGroup.ToArchetypeChunkArray(Allocator.TempJob);
        var deferredImpulses = new NativeStream(chunks.Length, Allocator.TempJob);
        var physicsWorldSingleton = SystemAPI.GetSingleton<PhysicsWorldSingleton>();

        float dt = SystemAPI.Time.DeltaTime;
        var ccJob = new CharacterControllerJob
        {
            // Archetypes
            CharacterControllerComponentType = m_Handles.CharacterControllerComponentType,
            CharacterControllerInternalType = m_Handles.CharacterControllerInternalType,
            PhysicsColliderType = m_Handles.PhysicsColliderType,

            LocalTransformType = m_Handles.LocalTransformType,

            CollisionEventBufferType = m_Handles.CollisionEventBufferType,
            TriggerEventBufferType = m_Handles.TriggerEventBufferType,

            // Input
            DeltaTime = dt,
            PhysicsWorldSingleton = physicsWorldSingleton,
            DeferredImpulseWriter = deferredImpulses.AsWriter()
        };

        state.Dependency = ccJob.ScheduleParallel(m_CharacterControllersGroup, state.Dependency);

        var copyVelocitiesHandle = new CopyVelocityToGraphicalSmoothingJob().ScheduleParallel(m_SmoothedCharacterControllersGroup, state.Dependency);

        var applyJob = new ApplyDeferredPhysicsUpdatesJob()
        {
            Chunks = chunks,
            DeferredImpulseReader = deferredImpulses.AsReader(),
            PhysicsVelocityData = m_Handles.PhysicsVelocityData,
            PhysicsMassData = m_Handles.PhysicsMassData,

            LocalTransformData = m_Handles.LocalTransformData,
        };

        state.Dependency = applyJob.Schedule(state.Dependency);

        state.Dependency = deferredImpulses.Dispose(state.Dependency);

        state.Dependency = JobHandle.CombineDependencies(state.Dependency, copyVelocitiesHandle);
    }
}
