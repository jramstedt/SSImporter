using SS.Resources;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Core;
using Unity.Entities;
using UnityEngine;
using EventType = SS.Resources.EventType;

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
    private EntityQuery animationQuery;

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
      
      animationQuery = new EntityQueryBuilder(Allocator.Temp)
        .WithAllRW<AnimationData>()
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
      
      var animationData = animationQuery.ToComponentDataListAsync<AnimationData>(Allocator.TempJob, state.Dependency, out var animationDataJobHandle);

      var schedulerJob = new SchedulerJob {
        EntityTypeHandle = entityTypeHandle,
        ScheduleEventTypeHandleRO = scheduleEventTypeHandleRO,

        TimeData = SystemAPI.Time,
        ObjectInstancesBlobAsset = level.ObjectInstances,
        ObjectInstanceLookupRO = objectInstanceLookupRO,
        DoorLookupRW = doorLookupRW,
        
        AnimationData = animationData.AsParallelReader(),

        CommandBuffer = commandBuffer.AsParallelWriter()
      };

      state.Dependency = schedulerJob.ScheduleParallel(eventQuery, animationDataJobHandle);
    }

    [BurstCompile]
    struct SchedulerJob : IJobChunk {
      [ReadOnly] public EntityTypeHandle EntityTypeHandle;
      [ReadOnly] public ComponentTypeHandle<ScheduleEvent> ScheduleEventTypeHandleRO;

      [ReadOnly] public TimeData TimeData;
      [ReadOnly] public BlobAssetReference<BlobArray<Entity>> ObjectInstancesBlobAsset;
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

          if (ExpandTimestamp(scheduleEvent.Timestamp, timestamp) >= ExpandTimestamp(timestamp, timestamp)) continue;

          // TODO handle other ScheduleEvent.Types

          if (scheduleEvent.Type == EventType.Door) {
            DoorScheduleEvent doorEvent = *(DoorScheduleEvent*)scheduleEvent.Data;
            
            Debug.Log($"SchedulerJob EventType.Door ets:{scheduleEvent.Timestamp} ts:{timestamp}");
            Debug.Log($"SchedulerJob EventType.Door t:{doorEvent.ObjectIndex} a:{doorEvent.AutoClose}");
            
            var targetEntity = ObjectInstancesBlobAsset.Value[doorEvent.ObjectIndex];
            var targetObjectInstance = ObjectInstanceLookupRO.GetRefRO(targetEntity).ValueRO;
            
            if (targetObjectInstance.Class == ObjectClass.DoorAndGrating) {
              if (!ObjectInstance.DoorAndGrating.AutoClose(targetObjectInstance, (byte)doorEvent.AutoClose)) continue;
              if (ObjectInstance.DoorAndGrating.IsReallyClosed(targetObjectInstance)) continue;

              var door = DoorLookupRW.GetRefRO(targetEntity).ValueRO;
              if (door.NeverAutoClose) continue;
              if (IsDoorClosing(door)) continue;
              
              CommandBuffer.AddComponent<ObjectUseTag>(unfilteredChunkIndex, targetEntity); // object_use(id, FALSE, OBJ_NULL);
            }
            
            // TODO PLAS_ANTENNA_TRIPLE
            
          } else if (scheduleEvent.Type == EventType.Trap) {
            TrapScheduleEvent trapEvent = *(TrapScheduleEvent*)scheduleEvent.Data;

            // Debug.Log($"SchedulerJob EventType.Trap ets:{scheduleEvent.Timestamp} ts:{timestamp}");
            // Debug.Log($"SchedulerJob EventType.Trap t:{trapEvent.TargetObjectIndex} s:{trapEvent.SourceObjectIndex}");
            
            DoMulti(trapEvent.TargetObjectIndex, unfilteredChunkIndex);
            if (trapEvent.SourceObjectIndex != -1)
              CommandBuffer.AddComponent<TriggerActivateTag>(unfilteredChunkIndex, ObjectInstancesBlobAsset.Value[trapEvent.SourceObjectIndex]); // trap_activate
          }

          CommandBuffer.DestroyEntity(unfilteredChunkIndex, entity);
        }
      }

      /// <summary>
      /// This hack is to handle timestamp overflow.
      /// </summary>
      private readonly ushort ExpandTimestamp(ushort timestamp, ushort gametime) {
        if (timestamp <= (gametime - (0xFFFF >> 1)))
          timestamp += 0xFFFF;
        else if (timestamp >= (gametime + (0xFFFF >> 1)))
          timestamp -= 0xFFFF;

        return timestamp;
      }

      private readonly bool IsDoorClosing(ObjectInstance.DoorAndGrating door) {
        foreach (var animationData in AnimationData)
          if (door.IsMoving(animationData, true)) return true;

        return false;
      }

      private void DoMulti(short objectIndex, int unfilteredChunkIndex) {
        if (objectIndex == 0) return;
        
        var targetEntity = ObjectInstancesBlobAsset.Value[objectIndex];
        var targetObjectInstance = ObjectInstanceLookupRO.GetRefRO(targetEntity).ValueRO;

        if (targetObjectInstance.Class == ObjectClass.Trigger) {
          CommandBuffer.AddComponent<TriggerActivateTag>(unfilteredChunkIndex, targetEntity);
        } else {
          if (targetObjectInstance.Class == ObjectClass.DoorAndGrating) {
            ref var door = ref DoorLookupRW.GetRefRW(targetEntity).ValueRW;
            door.Lock = 0;
            door.AccessLevel = 0;

            var otherEntity = ObjectInstancesBlobAsset.Value[door.OtherHalf];
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
