using SS.Resources;
using Unity.Burst;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using UnityEngine;

namespace SS.System {
  [UpdateInGroup(typeof(SimulationSystemGroup))]
  public sealed class SchedulerSystem : SystemBase {
    private EntityQuery eventQuery;

    protected override void OnCreate() {
      base.OnCreate();

      eventQuery = GetEntityQuery(new EntityQueryDesc {
        All = new ComponentType[] {
          ComponentType.ReadOnly<ScheduleEvent>(),
        }
      });
    }

    protected override void OnUpdate() {
      var ecbSystem = World.GetExistingSystem<EndSimulationEntityCommandBufferSystem>();
      var commandBuffer = ecbSystem.CreateCommandBuffer();

      var level = GetSingleton<Level>();

      var schedulerJob = new SchedulerJob {
        entityTypeHandle = GetEntityTypeHandle(),
        scheduleEventTypeHandle = GetComponentTypeHandle<ScheduleEvent>(),

        TimeData = Time,
        ObjectInstancesBlobAsset = level.ObjectInstances,

        CommandBuffer = commandBuffer.AsParallelWriter()
      };

      var trigger = schedulerJob.ScheduleParallel(eventQuery, dependsOn: Dependency);
      Dependency = trigger;
      trigger.Complete();
    }

    [BurstCompile]
    struct SchedulerJob : IJobEntityBatch {
      [ReadOnly] public EntityTypeHandle entityTypeHandle;
      [ReadOnly] public ComponentTypeHandle<ScheduleEvent> scheduleEventTypeHandle;

      [ReadOnly] public TimeData TimeData;
      [ReadOnly] public BlobAssetReference<BlobArray<Entity>> ObjectInstancesBlobAsset;

      [WriteOnly] public EntityCommandBuffer.ParallelWriter CommandBuffer;

      public unsafe void Execute(ArchetypeChunk batchInChunk, int batchIndex) {
        var entities = batchInChunk.GetNativeArray(entityTypeHandle);
        var scheduleEvents = batchInChunk.GetNativeArray(scheduleEventTypeHandle);

        var timestamp = TimeUtils.SecondsToTimestamp(TimeData.ElapsedTime); // TODO player gametime

        for (int i = 0; i < batchInChunk.Count; ++i) {
          var entity = entities[i];
          var scheduleEvent = scheduleEvents[i];

          if (expandTimestamp(scheduleEvent.Timestamp, timestamp) >= expandTimestamp(timestamp, timestamp)) continue;

          if (scheduleEvent.Type == EventType.Trap) {
            TrapScheduleEvent trapEvent = *(TrapScheduleEvent*)scheduleEvent.Data;

            Debug.Log($"SchedulerJob EventType.Trap ets:{scheduleEvent.Timestamp} ts:{timestamp}");

            // Debug.Log($"SchedulerJob EventType.Trap t:{trapEvent.TargetObjectIndex} s:{trapEvent.SourceObjectIndex}");

            CommandBuffer.AddComponent<TriggerActivateTag>(batchIndex, ObjectInstancesBlobAsset.Value[trapEvent.TargetObjectIndex]);
            if (trapEvent.SourceObjectIndex != -1)
              CommandBuffer.AddComponent<TriggerActivateTag>(batchIndex, ObjectInstancesBlobAsset.Value[trapEvent.SourceObjectIndex]);
          }

          CommandBuffer.DestroyEntity(batchIndex, entity);
        }
      }

      private ushort expandTimestamp(ushort timestamp, ushort gametime) {
        if (timestamp <= (gametime - (0xFFFF >> 1)))
          timestamp += 0xFFFF;
        else if (timestamp >= (gametime + (0xFFFF >> 1)))
          timestamp -= 0xFFFF;

        return timestamp;
      }
    }
  }
}
