using SS.Resources;
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
    [BurstCompile]
    public void OnCreate(ref SystemState state) {
      state.RequireForUpdate<Level>();
      state.EntityManager.AddComponentData(state.SystemHandle,  new AnimateObjectSystemData { Commands = new UnsafeStream(JobsUtility.ThreadIndexCount, Allocator.TempJob) });
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state) {
      var systemData = state.EntityManager.GetComponentData<AnimateObjectSystemData>(state.SystemHandle);
      systemData.Commands.Dispose();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state) {
      var commands = state.EntityManager.GetComponentDataRW<AnimateObjectSystemData>(state.SystemHandle).ValueRO.Commands;
      state.EntityManager.SetComponentData(state.SystemHandle, new AnimateObjectSystemData { Commands = new UnsafeStream(JobsUtility.ThreadIndexCount, Allocator.TempJob) });

      var level = SystemAPI.GetSingleton<Level>();
      
      var processAnimationCommands = new ProcessAnimationCommands {
        Commands = commands.AsReader(),
        Animations = level.Animations,
      };

      state.Dependency = processAnimationCommands.Schedule(commands.ForEachCount, state.Dependency);
      if (commands.IsCreated) commands.Dispose(state.Dependency);

      state.CompleteDependency();
    }

    [BurstCompile]
    private struct ProcessAnimationCommands : IJobFor {
      public UnsafeStream.Reader Commands;
      public NativeList<AnimationData> Animations;
      
      public void Execute(int index) {
        int commandCount = Commands.BeginForEachIndex(index);

        for (int i = 0; i < commandCount; i += 2) {
          var command = Commands.Read<AnimationCommand>();
          if (command == AnimationCommand.Remove) {
            // Debug.Log("<color=green> ProcessAnimationCommands removeAnimation");
            var data = Commands.Read<AnimationRemove>();
            ProcessRemoveAnimation(data.objectIndex);
          } else {
            // Debug.Log("<color=green> ProcessAnimationCommands addAnimation");
            var data = Commands.Read<AnimationAdd>();
            ProcessAddAnimation(data.objectIndex, data.repeat, data.reverse, data.cycle, data.speed, data.callbackOperation, data.userData, data.callbackType);
          }
        }

        Commands.EndForEachIndex();
      }

      private void ProcessAddAnimation(ushort objectIndex, bool repeat, bool reverse, bool cycle, ushort speed, Callback callbackOperation, uint userData, AnimationCallbackType callbackType) {
        const ushort DEFAULT_ANIMLIST_SPEED = 128;

        AnimationFlags flags = 0;
        if (repeat) flags |= AnimationFlags.Repeat;
        if (reverse) flags |= AnimationFlags.Reversing;
        if (cycle) flags |= AnimationFlags.Cyclic;

        // Debug.Log($"<color=white> ProcessAnimationCommands addAnimation rep:{repeat} rev:{reverse} cyc:{cycle} speed:{speed} cb:{callbackType} co:{callbackOperation} ud:{userData} oi:{objectIndex}");

        var animationData = new AnimationData {
          ObjectIndex = objectIndex,
          Flags = flags,
          CallbackType = callbackType,
          CallbackOperation = callbackOperation,
          UserData = userData,
          FrameTime = speed > 0 ? speed : DEFAULT_ANIMLIST_SPEED
        };

        var animationIndex = IsAnimated(objectIndex, Animations.AsReadOnly());
        // Debug.Log($"<color=lightblue> ProcessAnimationCommands ProcessAddAnimation i:{animationIndex} l:{Animations.Length}");
        
        if (animationIndex == Animations.Length)
          Animations.AddNoResize(animationData);
        else
          Animations[animationIndex] = animationData;
      }

      private void ProcessRemoveAnimation(ushort objectIndex) {
        var animationIndex = IsAnimated(objectIndex, Animations.AsReadOnly());
        
        // Debug.Log($"<color=maroon> ProcessAnimationCommands ProcessRemoveAnimation i:{animationIndex} l:{Animations.Length}");
        
        if (animationIndex < Animations.Length)
          Animations.RemoveAtSwapBack(animationIndex);
      }
      
      public static int IsAnimated(ushort objectIndex, in NativeArray<AnimationData>.ReadOnly animationData) {
        var index = 0;
        for (; index < animationData.Length; ++index)
          if (animationData[index].ObjectIndex == objectIndex) return index;

        return index;
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
       // Debug.Log($"<color=yellow> Adding removeAnimation {objectIndex}");

        Commands.Write(AnimationCommand.Remove);
        Commands.Write(new AnimationRemove { objectIndex = objectIndex });
      }

      public void AddAnimation(ushort objectIndex, bool repeat, bool reverse, bool cycle, ushort speed, Callback callbackOperation, uint userData, AnimationCallbackType callbackType) {
        // Debug.Log($"<color=yellow> Adding addAnimation ud:{userData} oi:{objectIndex}");

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
