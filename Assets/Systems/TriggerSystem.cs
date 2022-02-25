using SS.Resources;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Core;
using Unity.Entities;
using static SS.ObjectInstance;

namespace SS.System {
  [UpdateInGroup (typeof(SimulationSystemGroup)), UpdateAfter(typeof(UpdateWorldTimeSystem))]
  public sealed class TriggerSystem : SystemBase {
    private const double NextContinuousSeconds = 5.0;

    private EntityQuery triggerQuery;
    private EntityArchetype triggerEventArchetype;

    protected override void OnCreate() {
      base.OnCreate();

      triggerQuery = GetEntityQuery(new EntityQueryDesc {
        All = new ComponentType[] {
          ComponentType.ReadOnly<ObjectInstance>(),
          ComponentType.ReadOnly<ObjectInstance.Trigger>(),
          ComponentType.ReadOnly<TriggerActivateTag>()
        }
      });

      triggerEventArchetype = World.EntityManager.CreateArchetype(
        typeof(ScheduleEvent)
      );
    }

    protected override void OnUpdate() {
      var ecbSystem = World.GetExistingSystem<EndSimulationEntityCommandBufferSystem>();
      var commandBuffer = ecbSystem.CreateCommandBuffer();

      var level = GetSingleton<Level>();

      var triggerJob = new TriggerJob {
        entityTypeHandle = GetEntityTypeHandle(),
        instanceTypeHandle = GetComponentTypeHandle<ObjectInstance>(),
        triggerTypeHandle = GetComponentTypeHandle<ObjectInstance.Trigger>(),
        CommandBuffer = commandBuffer.AsParallelWriter(),
        ObjectInstancesBlobAsset = level.ObjectInstances,

        InstanceFromEntity = GetComponentDataFromEntity<ObjectInstance>(),
        TriggerFromEntity = GetComponentDataFromEntity<ObjectInstance.Trigger>(),

        triggerEventArchetype = triggerEventArchetype,
        timeData = World.Time
      };

      var trigger = triggerJob.ScheduleParallel(triggerQuery, dependsOn: Dependency);
      Dependency = trigger;
      trigger.Complete();
    }

    [BurstCompile]
    struct TriggerJob : IJobEntityBatch {
      [ReadOnly] public EntityTypeHandle entityTypeHandle;
      [ReadOnly] public ComponentTypeHandle<ObjectInstance> instanceTypeHandle;
      [ReadOnly] public ComponentTypeHandle<ObjectInstance.Trigger> triggerTypeHandle;
      [WriteOnly] public EntityCommandBuffer.ParallelWriter CommandBuffer;
      [ReadOnly] public BlobAssetReference<BlobArray<Entity>> ObjectInstancesBlobAsset;

      [NativeDisableContainerSafetyRestriction] public ComponentDataFromEntity<ObjectInstance> InstanceFromEntity;
      [NativeDisableContainerSafetyRestriction] public ComponentDataFromEntity<ObjectInstance.Trigger> TriggerFromEntity;

      [ReadOnly] public TimeData timeData;
      [ReadOnly] public EntityArchetype triggerEventArchetype;
      
      public void Execute(ArchetypeChunk batchInChunk, int batchIndex) {
        var entities = batchInChunk.GetNativeArray(entityTypeHandle);
        var instances = batchInChunk.GetNativeArray(instanceTypeHandle);
        var triggers = batchInChunk.GetNativeArray(triggerTypeHandle);

        for (int i = 0; i < batchInChunk.Count; ++i) {
          var entity = entities[i];
          var instance = instances[i];
          var trigger = triggers[i];

          Activate(entity, batchIndex, out bool message);

          CommandBuffer.RemoveComponent<TriggerActivateTag>(batchIndex, entity);
        }
      }

      private bool Activate(in Entity entity, int batchIndex, out bool message) {
        message = false;

        if (TriggerFromEntity.HasComponent(entity)) {
          var instance = InstanceFromEntity[entity];
          var trigger = TriggerFromEntity[entity];

          int comparator = instance.Info.Type switch {
            0 => 0, // DEATHWATCH_TRIGGER_TYPE
            5 => 0, // AREA_ENTRY_TRIGGER_TYPE
            6 => 0, // AREA_CONTINUOUS_TRIGGER_TYPE
            _ => trigger.Comparator
          };

          if (comparatorCheck(comparator, entity, out byte specialCode))
            processTrigger(entity, batchIndex); // TODO Activate actually

          return true;
        } // else fixture
  
        return false;
      }

      private bool comparatorCheck(int comparator, in Entity entity, out byte specialCode) {
        specialCode = 0;
        return true;
      }

      private void processTrigger(in Entity entity, int batchIndex) {
        if (!TriggerFromEntity.HasComponent(entity)) return;

        var trigger = TriggerFromEntity[entity];
        
        if (trigger.ActionType == ActionType.Propagate) {
          timedMulti(trigger.ActionParam1, batchIndex);
          timedMulti(trigger.ActionParam2, batchIndex);
          timedMulti(trigger.ActionParam3, batchIndex);
          timedMulti(trigger.ActionParam4, batchIndex);
        }
        
        if (trigger.DestroyCount > 0) {
          if (--trigger.DestroyCount == 0)
            CommandBuffer.DestroyEntity(batchIndex, entity);
        }

        TriggerFromEntity[entity] = trigger;
      }

      private unsafe void timedMulti(int param, int batchIndex) {
        int timeUnits = param >> 16; // 0.1 seconds per unit
        if (timeUnits != 0) {
          var entity = CommandBuffer.CreateEntity(batchIndex, triggerEventArchetype);

          var scheduleEvent = new ScheduleEvent {
            Timestamp = (ushort)(ScheduleEvent.TicksToTimestamp(/* gametime */ (timeUnits * 280) / 10 ) + 1), // TODO FIXME
            Type = EventType.Trap
          };
          *((TrapScheduleEvent*)scheduleEvent.Data) = new TrapScheduleEvent {
            TargetObjectIndex = (short)(param & 0xFFF),
            SourceObjectIndex = -1
          };

          CommandBuffer.SetComponent(batchIndex, entity, scheduleEvent);
        } else {
          multi((short)(param & 0xFFF));
        }
      }

      private void multi(short objectIndex) {
        if (objectIndex == 0) return;

        var entity = ObjectInstancesBlobAsset.Value[objectIndex];
        if (TriggerFromEntity.HasComponent(entity)) {
          Activate(entity, objectIndex, out bool message);
        } else {
          // TODO
          // if door then unlock it
          // object use
        }
      }

/*
      private short questDataParse(short qdata) {
        short contents = (short)(qdata & 0xFFF);
        if ((qdata & 0x1000) == 0x1000) {
          return 0; // TODO shodan values
        } else if ((qdata & 0x2000) == 0x2000) {
          return 0; // QUESTBIT_GET(contents)
        } else {
          return contents;
        }
      }
*/
    }
  }

  public struct TriggerActivateTag : IComponentData { }
}
