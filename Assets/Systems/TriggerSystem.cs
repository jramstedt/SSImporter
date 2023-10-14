using SS.Resources;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs.LowLevel.Unsafe;
using Random = Unity.Mathematics.Random;

namespace SS.System {
  [BurstCompile]
  [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
  public partial struct TriggerSystem : ISystem {
    private const double NextContinuousSeconds = 5.0;

    private EntityTypeHandle entityTypeHandle;
    private ComponentTypeHandle<ObjectInstance> instanceTypeHandle;
    private ComponentTypeHandle<ObjectInstance.Trigger> triggerTypeHandle;

    private ComponentLookup<MapElement> mapElementLookup;
    private ComponentLookup<ObjectInstance> instanceLookup;
    private ComponentLookup<ObjectInstance.Trigger> triggerLookup;
    private ComponentLookup<ObjectInstance.Interface> interfaceLookup;
    private ComponentLookup<ObjectInstance.Decoration> decorationLookup;
    private ComponentLookup<ObjectInstance.DoorAndGrating> doorLookup;

    private NativeArray<Random> randoms;

    private EntityQuery triggerQuery;
    private EntityArchetype triggerEventArchetype;

    public void OnCreate(ref SystemState state) {
      state.RequireForUpdate<Level>();

      entityTypeHandle = state.GetEntityTypeHandle();
      instanceTypeHandle = state.GetComponentTypeHandle<ObjectInstance>();
      triggerTypeHandle = state.GetComponentTypeHandle<ObjectInstance.Trigger>();
      mapElementLookup = state.GetComponentLookup<MapElement>();
      instanceLookup = state.GetComponentLookup<ObjectInstance>();
      triggerLookup = state.GetComponentLookup<ObjectInstance.Trigger>();
      interfaceLookup = state.GetComponentLookup<ObjectInstance.Interface>();
      decorationLookup = state.GetComponentLookup<ObjectInstance.Decoration>();
      doorLookup = state.GetComponentLookup<ObjectInstance.DoorAndGrating>();

      randoms = new NativeArray<Random>(JobsUtility.ThreadIndexCount, Allocator.Persistent);
      for (int i = 0; i < randoms.Length; ++i)
        randoms[i] = Random.CreateFromIndex((uint)i);

      triggerQuery = state.GetEntityQuery(new EntityQueryDesc {
        All = new ComponentType[] {
          ComponentType.ReadOnly<ObjectInstance>(),
          ComponentType.ReadOnly<ObjectInstance.Trigger>(),
          ComponentType.ReadOnly<TriggerActivateTag>()
        }
      });

      triggerEventArchetype = state.EntityManager.CreateArchetype(
        typeof(ScheduleEvent)
      );
    }

    public void OnDestroy(ref SystemState state) {
      randoms.Dispose();
    }

    public void OnUpdate(ref SystemState state) {
      var ecbSingleton = SystemAPI.GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>();

      var level = SystemAPI.GetSingleton<Level>();
      var player = SystemAPI.GetSingleton<Hacker>();
      var levelInfo = SystemAPI.GetSingleton<LevelInfo>();

      entityTypeHandle.Update(ref state);
      instanceTypeHandle.Update(ref state);
      triggerTypeHandle.Update(ref state);
      mapElementLookup.Update(ref state);
      instanceLookup.Update(ref state);
      triggerLookup.Update(ref state);
      interfaceLookup.Update(ref state);
      decorationLookup.Update(ref state);
      doorLookup.Update(ref state);

      var triggerJobCommandBuffer = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
      var processorCommandBuffer = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

      var animationCommandListSystem = state.World.GetExistingSystem<AnimationCommandListSystem>();
      var animationCommandListSystemData = SystemAPI.GetComponent<AnimateObjectSystemData>(animationCommandListSystem);

      var triggerJob = new TriggerJob {
        entityTypeHandle = entityTypeHandle,
        instanceTypeHandle = instanceTypeHandle,
        triggerTypeHandle = triggerTypeHandle,

        Processor = new TriggerProcessor() {
          CommandBuffer = processorCommandBuffer.AsParallelWriter(),
          TriggerEventArchetype = triggerEventArchetype,

          Player = player,
          TimeData = SystemAPI.Time,
          LevelInfo = levelInfo,

          TileMapBlobAsset = level.TileMap,
          ObjectInstancesBlobAsset = level.ObjectInstances,

          MapElementLookup = mapElementLookup,
          InstanceLookup = instanceLookup,
          TriggerLookup = triggerLookup,
          InterfaceLookup = interfaceLookup,
          DecorationLookup = decorationLookup,
          DoorLookup = doorLookup,

          Randoms = randoms,

          animationList = new AnimateObjectSystemData.Writer {
            commands = animationCommandListSystemData.commands.AsWriter()
          }
        },

        CommandBuffer = triggerJobCommandBuffer.AsParallelWriter(),
      };

      state.Dependency = triggerJob.ScheduleParallel(triggerQuery, state.Dependency);
    }

    [BurstCompile]
    private struct TriggerJob : IJobChunk {
      [ReadOnly] public EntityTypeHandle entityTypeHandle;
      [ReadOnly] public ComponentTypeHandle<ObjectInstance> instanceTypeHandle;
      [ReadOnly] public ComponentTypeHandle<ObjectInstance.Trigger> triggerTypeHandle;

      public TriggerProcessor Processor;

      [WriteOnly] public EntityCommandBuffer.ParallelWriter CommandBuffer;

      public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask) {
        var entities = chunk.GetNativeArray(entityTypeHandle);
        var instances = chunk.GetNativeArray(ref instanceTypeHandle);
        var triggers = chunk.GetNativeArray(ref triggerTypeHandle);

        Processor.unfilteredChunkIndex = unfilteredChunkIndex;
        Processor.animationList.commands.BeginForEachIndex(JobsUtility.ThreadIndex);

        for (int i = 0; i < chunk.Count; ++i) {
          var entity = entities[i];
          var instance = instances[i];
          var trigger = triggers[i];

          Processor.Activate(entity, out bool message);

          CommandBuffer.RemoveComponent<TriggerActivateTag>(unfilteredChunkIndex, entity);
        }

        Processor.animationList.commands.EndForEachIndex();
      }
    }
  }

  public struct TriggerActivateTag : IComponentData { }
}
