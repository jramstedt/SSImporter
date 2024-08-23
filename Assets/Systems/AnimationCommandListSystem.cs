using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine;
using static SS.System.AnimationData;

namespace SS.System {
  [UpdateInGroup(typeof(PresentationSystemGroup))]
  public partial struct AnimationCommandListSystem : ISystem {
    private EntityQuery animationQuery;

    private EntityArchetype animationArchetype;

    [BurstCompile]
    public void OnCreate(ref SystemState state) {
      state.EntityManager.AddComponentData(state.SystemHandle,  new AnimateObjectSystemData { Commands = new UnsafeStream(JobsUtility.ThreadIndexCount, Allocator.TempJob) });
      
      animationQuery = new EntityQueryBuilder(Allocator.Temp)
        .WithAllRW<AnimationData>()
        .Build(ref state);

      animationArchetype = state.EntityManager.CreateArchetype(
        new NativeArray<ComponentType>(1, Allocator.Temp) {
          [0] = ComponentType.ReadWrite<AnimationData>()
        }
      );
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state) {
      var systemData = state.EntityManager.GetComponentData<AnimateObjectSystemData>(state.SystemHandle);
      systemData.Commands.Dispose();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state) {
      var listProcessCommandBuffer = new EntityCommandBuffer(Allocator.TempJob);

      var animationCount = animationQuery.CalculateEntityCount();
      var cachedAnimations = new NativeArray<(Entity entity, AnimationData animationData)>(animationCount, Allocator.TempJob);
      var collectAnimationDataJob = new CollectAnimationDataJob {
        CachedAnimations = cachedAnimations
      };

      state.Dependency = collectAnimationDataJob.ScheduleParallel(animationQuery, state.Dependency);

      var commands = state.EntityManager.GetComponentDataRW<AnimateObjectSystemData>(state.SystemHandle).ValueRO.Commands;
      state.EntityManager.SetComponentData(state.SystemHandle, new AnimateObjectSystemData { Commands = new UnsafeStream(JobsUtility.ThreadIndexCount, Allocator.TempJob) });

      var processAnimationCommands = new ProcessAnimationCommands {
        commands = commands.AsReader(),
        AnimationArchetype = animationArchetype,
        CachedAnimations = cachedAnimations,
        CommandBuffer = listProcessCommandBuffer.AsParallelWriter()
      };

      state.Dependency = processAnimationCommands.Schedule(commands.ForEachCount, 1, state.Dependency);
      if (commands.IsCreated) commands.Dispose(state.Dependency);

      state.CompleteDependency();
      listProcessCommandBuffer.Playback(state.EntityManager);
    }

    [BurstCompile]
    struct ProcessAnimationCommands : IJobParallelFor {
      private int unfilteredChunkIndex;

      public UnsafeStream.Reader commands;

      [ReadOnly] public EntityArchetype AnimationArchetype;
      [ReadOnly] public NativeArray<(Entity entity, AnimationData animationData)> CachedAnimations;

      [WriteOnly] public EntityCommandBuffer.ParallelWriter CommandBuffer;

      public void Execute(int index) {
        int commandCount = commands.BeginForEachIndex(index);

        unfilteredChunkIndex = index;

        for (int i = 0; i < commandCount; i += 2) {
          var command = commands.Read<AnimationCommand>();
          if (command == AnimationCommand.Remove) {
            Debug.Log("<color=green> ProcessAnimationCommands removeAnimation");
            var data = commands.Read<AnimationRemove>();
            ProcessRemoveAnimation(data.objectIndex);
          } else {
            Debug.Log("<color=green> ProcessAnimationCommands addAnimation");
            var data = commands.Read<AnimationAdd>();
            ProcessAddAnimation(data.objectIndex, data.repeat, data.reverse, data.cycle, data.speed, data.callbackOperation, data.userData, data.callbackType);
          }
        }

        commands.EndForEachIndex();
      }

