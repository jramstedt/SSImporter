using SS.Resources;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace SS.System {
  [UpdateBefore(typeof(ObjectUseSystem))]
  [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
  public partial struct TriggerSystem : ISystem, ISystemStartStop {
    private const double NextContinuousSeconds = 5.0;
    
    private double NextContinuousTrigger;
    private bool LevelEnterProcessed;

    private EntityTypeHandle entityTypeHandle;
    private ComponentTypeHandle<ObjectInstance> instanceTypeHandleRO;
    private ComponentTypeHandle<TriggerActivateTag> activateTypeHandleRW;

    private ComponentLookup<MapElement> mapElementLookup;
    private ComponentLookup<ObjectInstance> instanceLookup;
    private ComponentLookup<ObjectInstance.Trigger> triggerLookup;
    private ComponentLookup<ObjectInstance.Interface> interfaceLookup;
    private ComponentLookup<ObjectInstance.Decoration> decorationLookup;
    private ComponentLookup<ObjectInstance.DoorAndGrating> doorLookup;

    private NativeArray<Random> randoms;

    // private EntityQuery newTriggerQuery;
    private EntityQuery triggerQuery;
    private EntityArchetype triggerEventArchetype;

    [BurstCompile]
    public void OnCreate(ref SystemState state) {
      NextContinuousTrigger = 0.0;
      LevelEnterProcessed = false;

      state.RequireForUpdate<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>();
      state.RequireForUpdate<Level>();
      state.RequireForUpdate<Hacker>();
      state.RequireForUpdate<LevelInfo>();

      entityTypeHandle = state.GetEntityTypeHandle();
      instanceTypeHandleRO = state.GetComponentTypeHandle<ObjectInstance>(true);
      activateTypeHandleRW = state.GetComponentTypeHandle<TriggerActivateTag>();
      mapElementLookup = state.GetComponentLookup<MapElement>();
      instanceLookup = state.GetComponentLookup<ObjectInstance>();
      triggerLookup = state.GetComponentLookup<ObjectInstance.Trigger>();
      interfaceLookup = state.GetComponentLookup<ObjectInstance.Interface>();
      decorationLookup = state.GetComponentLookup<ObjectInstance.Decoration>();
      doorLookup = state.GetComponentLookup<ObjectInstance.DoorAndGrating>();

      randoms = new NativeArray<Random>(JobsUtility.ThreadIndexCount, Allocator.Persistent);
      for (int i = 0; i < randoms.Length; ++i)
        randoms[i] = Random.CreateFromIndex((uint)i);
      
      /*
      newTriggerQuery =  new EntityQueryBuilder(Allocator.Temp)
        .WithAll<ObjectInstance, ObjectInstance.Trigger>()
        .WithNone<TriggerActivateTag>()
        .Build(ref state);
      */

      triggerQuery = new EntityQueryBuilder(Allocator.Temp)
        .WithAll<ObjectInstance, ObjectInstance.Trigger>()
        .WithPresent<TriggerActivateTag>()
        .Build(ref state);

      triggerEventArchetype = state.EntityManager.CreateArchetype(stackalloc[] {
        ComponentType.ReadWrite<ScheduleEvent>()
      });
      
      // TODO listen Level singleton add and remove
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state) {
      randoms.Dispose();
    }
    
    public void OnStartRunning(ref SystemState state) {
    }

    public void OnStopRunning(ref SystemState state) {
      // LevelEnterProcessed = false;
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state) {
      var ecbSingleton = SystemAPI.GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>();

      var level = SystemAPI.GetSingleton<Level>();
      var player = SystemAPI.GetSingleton<Hacker>();
      var levelInfo = SystemAPI.GetSingleton<LevelInfo>();

      // state.EntityManager.AddComponent<TriggerActivateTag>(newTriggerQuery); // Adds TriggerActivateTag to triggers where it is missing.
      // state.EntityManager.SetComponentEnabled<TriggerActivateTag>(triggerQuery, false);
      
      entityTypeHandle.Update(ref state);
      instanceTypeHandleRO.Update(ref state);
      activateTypeHandleRW.Update(ref state);
      mapElementLookup.Update(ref state);
      instanceLookup.Update(ref state);
      triggerLookup.Update(ref state);
      interfaceLookup.Update(ref state);
      decorationLookup.Update(ref state);
      doorLookup.Update(ref state);

      var processorCommandBuffer = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

      var animationCommandListSystem = state.WorldUnmanaged.GetExistingUnmanagedSystem<AnimationCommandListSystem>();
      var animationCommandListSystemData = SystemAPI.GetComponent<AnimateObjectSystemData>(animationCommandListSystem);

      var triggerContinuous = SystemAPI.Time.ElapsedTime > NextContinuousTrigger;
      var triggerLevelEnter = !LevelEnterProcessed;
      
      var triggerJob = new TriggerJob {
        EntityTypeHandle = entityTypeHandle,
        InstanceTypeHandleRO = instanceTypeHandleRO,
        ActivateTypeHandleRW = activateTypeHandleRW,

        Processor = new TriggerProcessor() {
          CommandBuffer = processorCommandBuffer.AsParallelWriter(),
          TriggerEventArchetype = triggerEventArchetype,

          Player = player,
          TimeData = SystemAPI.Time,
          LevelInfo = levelInfo,

          TileMapBlobAsset = level.TileMap,
          ObjectInstancesRO = level.ObjectInstances.AsReadOnly(),

          MapElementLookupRW = mapElementLookup,
          InstanceLookupRW = instanceLookup,
          TriggerLookupRW = triggerLookup,
          InterfaceLookupRW = interfaceLookup,
          DecorationLookupRW = decorationLookup,
          DoorLookupRW = doorLookup,

          RandomsRW = randoms,

          animationList = animationCommandListSystemData.AllocateWriter(JobsUtility.ThreadIndexCount, state.WorldUpdateAllocator)
        },

        TrggerContinuous = triggerContinuous,
        TriggerLevelEnter = triggerLevelEnter
      };

      state.Dependency = triggerJob.ScheduleParallel(triggerQuery, state.Dependency);
      
      if (triggerLevelEnter) LevelEnterProcessed = true;
      if (triggerContinuous) NextContinuousTrigger = SystemAPI.Time.ElapsedTime + NextContinuousSeconds;
    }

    [BurstCompile]
    private struct TriggerJob : IJobChunk {
      [ReadOnly] public EntityTypeHandle EntityTypeHandle;
      [ReadOnly] public ComponentTypeHandle<ObjectInstance> InstanceTypeHandleRO;
      public ComponentTypeHandle<TriggerActivateTag> ActivateTypeHandleRW;

      public TriggerProcessor Processor;
      public bool TrggerContinuous;
      public bool TriggerLevelEnter;

      public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask) {
        var entities = chunk.GetNativeArray(EntityTypeHandle);
        var instances = chunk.GetNativeArray(ref InstanceTypeHandleRO);

        Processor.unfilteredChunkIndex = unfilteredChunkIndex;
        Processor.animationList.Commands.BeginForEachIndex(JobsUtility.ThreadIndex);
        // Debug.Log($"TriggerJob BeginForEachIndex {JobsUtility.ThreadIndex}");

        for (var i = 0; i < chunk.Count; ++i) {
          var entity = entities[i];
          var instance = instances[i];

          if (TriggerLevelEnter && instance.SubClass == 0 && instance.Info.Type == 8) { // Level Entry LEVEL_TRIG_TRIPLE
            Debug.Log($"TriggerSystem Level Entry e:{entity.Index}");
            Processor.Activate(entity, out bool message); // grind_trap
          } else if (TrggerContinuous && instance.SubClass == 0 && instance.Info.Type == 9) { // Continuous CONTIN_TRIG_TRIPLE
            Debug.Log($"TriggerSystem Continuous e:{entity.Index}");
            Processor.Activate(entity, out bool message); // trap_activate
          } else if (chunk.IsComponentEnabled(ref ActivateTypeHandleRW, i)) {
            Processor.Activate(entity, out bool message); // TODO do_multi_stuff?
          }
        }
        
        chunk.SetComponentEnabledForAll(ref ActivateTypeHandleRW, false);

        Processor.animationList.Commands.EndForEachIndex();
      }
    }
  }

  public struct TriggerActivateTag : IComponentData, IEnableableComponent { }
}
