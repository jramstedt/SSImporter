using Unity.Burst;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;

namespace SS.System {
  public sealed class TriggerSystem : SystemBase {
    private const double NextContinuousSeconds = 5.0;

    private EntityQuery triggerQuery;

    protected override void OnCreate() {
      base.OnCreate();

      triggerQuery = GetEntityQuery(new EntityQueryDesc {
        All = new ComponentType[] {
          ComponentType.ReadOnly<ObjectInstance>(),
          ComponentType.ReadOnly<ObjectInstance.Trigger>(),
          ComponentType.ReadOnly<TriggerActivateTag>()
        }
      });
    }

    protected override void OnUpdate() {
      var triggerJob = new TriggerJob {
        entityTypeHandle = GetEntityTypeHandle(),
        instanceTypeHandle = GetComponentTypeHandle<ObjectInstance>(),
        triggerTypeHandle = GetComponentTypeHandle<ObjectInstance.Trigger>()
      };

      var trigger = triggerJob.ScheduleParallel(triggerQuery, dependsOn: Dependency);
      Dependency = trigger;
      trigger.Complete();
    }

    struct TriggerJob : IJobEntityBatch {
      [ReadOnly] public EntityTypeHandle entityTypeHandle;
      [ReadOnly] public ComponentTypeHandle<ObjectInstance> instanceTypeHandle;
      [ReadOnly] public ComponentTypeHandle<ObjectInstance.Trigger> triggerTypeHandle;

      [BurstCompile]
      public void Execute(ArchetypeChunk batchInChunk, int batchIndex) {
        var entities = batchInChunk.GetNativeArray(entityTypeHandle);
        var instances = batchInChunk.GetNativeArray(instanceTypeHandle);
        var triggers = batchInChunk.GetNativeArray(triggerTypeHandle);

        for (int i = 0; i < batchInChunk.Count; ++i) {
          var entity = entities[i];
          var instance = instances[i];
          var trigger = triggers[i];

          // Activate(entity, )
        }
      }

      private bool Activate(Entity entity, out bool message) {
        message = false;

        return false;
      }
    }
  }

  public struct TriggerActivateTag : IComponentData { }
}
