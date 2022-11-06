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
using static SS.TextureUtils;

namespace SS.System {
  [UpdateInGroup(typeof(VariableRateSimulationSystemGroup))]
  public partial class AnimateObjectSystem : SystemBase {
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
        entityTypeHandle = GetEntityTypeHandle(),
        animationTypeHandle = GetComponentTypeHandle<AnimationData>(),
        CommandBuffer = commandBuffer.AsParallelWriter(),

        ObjectInstancesBlobAsset = level.ObjectInstances,
        ObjectDatasBlobAsset = objectProperties.ObjectDatasBlobAsset,
        TimeData = SystemAPI.Time,
        blockCounts = blockCounts,
        InstanceLookup = GetComponentLookup<ObjectInstance>(),
        DecorationLookup = GetComponentLookup<ObjectInstance.Decoration>(true),
        ItemLookup = GetComponentLookup<ObjectInstance.Item>(true),
        EnemyLookup = GetComponentLookup<ObjectInstance.Enemy>(true),
      };

      var animate = animateJob.ScheduleParallel(animationQuery, dependsOn: Dependency);
      Dependency = animate;
    }

    protected override void OnDestroy() {
      base.OnDestroy();

      this.blockCounts.Dispose();
    }

    // TODO FIXME remove enity's animations (and call remove callback if has one)

    [BurstCompile]
    struct AnimateAnimationJob : IJobChunk {
      public EntityTypeHandle entityTypeHandle;
      public ComponentTypeHandle<AnimationData> animationTypeHandle;

      [WriteOnly] public EntityCommandBuffer.ParallelWriter CommandBuffer;

      [ReadOnly] public BlobAssetReference<BlobArray<Entity>> ObjectInstancesBlobAsset;
      [ReadOnly] public BlobAssetReference<ObjectDatas> ObjectDatasBlobAsset;

      [ReadOnly] public TimeData TimeData;
      [ReadOnly] public byte Level;

      [ReadOnly] public NativeParallelHashMap<ushort, ushort> blockCounts;

      [NativeDisableContainerSafetyRestriction] public ComponentLookup<ObjectInstance> InstanceLookup;
      [NativeDisableContainerSafetyRestriction, ReadOnly] public ComponentLookup<ObjectInstance.Decoration> DecorationLookup;
      [NativeDisableContainerSafetyRestriction, ReadOnly] public ComponentLookup<ObjectInstance.Item> ItemLookup;
      [NativeDisableContainerSafetyRestriction, ReadOnly] public ComponentLookup<ObjectInstance.Enemy> EnemyLookup;

      public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask) {
        var animationentities = chunk.GetNativeArray(entityTypeHandle);
        var animationDatas = chunk.GetNativeArray(animationTypeHandle);

        var deltaTime = TimeUtils.SecondsToFastTicks(TimeData.DeltaTime); //(ushort)(timeData.DeltaTime * 1000);

        for (int i = 0; i < chunk.Count; ++i) {
          var animationEntity = animationentities[i];
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
                  if (animation.CallbackOperation != 0 && animation.IsCallbackTypeCycle)
                    processCallback(entity, ref instanceData, animation, unfilteredChunkIndex);

                  animation.Flags &= ~AnimationData.AnimationFlags.Reversing;
                  instanceData.Info.CurrentFrame = 0;
                } else if (animation.IsRepeat) {
                  if (animation.CallbackOperation != 0 && animation.IsCallbackTypeRepeat)
                    processCallback(entity, ref instanceData, animation, unfilteredChunkIndex);

                  instanceData.Info.CurrentFrame = (sbyte)(frameCount - 1);
                } else { // Remove
                  instanceData.Info.CurrentFrame = 0;

                  if (animation.CallbackOperation != 0 && animation.IsCallbackTypeRemove)
                    processCallback(entity, ref instanceData, animation, unfilteredChunkIndex);

                  CommandBuffer.DestroyEntity(unfilteredChunkIndex, animationEntity);
                }
              }
            } else {
              ++instanceData.Info.CurrentFrame;

              if (instanceData.Info.CurrentFrame >= frameCount) {
                if (animation.IsCyclic) {
                  if (animation.CallbackOperation != 0 && animation.IsCallbackTypeCycle)
                    processCallback(entity, ref instanceData, animation, unfilteredChunkIndex);

                  animation.Flags |= AnimationData.AnimationFlags.Reversing;
                  instanceData.Info.CurrentFrame = (sbyte)(frameCount - 1);
                } else if (animation.IsRepeat) {
                  if (animation.CallbackOperation != 0 && animation.IsCallbackTypeRepeat)
                    processCallback(entity, ref instanceData, animation, unfilteredChunkIndex);

                  instanceData.Info.CurrentFrame = 0;
                } else { // Remove
                  instanceData.Info.CurrentFrame = (sbyte)(frameCount - 1);

                  if (animation.CallbackOperation != 0 && animation.IsCallbackTypeRemove)
                    processCallback(entity, ref instanceData, animation, unfilteredChunkIndex);
                  
                  CommandBuffer.DestroyEntity(unfilteredChunkIndex, animationEntity);
                }
              }
            }
          }

          InstanceLookup[entity] = instanceData;
          animationDatas[i] = animation;

          CommandBuffer.AddComponent<AnimatedTag>(unfilteredChunkIndex, entity);
        }
      }

      private void processCallback(in Entity entity, ref ObjectInstance instanceData, in AnimationData animation, int unfilteredChunkIndex) {
        var userData = animation.UserData;

        if (animation.CallbackOperation == AnimationData.Callback.UnShodanize) {
          if (userData != 0) {
            if (instanceData.Class == ObjectClass.Decoration) {
              var decoration = DecorationLookup[entity];
              decoration.Data2 = SHODAN_STATIC_MAGIC_COOKIE | ((uint)TextureType.Custom << TPOLY_INDEX_BITS);
              decoration.Cosmetic = 0;
              DecorationLookup[entity] = decoration;
            }
            instanceData.Info.CurrentFrame = 0;
          } else {
            // add_obj_to_animlist(animation.ObjectIndex, false, true, false, 0, AnimationData.Callback.UnShodanize, 1, AnimationData.AnimationCallbackType.Remove);
          }
        } else if (animation.CallbackOperation == AnimationData.Callback.Animate) {
          if ((userData & 0x20000) == 0x20000) { // 1 << 17
            //TriggerJob.multi(userData & 0x7FFF);
          } else {
            //TriggerJob.changeAnimation(animation.ObjectIndex, 0, userData, 0)
          }
        } else {
          Debug.LogWarning($"Not supported e:{entity.Index} o:{animation.ObjectIndex} t:{animation.CallbackType} op:{animation.CallbackOperation}");
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
      Null = 0x00,
      Remove = 0x01,
      Repeat,
      Cycle
    }

    public enum Callback : uint {
      Null = 0x00,
      DiegoTeleport = 0x01,
      DestroyScreen,
      UnShodanize,
      Unmulti,
      Multi,
      Animate
    }

    public ushort ObjectIndex;
    public AnimationFlags Flags;
    public AnimationCallbackType CallbackType;
    public Callback CallbackOperation;
    public uint UserData;
    public readonly ushort FrameTime;

    public bool IsRepeat => (Flags & AnimationFlags.Repeat) == AnimationFlags.Repeat;
    public bool IsCyclic => (Flags & AnimationFlags.Cyclic) == AnimationFlags.Cyclic;
    public bool IsReversing => (Flags & AnimationFlags.Reversing) == AnimationFlags.Reversing;

    public bool IsCallbackTypeRemove => (CallbackType & AnimationCallbackType.Remove) == AnimationCallbackType.Remove;
    public bool IsCallbackTypeRepeat => (CallbackType & AnimationCallbackType.Repeat) == AnimationCallbackType.Repeat;
    public bool IsCallbackTypeCycle => (CallbackType & AnimationCallbackType.Cycle) == AnimationCallbackType.Cycle;
  }
}