      private readonly Entity FindAnimatingEntity(ushort objectIndex) {
        foreach (var (entity, animationData) in CachedAnimations) {
          if (animationData.ObjectIndex == objectIndex)
            return entity;
        }

        return Entity.Null;
      }

      private void ProcessAddAnimation(ushort objectIndex, bool repeat, bool reverse, bool cycle, ushort speed, Callback callbackOperation, uint userData, AnimationCallbackType callbackType) {
        const ushort DEFAULT_ANIMLIST_SPEED = 128;

        AnimationFlags flags = 0;
        if (repeat) flags |= AnimationFlags.Repeat;
        if (reverse) flags |= AnimationFlags.Reversing;
        if (cycle) flags |= AnimationFlags.Cyclic;

        Debug.Log($"<color=white> ProcessAnimationCommands addAnimation rep:{repeat} rev:{reverse} cyc:{cycle} speed:{speed} cb:{callbackType} co:{callbackOperation} ud:{userData} oi:{objectIndex}");

        var animationData = new AnimationData {
          ObjectIndex = objectIndex,
          Flags = flags,
          CallbackType = callbackType,
          CallbackOperation = callbackOperation,
          UserData = userData,
          FrameTime = speed > 0 ? speed : DEFAULT_ANIMLIST_SPEED
        };

        var animationEntity = FindAnimatingEntity(objectIndex);

        Debug.Log($"<color=lightblue> ProcessAnimationCommands findAnimatingEntity e:{animationEntity} isnull:{animationEntity == Entity.Null}");

        if (animationEntity == Entity.Null)
          animationEntity = CommandBuffer.CreateEntity(unfilteredChunkIndex, AnimationArchetype);

        CommandBuffer.SetComponent(unfilteredChunkIndex, animationEntity, animationData);
      }

      private void ProcessRemoveAnimation(ushort objectIndex) {
        var animationEntity = FindAnimatingEntity(objectIndex);

        if (animationEntity != Entity.Null)
          CommandBuffer.DestroyEntity(unfilteredChunkIndex, animationEntity);
      }
    }


    [BurstCompile]
    private partial struct CollectAnimationDataJob : IJobEntity {
      [WriteOnly] public NativeArray<(Entity entity, AnimationData animationData)> CachedAnimations;

      public void Execute([EntityIndexInQuery] int entityInQueryIndex, in Entity entity, in AnimationData animationData) {
        CachedAnimations[entityInQueryIndex] = (entity, animationData);
      }
    }
  }

  internal enum AnimationCommand {
    Remove,
    Add
  }

  public struct AnimationRemove {
    public ushort objectIndex;
  }

  public struct AnimationAdd {
    public ushort objectIndex;
    public bool repeat;
    public bool reverse;
    public bool cycle;
    public ushort speed;
    public Callback callbackOperation;
    public uint userData;
    public AnimationCallbackType callbackType;
  }

  public struct AnimateObjectSystemData : IComponentData {
    public UnsafeStream Commands;

    [BurstCompile]
    public struct Writer {
      public UnsafeStream.Writer Commands;

      public void RemoveAnimation(ushort objectIndex) {
        Debug.Log($"<color=yellow> Adding removeAnimation {objectIndex}");

        Commands.Write(AnimationCommand.Remove);
        Commands.Write(new AnimationRemove { objectIndex = objectIndex });
      }

      public void AddAnimation(ushort objectIndex, bool repeat, bool reverse, bool cycle, ushort speed, Callback callbackOperation, uint userData, AnimationCallbackType callbackType) {
        Debug.Log($"<color=yellow> Adding addAnimation ud:{userData} oi:{objectIndex}");

        Commands.Write(AnimationCommand.Add);
        Commands.Write(
          new AnimationAdd {
            objectIndex = objectIndex,
            repeat = repeat,
            reverse = reverse,
            cycle = cycle,
            speed = speed,
            callbackOperation = callbackOperation,
            userData = userData,
            callbackType = callbackType
          }
        );
      }
    }
  }
}
