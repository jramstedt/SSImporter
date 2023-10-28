using SS.Resources;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace SS.System {
  [BurstCompile]
  [UpdateAfter(typeof(UpdateWorldTimeSystem))]
  [UpdateInGroup(typeof(InitializationSystemGroup))]
  public partial struct RootTriggerSystem : ISystem, ISystemStartStop {
    private const double NextContinuousSeconds = 5.0;

    private double NextContinuousTrigger;
    private bool LevelEnterProcessed;

    private EntityTypeHandle entityTypeHandle;
    private ComponentTypeHandle<ObjectInstance> instanceTypeHandleRO;
    private ComponentTypeHandle<ObjectInstance.Trigger> triggerTypeHandleRO;

    private EntityQuery triggerQuery;

    public void OnCreate(ref SystemState state) {
      NextContinuousTrigger = 0.0;
      LevelEnterProcessed = false;

      state.RequireForUpdate<Level>();

      entityTypeHandle = state.GetEntityTypeHandle();
      instanceTypeHandleRO = state.GetComponentTypeHandle<ObjectInstance>(true);
      triggerTypeHandleRO = state.GetComponentTypeHandle<ObjectInstance.Trigger>(true);

      triggerQuery = new EntityQueryBuilder(Allocator.Temp)
        .WithAll<ObjectInstance, ObjectInstance.Trigger>()
        .Build(ref state);

      // TODO listen Level singleton add and remove
    }

    public readonly void OnDestroy(ref SystemState state) { }

    public readonly void OnStartRunning(ref SystemState state) { }

    public readonly void OnStopRunning(ref SystemState state) {
      // LevelEnterProcessed = false;
    }

    public void OnUpdate(ref SystemState state) {
      var ecbSingleton = SystemAPI.GetSingleton<EndInitializationEntityCommandBufferSystem.Singleton>();
      var commandBuffer = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

      entityTypeHandle.Update(ref state);
      instanceTypeHandleRO.Update(ref state);
      triggerTypeHandleRO.Update(ref state);

      // TODO player gametime 

      var triggerContinuous = SystemAPI.Time.ElapsedTime > NextContinuousTrigger;
      var triggerLevelEnter = !LevelEnterProcessed;

      var triggerJob = new TriggerJob {
        entityTypeHandle = entityTypeHandle,
        instanceTypeHandleRO = instanceTypeHandleRO,
        triggerTypeHandleRO = triggerTypeHandleRO,
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
      [ReadOnly] public ComponentTypeHandle<ObjectInstance> instanceTypeHandleRO;
      [ReadOnly] public ComponentTypeHandle<ObjectInstance.Trigger> triggerTypeHandleRO;
      [WriteOnly] public EntityCommandBuffer.ParallelWriter CommandBuffer;

      public bool TrggerContinuous;
      public bool TriggerLevelEnter;

      public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask) {
        var entities = chunk.GetNativeArray(entityTypeHandle);
        var instances = chunk.GetNativeArray(ref instanceTypeHandleRO);
        var triggers = chunk.GetNativeArray(ref triggerTypeHandleRO);

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
