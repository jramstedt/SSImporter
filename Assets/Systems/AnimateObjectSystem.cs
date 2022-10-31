using System.Linq;
using System.Runtime.InteropServices;
using SS.Resources;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Core;
using Unity.Entities;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace SS.System {
  [UpdateInGroup(typeof(VariableRateSimulationSystemGroup))]
  public partial class AnimateObjectSystem : SystemBase {
    private const ushort DoorResourceIdBase = 2400;

    private EntityQuery animationQuery;

    private NativeParallelHashMap<ushort, ushort> blockCounts;

    private Resources.ObjectProperties objectProperties;

    protected override async void OnCreate() {
      base.OnCreate();

      RequireForUpdate<Level>();

      animationQuery = GetEntityQuery(new EntityQueryDesc {
        All = new ComponentType[] {
          ComponentType.ReadWrite<AnimationData>()
        }
      });

      // TODO Make better somehow. Don't load specific file and scan trough.
      var artResources = await Addressables.LoadAssetAsync<ResourceFile>(@"objart3.res").Task;
      this.blockCounts = new (artResources.ResourceEntries.Count, Allocator.Persistent);
      foreach (var (id, resourceInfo) in artResources.ResourceEntries)
        this.blockCounts.Add(id, artResources.GetResourceBlockCount(resourceInfo));

      objectProperties = Services.ObjectProperties.WaitForCompletion();
    }

    protected override void OnUpdate() {
      var ecbSingleton = SystemAPI.GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>();
      var commandBuffer = ecbSingleton.CreateCommandBuffer(World.Unmanaged);

      var level = GetSingleton<Level>();

      var animateJob = new AnimateAnimationJob {
        animationTypeHandle = GetComponentTypeHandle<AnimationData>(),
        CommandBuffer = commandBuffer.AsParallelWriter(),

        ObjectInstancesBlobAsset = level.ObjectInstances,
        ObjectDatasBlobAsset = objectProperties.ObjectDatasBlobAsset,
        TimeData = SystemAPI.Time,
        blockCounts = blockCounts,
        InstanceLookup = GetComponentLookup<ObjectInstance>(),
        DecorationLookup = GetComponentLookup<ObjectInstance.Decoration>(),
        ItemLookup = GetComponentLookup<ObjectInstance.Item>(),
        EnemyLookup = GetComponentLookup<ObjectInstance.Enemy>(),
      };

      var animate = animateJob.ScheduleParallel(animationQuery, dependsOn: Dependency);
      Dependency = animate;
    }

    protected override void OnDestroy() {
      base.OnDestroy();

      this.blockCounts.Dispose();
    }

    [BurstCompile]
    struct AnimateAnimationJob : IJobChunk {
      public ComponentTypeHandle<AnimationData> animationTypeHandle;

      [WriteOnly] public EntityCommandBuffer.ParallelWriter CommandBuffer;

      [ReadOnly] public BlobAssetReference<BlobArray<Entity>> ObjectInstancesBlobAsset;
      [ReadOnly] public BlobAssetReference<ObjectDatas> ObjectDatasBlobAsset;

      [ReadOnly] public TimeData TimeData;
      [ReadOnly] public byte Level;

      [ReadOnly] public NativeParallelHashMap<ushort, ushort> blockCounts;

      [NativeDisableContainerSafetyRestriction] public ComponentLookup<ObjectInstance> InstanceLookup;
      [NativeDisableContainerSafetyRestriction] public ComponentLookup<ObjectInstance.Decoration> DecorationLookup;
      [NativeDisableContainerSafetyRestriction] public ComponentLookup<ObjectInstance.Item> ItemLookup;
      [NativeDisableContainerSafetyRestriction] public ComponentLookup<ObjectInstance.Enemy> EnemyLookup;

      public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask) {
        var animationDatas = chunk.GetNativeArray(animationTypeHandle);

        var deltaTime = TimeUtils.SecondsToFastTicks(TimeData.DeltaTime); //(ushort)(timeData.DeltaTime * 1000);

        for (int i = 0; i < chunk.Count; ++i) {
          var animation = animationDatas[i];
          var entity = ObjectInstancesBlobAsset.Value[animation.ObjectIndex];
          var instanceData = InstanceLookup[entity];

          var frameCount = 1;
          if (instanceData.Class == ObjectClass.DoorAndGrating) {
            var resourceId = DoorResourceIdBase + ObjectDatasBlobAsset.Value.ClassPropertyIndex(instanceData);
            frameCount = blockCounts[(ushort)resourceId];
          } else if (instanceData.Class == ObjectClass.Decoration) {
            var decoration = DecorationLookup[entity];
            frameCount = decoration.Cosmetic;
            if (frameCount == 0) frameCount = 1;
          } else if (instanceData.Class == ObjectClass.Item) {
            var item = ItemLookup[entity];
            frameCount = item.Cosmetic;
            if (frameCount == 0) frameCount = 4;
          } else if (instanceData.Class == ObjectClass.Enemy) {
            const int MAX_TELEPORT_FRAME = 10;
            const int DIEGO_DEATH_BATTLE_LEVEL = 8;

            var enemy = EnemyLookup[entity];

            if (instanceData.Triple == 0xe0401 && enemy.Posture == ObjectInstance.Enemy.PostureType.Death && Level != DIEGO_DEATH_BATTLE_LEVEL) // DIEGO_TRIPLE
              frameCount = MAX_TELEPORT_FRAME;
          } else {
            var baseData = ObjectDatasBlobAsset.Value.BasePropertyData(instanceData);
            frameCount = baseData.BitmapFrameCount;
          }

          var frameDeltaTime = deltaTime + instanceData.Info.TimeRemaining;
          var framesAnimated = frameDeltaTime / animation.FrameTime;
          instanceData.Info.TimeRemaining = (byte)(frameDeltaTime % animation.FrameTime);
          while (framesAnimated-- > 0) {
            // TODO FIXME door physics?

            if (animation.IsReversing) {
              --instanceData.Info.CurrentFrame;

              if (instanceData.Info.CurrentFrame < 0) {
                if (animation.IsCyclic) {
                  if (animation.CallbackIndex != 0 && animation.IsCallbackTypeCycle) {
                    // cb_list[cb_num++] = i;
                  }

                  animation.Flags &= ~AnimationData.AnimationFlags.Reversing;
                  instanceData.Info.CurrentFrame = 0;
                } else if (animation.IsRepeat) {
                  if (animation.CallbackIndex != 0 && animation.IsCallbackTypeRepeat) {
                    // cb_list[cb_num++] = i;
                  }

                  instanceData.Info.CurrentFrame = (sbyte)(frameCount - 1);
                } else {
                  instanceData.Info.CurrentFrame = 0;
                  // anim_rem[rem_num++] = i;
                }
              }
            } else {
              ++instanceData.Info.CurrentFrame;

              if (instanceData.Info.CurrentFrame >= frameCount) {
                if (animation.IsCyclic) {
                  if (animation.CallbackIndex != 0 && animation.IsCallbackTypeCycle) {
                    // cb_list[cb_num++] = i;
                  }

                  animation.Flags |= AnimationData.AnimationFlags.Reversing;
                  instanceData.Info.CurrentFrame = (sbyte)(frameCount - 1);
                } else if (animation.IsRepeat) {
                  if (animation.CallbackIndex != 0 && animation.IsCallbackTypeRepeat) {
                    // cb_list[cb_num++] = i;
                  }

                  instanceData.Info.CurrentFrame = 0;
                } else {
                  instanceData.Info.CurrentFrame = (sbyte)(frameCount - 1);
                  // anim_rem[rem_num++] = i;
                }
              }
            }
          }

          InstanceLookup[entity] = instanceData;
          animationDatas[i] = animation;

          CommandBuffer.AddComponent<AnimatedTag>(unfilteredChunkIndex, entity);
        }
      }
    }
  }

  public struct AnimatedTag : IComponentData  { }

  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct AnimationData : IComponentData {
    public const int MAX_ANIMLIST_SIZE = 64;

    public enum AnimationFlags : byte {
        Repeat = 0x01,
        Reversing = 0x02,
        Cyclic = 0x04 // Ping Pong
    }

    public enum AnimationCallbackType : ushort {
      Remove = 0x01,
      Repeat,
      Cycle
    }

    public enum Callback : uint {
      DiegoTeleport = 0x01,
      DestroyScreen,
      Unshodanize,
      Unmulti,
      Multi,
      Animate
    }

    public ushort ObjectIndex;
    public AnimationFlags Flags;
    public AnimationCallbackType CallbackType;
    public Callback CallbackIndex;
    public uint UserDataPointer;
    public readonly ushort FrameTime;

    public bool IsRepeat => (Flags & AnimationFlags.Repeat) == AnimationFlags.Repeat;
    public bool IsCyclic => (Flags & AnimationFlags.Cyclic) == AnimationFlags.Cyclic;
    public bool IsReversing => (Flags & AnimationFlags.Reversing) == AnimationFlags.Reversing;

    public bool IsCallbackTypeRemove => (CallbackType & AnimationCallbackType.Remove) == AnimationCallbackType.Remove;
    public bool IsCallbackTypeRepeat => (CallbackType & AnimationCallbackType.Repeat) == AnimationCallbackType.Repeat;
    public bool IsCallbackTypeCycle => (CallbackType & AnimationCallbackType.Cycle) == AnimationCallbackType.Cycle;
  }
}
