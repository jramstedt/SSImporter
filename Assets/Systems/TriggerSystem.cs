using SS.Resources;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Core;
using Unity.Entities;
using static SS.ObjectInstance;

namespace SS.System {
  [UpdateInGroup (typeof(SimulationSystemGroup))]
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
      var ecbSystem = World.GetExistingSystem<EndSimulationEntityCommandBufferSystem>();
      var commandBuffer = ecbSystem.CreateCommandBuffer();

      var triggerJob = new TriggerJob {
        entityTypeHandle = GetEntityTypeHandle(),
        instanceTypeHandle = GetComponentTypeHandle<ObjectInstance>(),
        triggerTypeHandle = GetComponentTypeHandle<ObjectInstance.Trigger>(),
        CommandBuffer = commandBuffer.AsParallelWriter(),
        map = GetSingleton<Level>()
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
      [ReadOnly] public Level map;

      [NativeDisableContainerSafetyRestriction]
      public ComponentDataFromEntity<ObjectInstance.Trigger> TriggerFromEntity;
      
      public void Execute(ArchetypeChunk batchInChunk, int batchIndex) {
        var entities = batchInChunk.GetNativeArray(entityTypeHandle);
        var instances = batchInChunk.GetNativeArray(instanceTypeHandle);
        var triggers = batchInChunk.GetNativeArray(triggerTypeHandle);

        for (int i = 0; i < batchInChunk.Count; ++i) {
          var entity = entities[i];
          var instance = instances[i];
          var trigger = triggers[i];

          // Activate(entity, )

          CommandBuffer.RemoveComponent<TriggerActivateTag>(batchIndex, entity);
        }
      }

      private bool Activate(in Entity entity, int batchIndex, out bool message) {
        message = false;

        

        return false;
      }

      private void processTrigger(in Entity entity, int batchIndex) {
        var trigger = TriggerFromEntity[entity];

        
        if (trigger.DestroyCount > 0) {
          if (--trigger.DestroyCount == 0)
            CommandBuffer.DestroyEntity(batchIndex, entity);
        }

        TriggerFromEntity[entity] = trigger;
      }
    }
  }

  public struct TriggerActivateTag : IComponentData { }
}
