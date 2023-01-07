using SS.Resources;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

#pragma warning disable CS0282

namespace SS.System {
  [BurstCompile]
  [UpdateAfter(typeof(UpdateWorldTimeSystem))]
  [UpdateInGroup(typeof(InitializationSystemGroup))]
  public partial struct RootTriggerSystem : ISystem, ISystemStartStop {
    private const double NextContinuousSeconds = 5.0;

    private double NextContinuousTrigger;
    private bool LevelEnterProcessed;

    private EntityTypeHandle entityTypeHandle;
    private ComponentTypeHandle<ObjectInstance> instanceTypeHandle;
    private ComponentTypeHandle<ObjectInstance.Trigger> triggerTypeHandle;

    private EntityQuery triggerQuery;

    public void OnCreate(ref SystemState state) {
      NextContinuousTrigger = 0.0;
      LevelEnterProcessed = false;

      state.RequireForUpdate<Level>();

      entityTypeHandle = state.GetEntityTypeHandle();
      instanceTypeHandle = state.GetComponentTypeHandle<ObjectInstance>();
      triggerTypeHandle = state.GetComponentTypeHandle<ObjectInstance.Trigger>();

      triggerQuery = state.GetEntityQuery(new EntityQueryDesc {
        All = new ComponentType[] {
          ComponentType.ReadOnly<ObjectInstance>(),
          ComponentType.ReadOnly<ObjectInstance.Trigger>()
        }
      });

      // TODO listen Level singleton add and remove
    }

    public void OnDestroy(ref SystemState state) { }

    public void OnStartRunning(ref SystemState state) { }

    public void OnStopRunning(ref SystemState state) {
      // LevelEnterProcessed = false;
    }

    public void OnUpdate(ref SystemState state) {
      var ecbSingleton = SystemAPI.GetSingleton<EndInitializationEntityCommandBufferSystem.Singleton>();
      var commandBuffer = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

      entityTypeHandle.Update(ref state);
      instanceTypeHandle.Update(ref state);
      triggerTypeHandle.Update(ref state);

      // TODO player gametime 

      var triggerContinuous = SystemAPI.Time.ElapsedTime > NextContinuousTrigger;
      var triggerLevelEnter = !LevelEnterProcessed;

      var triggerJob = new TriggerJob {
        entityTypeHandle = entityTypeHandle,
        instanceTypeHandle = instanceTypeHandle,
        triggerTypeHandle = triggerTypeHandle,
        CommandBuffer = commandBuffer.AsParallelWriter(),

        TrggerContinuous = triggerContinuous,
        TriggerLevelEnter = triggerLevelEnter
      };

      state.Dependency = triggerJob.ScheduleParallel(triggerQuery, state.Dependency);

      if (triggerLevelEnter) LevelEnterProcessed = true;
      if (triggerContinuous) NextContinuousTrigger = SystemAPI.Time.ElapsedTime + NextContinuousSeconds;
    }

    [BurstCompile]
    struct TriggerJob : IJobChunk {
      [ReadOnly] public EntityTypeHandle entityTypeHandle;
      [ReadOnly] public ComponentTypeHandle<ObjectInstance> instanceTypeHandle;
      [ReadOnly] public ComponentTypeHandle<ObjectInstance.Trigger> triggerTypeHandle;
      [WriteOnly] public EntityCommandBuffer.ParallelWriter CommandBuffer;

      public bool TrggerContinuous;
      public bool TriggerLevelEnter;

      public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask) {
        var entities = chunk.GetNativeArray(entityTypeHandle);
        var instances = chunk.GetNativeArray(ref instanceTypeHandle);
        var triggers = chunk.GetNativeArray(ref triggerTypeHandle);

        for (int i = 0; i < chunk.Count; ++i) {
          var entity = entities[i];
          var instance = instances[i];
          var trigger = triggers[i];

          if (trigger.Link.ObjectIndex == 0) continue;

          if (TriggerLevelEnter && instance.SubClass == 0 && instance.Info.Type == 8) { // Level Entry
            Debug.Log($"RootTriggerSystem Level Entry e:{entity.Index}");
            CommandBuffer.AddComponent<TriggerActivateTag>(unfilteredChunkIndex, entity);
          } else if (TrggerContinuous && instance.SubClass == 0 && instance.Info.Type == 9) { // Continuous
            Debug.Log($"RootTriggerSystem Continuous e:{entity.Index}");
            CommandBuffer.AddComponent<TriggerActivateTag>(unfilteredChunkIndex, entity);
          }
        }
      }
    }
  }
}
