using SS.Resources;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Core;
using Unity.Entities;
using UnityEngine;
using EventType = SS.Resources.EventType;

// TODO Global schedule

namespace SS.System {
  [BurstCompile]
  [UpdateBefore(typeof(TriggerSystem))]
  [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
  public partial struct SchedulerSystem : ISystem {
    private EntityTypeHandle entityTypeHandle;
    private ComponentTypeHandle<ScheduleEvent> scheduleEventTypeHandleRO;
    private ComponentLookup<ObjectInstance> objectInstanceLookupRO;
    private ComponentLookup<ObjectInstance.DoorAndGrating> doorLookupRW;

    private EntityQuery eventQuery;

    public void OnCreate(ref SystemState state) {
      state.RequireForUpdate<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>();
      state.RequireForUpdate<Level>();

      entityTypeHandle = state.GetEntityTypeHandle();
      scheduleEventTypeHandleRO = state.GetComponentTypeHandle<ScheduleEvent>(true);
      objectInstanceLookupRO = state.GetComponentLookup<ObjectInstance>(true);
      doorLookupRW = state.GetComponentLookup<ObjectInstance.DoorAndGrating>();

      eventQuery = new EntityQueryBuilder(Allocator.Temp)
        .WithAll<ScheduleEvent>()
        .Build(ref state);
    }

    public void OnUpdate(ref SystemState state) {
      var ecbSingleton = SystemAPI.GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>();
      var commandBuffer = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

      var level = SystemAPI.GetSingleton<Level>();
      
      entityTypeHandle.Update(ref state);
      scheduleEventTypeHandleRO.Update(ref state);
      objectInstanceLookupRO.Update(ref state);
      doorLookupRW.Update(ref state);
      
      var schedulerJob = new SchedulerJob {
        EntityTypeHandle = entityTypeHandle,
        ScheduleEventTypeHandleRO = scheduleEventTypeHandleRO,

        TimeData = SystemAPI.Time,
        ObjectInstancesRO = level.ObjectInstances.AsReadOnly(),
        ObjectInstanceLookupRO = objectInstanceLookupRO,
        DoorLookupRW = doorLookupRW,
        
        AnimationData = level.Animations.AsParallelReader(),

        CommandBuffer = commandBuffer.AsParallelWriter()
      };

      state.Dependency = schedulerJob.ScheduleParallelByRef(eventQuery, state.Dependency);
      
      // Debug.Log($"Scheduler run ts:{TimeUtils.SecondsToTimestamp(SystemAPI.Time.ElapsedTime)}");
    }

    [BurstCompile]
    private struct SchedulerJob : IJobChunk {
      [ReadOnly] public EntityTypeHandle EntityTypeHandle;
      [ReadOnly] public ComponentTypeHandle<ScheduleEvent> ScheduleEventTypeHandleRO;

      [ReadOnly] public TimeData TimeData;
      [ReadOnly] public NativeArray<Entity>.ReadOnly ObjectInstancesRO;
      [ReadOnly] public ComponentLookup<ObjectInstance> ObjectInstanceLookupRO;
      [NativeDisableContainerSafetyRestriction] public ComponentLookup<ObjectInstance.DoorAndGrating> DoorLookupRW;

      [ReadOnly] public NativeArray<AnimationData>.ReadOnly AnimationData;

      [WriteOnly] public EntityCommandBuffer.ParallelWriter CommandBuffer;

      public unsafe void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask) {
        var entities = chunk.GetNativeArray(EntityTypeHandle);
        var scheduleEvents = chunk.GetNativeArray(ref ScheduleEventTypeHandleRO);

        var timestamp = TimeUtils.SecondsToTimestamp(TimeData.ElapsedTime); // TODO player gametime
        
        for (int i = 0; i < chunk.Count; ++i) {
          var entity = entities[i];
          var scheduleEvent = scheduleEvents[i];
          
          if (TimeUtils.ExpandTimestamp(scheduleEvent.Timestamp, timestamp) >= TimeUtils.ExpandTimestamp(timestamp, timestamp))
            continue;

          // TODO handle other ScheduleEvent.Types

          if (scheduleEvent.Type == EventType.Door) {
            var doorEvent = *(DoorScheduleEvent*)scheduleEvent.Data;
            
            // Debug.Log($"SchedulerJob EventType.Door ets:{scheduleEvent.Timestamp} ts:{timestamp}");
            // Debug.Log($"SchedulerJob EventType.Door t:{doorEvent.ObjectIndex} a:{doorEvent.AutoClose}");
            
            var targetEntity = ObjectInstancesRO[doorEvent.ObjectIndex];
            var targetObjectInstance = ObjectInstanceLookupRO.GetRefRO(targetEntity).ValueRO;
            
            if (targetObjectInstance.Class == ObjectClass.DoorAndGrating) {
              if (!ObjectInstance.DoorAndGrating.AutoClose(targetObjectInstance, (byte)doorEvent.AutoClose)) goto consumed;
              if (ObjectInstance.DoorAndGrating.IsReallyClosed(targetObjectInstance)) goto consumed;

              var door = DoorLookupRW.GetRefRO(targetEntity).ValueRO;
              if (door.NeverAutoClose) goto consumed;
              if (door.IsMoving(AnimationData, true)) goto consumed;
              
              CommandBuffer.AddComponent<ObjectUseTag>(unfilteredChunkIndex, targetEntity); // object_use(id, FALSE, OBJ_NULL);
            }
            
            // TODO PLAS_ANTENNA_TRIPLE
            
          } else if (scheduleEvent.Type == EventType.Trap) {
            var trapEvent = *(TrapScheduleEvent*)scheduleEvent.Data;

            Debug.Log($"SchedulerJob EventType.Trap ets:{scheduleEvent.Timestamp} ts:{timestamp} s:{trapEvent.SourceObjectIndex}");
            // Debug.Log($"SchedulerJob EventType.Trap ets:{scheduleEvent.Timestamp} t:{trapEvent.TargetObjectIndex}");
            
            DoMulti(trapEvent.TargetObjectIndex, unfilteredChunkIndex);
            if (trapEvent.SourceObjectIndex != -1)
              CommandBuffer.SetComponentEnabled<TriggerActivateTag>(unfilteredChunkIndex, ObjectInstancesRO[trapEvent.SourceObjectIndex], true); // trap_activate
          } else {
            Debug.LogWarning($"SchedulerJob ${scheduleEvent.Type} ets:{scheduleEvent.Timestamp} ts:{timestamp} UHANDLED");
          }

          consumed:
          CommandBuffer.DestroyEntity(unfilteredChunkIndex, entity);
        }
      }
      
      private void DoMulti(short objectIndex, int unfilteredChunkIndex) {
        if (objectIndex == 0) return;
        
        var targetEntity = ObjectInstancesRO[objectIndex];
        var targetObjectInstance = ObjectInstanceLookupRO.GetRefRO(targetEntity).ValueRO;

        if (targetObjectInstance.Class == ObjectClass.Trigger) {
          CommandBuffer.SetComponentEnabled<TriggerActivateTag>(unfilteredChunkIndex, targetEntity, true);
        } else {
          if (targetObjectInstance.Class == ObjectClass.DoorAndGrating) {
            ref var door = ref DoorLookupRW.GetRefRW(targetEntity).ValueRW;
            door.Lock = 0;
            door.AccessLevel = 0;

            var otherEntity = ObjectInstancesRO[door.OtherHalf];
            if (otherEntity != Entity.Null) {
              ref var otherDoor = ref DoorLookupRW.GetRefRW(otherEntity).ValueRW;
              otherDoor.Lock = 0;
              otherDoor.AccessLevel = 0;
            }
          }
          
          CommandBuffer.AddComponent<ObjectUseTag>(unfilteredChunkIndex, targetEntity); // object_use(id, FALSE, OBJ_NULL);
        }
      }
    }
  }
}
