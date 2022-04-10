using Unity.Burst;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using UnityEngine;

namespace SS.System {
  [UpdateInGroup (typeof(InitializationSystemGroup)), UpdateAfter(typeof(UpdateWorldTimeSystem))]
  public sealed class RootTriggerSystem : SystemBase {
    private const double NextContinuousSeconds = 5.0;

    private EntityQuery triggerQuery;

    private double NextContinuousTrigger = 0.0;
    private bool LevelEnterProcessed = false;

    protected override void OnCreate() {
      base.OnCreate();

      triggerQuery = GetEntityQuery(new EntityQueryDesc {
        All = new ComponentType[] {
          ComponentType.ReadOnly<ObjectInstance>(),
          ComponentType.ReadOnly<ObjectInstance.Trigger>()
        }
      });
    }

    protected override void OnStartRunning() {
      base.OnStartRunning();
    }

    protected override void OnStopRunning() {
      base.OnStopRunning();

      LevelEnterProcessed = false;
    }

    protected override void OnUpdate() {
      var ecbSystem = World.GetExistingSystem<EndInitializationEntityCommandBufferSystem>();
      var commandBuffer = ecbSystem.CreateCommandBuffer();

      // TODO player gametime 

      var triggerContinuous = Time.ElapsedTime > NextContinuousTrigger;
      var triggerLevelEnter = !LevelEnterProcessed;

      var triggerJob = new ContinuousTriggerJob {
        entityTypeHandle = GetEntityTypeHandle(),
        instanceTypeHandle = GetComponentTypeHandle<ObjectInstance>(),
        triggerTypeHandle = GetComponentTypeHandle<ObjectInstance.Trigger>(),
        CommandBuffer = commandBuffer.AsParallelWriter(),

        TrggerContinuous = triggerContinuous,
        TriggerLevelEnter = triggerLevelEnter
      };

      var trigger = triggerJob.ScheduleParallel(triggerQuery, dependsOn: Dependency);
      Dependency = trigger;
      ecbSystem.AddJobHandleForProducer(trigger);

      if (triggerLevelEnter) LevelEnterProcessed = true;
      if (triggerContinuous) NextContinuousTrigger = Time.ElapsedTime + NextContinuousSeconds;
    }

    struct ContinuousTriggerJob : IJobEntityBatch {
      [ReadOnly] public EntityTypeHandle entityTypeHandle;
      [ReadOnly] public ComponentTypeHandle<ObjectInstance> instanceTypeHandle;
      [ReadOnly] public ComponentTypeHandle<ObjectInstance.Trigger> triggerTypeHandle;
      [WriteOnly] public EntityCommandBuffer.ParallelWriter CommandBuffer;
      public bool TrggerContinuous;
      public bool TriggerLevelEnter;

      [BurstCompile]
      public void Execute(ArchetypeChunk batchInChunk, int batchIndex) {
        var entities = batchInChunk.GetNativeArray(entityTypeHandle);
        var instances = batchInChunk.GetNativeArray(instanceTypeHandle);
        var triggers = batchInChunk.GetNativeArray(triggerTypeHandle);

        for (int i = 0; i < batchInChunk.Count; ++i) {
          var entity = entities[i];
          var instance = instances[i];
          var trigger = triggers[i];

          if (trigger.Link.ObjectIndex == 0) continue;

          if (TriggerLevelEnter && instance.SubClass == 0 && instance.Info.Type == 8) { // Level Entry
            Debug.Log($"RootTriggerSystem Level Entry e:{entity.Index}");
            CommandBuffer.AddComponent<TriggerActivateTag>(batchIndex, entity);
          } else if (TrggerContinuous && instance.SubClass == 0 && instance.Info.Type == 9) { // Continuous
            Debug.Log($"RootTriggerSystem Continuous e:{entity.Index}");
            CommandBuffer.AddComponent<TriggerActivateTag>(batchIndex, entity);
          }
        }
      }
    }
  }
}
