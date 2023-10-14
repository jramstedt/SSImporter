using SS.Resources;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;

namespace SS.System {
  [BurstCompile]
  [UpdateBefore(typeof(TriggerSystem))]
  [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
  public partial struct SchedulerSystem : ISystem {
    private EntityTypeHandle entityTypeHandle;
    private ComponentTypeHandle<ScheduleEvent> scheduleEventTypeHandle;

    private EntityQuery eventQuery;

    public void OnCreate(ref SystemState state) {
      state.RequireForUpdate<Level>();

      entityTypeHandle = state.GetEntityTypeHandle();
      scheduleEventTypeHandle = state.GetComponentTypeHandle<ScheduleEvent>();

      eventQuery = state.GetEntityQuery(new EntityQueryDesc {
        All = new ComponentType[] {
          ComponentType.ReadOnly<ScheduleEvent>(),
        }
      });
    }

    public void OnDestroy(ref SystemState state) { }

    public void OnUpdate(ref SystemState state) {
      var ecbSingleton = SystemAPI.GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>();
      var commandBuffer = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

      var level = SystemAPI.GetSingleton<Level>();

      entityTypeHandle.Update(ref state);
      scheduleEventTypeHandle.Update(ref state);

      var schedulerJob = new SchedulerJob {
        entityTypeHandle = entityTypeHandle,
        scheduleEventTypeHandle = scheduleEventTypeHandle,

        TimeData = SystemAPI.Time,
        ObjectInstancesBlobAsset = level.ObjectInstances,

        CommandBuffer = commandBuffer.AsParallelWriter()
      };

      state.Dependency = schedulerJob.ScheduleParallel(eventQuery, state.Dependency);
    }

    [BurstCompile]
    struct SchedulerJob : IJobChunk {
      [ReadOnly] public EntityTypeHandle entityTypeHandle;
      [ReadOnly] public ComponentTypeHandle<ScheduleEvent> scheduleEventTypeHandle;

      [ReadOnly] public TimeData TimeData;
      [ReadOnly] public BlobAssetReference<BlobArray<Entity>> ObjectInstancesBlobAsset;

      [WriteOnly] public EntityCommandBuffer.ParallelWriter CommandBuffer;

      public unsafe void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask) {
        var entities = chunk.GetNativeArray(entityTypeHandle);
        var scheduleEvents = chunk.GetNativeArray(ref scheduleEventTypeHandle);

        var timestamp = TimeUtils.SecondsToTimestamp(TimeData.ElapsedTime); // TODO player gametime

        for (int i = 0; i < chunk.Count; ++i) {
          var entity = entities[i];
          var scheduleEvent = scheduleEvents[i];

          if (ExpandTimestamp(scheduleEvent.Timestamp, timestamp) >= ExpandTimestamp(timestamp, timestamp)) continue;

          if (scheduleEvent.Type == EventType.Trap) {
            TrapScheduleEvent trapEvent = *(TrapScheduleEvent*)scheduleEvent.Data;

            // Debug.Log($"SchedulerJob EventType.Trap ets:{scheduleEvent.Timestamp} ts:{timestamp}");

            // Debug.Log($"SchedulerJob EventType.Trap t:{trapEvent.TargetObjectIndex} s:{trapEvent.SourceObjectIndex}");

            CommandBuffer.AddComponent<TriggerActivateTag>(unfilteredChunkIndex, ObjectInstancesBlobAsset.Value[trapEvent.TargetObjectIndex]);
            if (trapEvent.SourceObjectIndex != -1)
              CommandBuffer.AddComponent<TriggerActivateTag>(unfilteredChunkIndex, ObjectInstancesBlobAsset.Value[trapEvent.SourceObjectIndex]);
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
    }
  }
}
