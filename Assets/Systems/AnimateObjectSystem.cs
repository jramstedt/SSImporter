using SS.Resources;
using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Core;
using Unity.Entities;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using static SS.TextureUtils;
using Random = Unity.Mathematics.Random;

namespace SS.System {
  [UpdateInGroup(typeof(VariableRateSimulationSystemGroup))]
  public partial class AnimateObjectSystem : SystemBase {
    private NativeParallelHashMap<ushort, ushort> blockCounts;

    private Resources.ObjectProperties objectProperties;

    private EntityTypeHandle entityTypeHandle;
    private ComponentTypeHandle<AnimationData> animationTypeHandle;

    private ComponentLookup<MapElement> mapElementLookup;
    private ComponentLookup<ObjectInstance> instanceLookup;
    private ComponentLookup<ObjectInstance.Item> itemLookup;
    private ComponentLookup<ObjectInstance.Enemy> enemyLookup;
    private ComponentLookup<ObjectInstance.Trigger> triggerLookup;
    private ComponentLookup<ObjectInstance.Interface> interfaceLookup;
    private ComponentLookup<ObjectInstance.Decoration> decorationLookup;
    private ComponentLookup<ObjectInstance.DoorAndGrating> doorLookup;
    private NativeArray<Random> randoms;
    private EntityQuery animationQuery;

    private EntityArchetype triggerEventArchetype;

    protected override void OnCreate() {
      base.OnCreate();

      RequireForUpdate<Level>();
      RequireForUpdate<LevelInfo>();
      RequireForUpdate<Hacker>();
      RequireForUpdate<AsyncLoadTag>();

      entityTypeHandle = GetEntityTypeHandle();
      animationTypeHandle = GetComponentTypeHandle<AnimationData>();
      mapElementLookup = GetComponentLookup<MapElement>();
      instanceLookup = GetComponentLookup<ObjectInstance>();
      itemLookup = GetComponentLookup<ObjectInstance.Item>(true);
      enemyLookup = GetComponentLookup<ObjectInstance.Enemy>();
      triggerLookup = GetComponentLookup<ObjectInstance.Trigger>();
      interfaceLookup = GetComponentLookup<ObjectInstance.Interface>();
      decorationLookup = GetComponentLookup<ObjectInstance.Decoration>();
      doorLookup = GetComponentLookup<ObjectInstance.DoorAndGrating>();

      randoms = new NativeArray<Random>(JobsUtility.MaxJobThreadCount, Allocator.Persistent);
      for (int i = 0; i < randoms.Length; ++i)
        randoms[i] = Random.CreateFromIndex((uint)i);

      animationQuery = GetEntityQuery(new EntityQueryDesc {
        All = new ComponentType[] {
          ComponentType.ReadWrite<AnimationData>()
        }
      });

      triggerEventArchetype = EntityManager.CreateArchetype(
        typeof(ScheduleEvent)
      );

      var objectPropertiesOp = Services.ObjectProperties;
      var artResourcesOp = Addressables.LoadAssetAsync<ResourceFile>(@"objart3.res");

      var loadOp = Addressables.ResourceManager.CreateGenericGroupOperation(new() { objectPropertiesOp, artResourcesOp });
      loadOp.Completed += op => {
        if (op.Status != AsyncOperationStatus.Succeeded)
          throw op.OperationException;

        var artResources = artResourcesOp.Result;

        // TODO Make better somehow. Don't load specific file and scan trough.
        this.blockCounts = new(artResources.ResourceEntries.Count, Allocator.Persistent);
        foreach (var (id, resourceInfo) in artResources.ResourceEntries)
          this.blockCounts.Add(id, artResources.GetResourceBlockCount(resourceInfo));

        objectProperties = objectPropertiesOp.Result;

        EntityManager.AddComponent<AsyncLoadTag>(this.SystemHandle);
      };
    }

    protected override void OnUpdate() {
      var ecbSingleton = SystemAPI.GetSingleton<EndVariableRateSimulationEntityCommandBufferSystem.Singleton>();

      var level = SystemAPI.GetSingleton<Level>();
      var player = SystemAPI.GetSingleton<Hacker>();
      var levelInfo = SystemAPI.GetSingleton<LevelInfo>();

      entityTypeHandle.Update(this);
      animationTypeHandle.Update(this);
      mapElementLookup.Update(this);
      instanceLookup.Update(this);
      itemLookup.Update(this);
      enemyLookup.Update(this);
      triggerLookup.Update(this);
      interfaceLookup.Update(this);
      decorationLookup.Update(this);
      doorLookup.Update(this);

      var animateJobCommandBuffer = ecbSingleton.CreateCommandBuffer(World.Unmanaged);
      var processorCommandBuffer = ecbSingleton.CreateCommandBuffer(World.Unmanaged);

      var animationCommandListSystem = World.GetExistingSystem<AnimationCommandListSystem>();
      var animationCommandListSystemData = SystemAPI.GetComponent<AnimateObjectSystemData>(animationCommandListSystem);

      var animateJob = new AnimateAnimationJob {
        entityTypeHandle = entityTypeHandle,
        animationTypeHandle = animationTypeHandle,

        ObjectInstancesBlobAsset = level.ObjectInstances,
        ObjectDatasBlobAsset = objectProperties.ObjectDatasBlobAsset,
        TimeData = SystemAPI.Time,
        blockCounts = blockCounts,
        InstanceLookup = instanceLookup,
        DecorationLookup = decorationLookup,
        ItemLookup = itemLookup,
        EnemyLookup = enemyLookup,

        Processor = new TriggerProcessor {
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

        CommandBuffer = animateJobCommandBuffer.AsParallelWriter()
      };

      Dependency = animateJob.ScheduleParallel(animationQuery, Dependency);
    }

    protected override void OnDestroy() {
      base.OnDestroy();

      this.blockCounts.Dispose();
      randoms.Dispose();
    }


    [BurstCompile]
    struct AnimateAnimationJob : IJobChunk {
      [NativeSetThreadIndex] internal readonly int threadIndex;

      [ReadOnly] public EntityTypeHandle entityTypeHandle;
      public ComponentTypeHandle<AnimationData> animationTypeHandle;

      [ReadOnly] public BlobAssetReference<BlobArray<Entity>> ObjectInstancesBlobAsset;
      [ReadOnly] public BlobAssetReference<ObjectDatas> ObjectDatasBlobAsset;

      [ReadOnly] public TimeData TimeData;
      [ReadOnly] public byte Level;

      [ReadOnly] public NativeParallelHashMap<ushort, ushort> blockCounts;

      [NativeDisableContainerSafetyRestriction] public ComponentLookup<ObjectInstance> InstanceLookup;
      [NativeDisableContainerSafetyRestriction] public ComponentLookup<ObjectInstance.Decoration> DecorationLookup;
      [NativeDisableContainerSafetyRestriction, ReadOnly] public ComponentLookup<ObjectInstance.Item> ItemLookup;
      [NativeDisableContainerSafetyRestriction, ReadOnly] public ComponentLookup<ObjectInstance.Enemy> EnemyLookup;

      public TriggerProcessor Processor;

      [WriteOnly] public EntityCommandBuffer.ParallelWriter CommandBuffer;

      public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask) {
        var animationEntities = chunk.GetNativeArray(entityTypeHandle);
        var animationDatas = chunk.GetNativeArray(ref animationTypeHandle);

        Processor.threadIndex = threadIndex;
        Processor.unfilteredChunkIndex = unfilteredChunkIndex;
        Processor.animationList.commands.BeginForEachIndex(threadIndex);

        var deltaTime = TimeUtils.SecondsToFastTicks(TimeData.DeltaTime); //(ushort)(timeData.DeltaTime * 1000);

        for (int i = 0; i < chunk.Count; ++i) {
          var animationEntity = animationEntities[i];
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
                    ProcessCallback(entity, ref instanceData, animation, unfilteredChunkIndex);

                  animation.Flags &= ~AnimationData.AnimationFlags.Reversing;
                  instanceData.Info.CurrentFrame = 0;
                } else if (animation.IsRepeat) {
                  if (animation.CallbackOperation != 0 && animation.IsCallbackTypeRepeat)
                    ProcessCallback(entity, ref instanceData, animation, unfilteredChunkIndex);

                  instanceData.Info.CurrentFrame = (sbyte)(frameCount - 1);
                } else { // Remove
                  instanceData.Info.CurrentFrame = 0;

                  if (animation.CallbackOperation != 0 && animation.IsCallbackTypeRemove)
                    ProcessCallback(entity, ref instanceData, animation, unfilteredChunkIndex);

                  CommandBuffer.DestroyEntity(unfilteredChunkIndex, animationEntity);
                }
              }
            } else {
              ++instanceData.Info.CurrentFrame;

              if (instanceData.Info.CurrentFrame >= frameCount) {
                if (animation.IsCyclic) {
                  if (animation.CallbackOperation != 0 && animation.IsCallbackTypeCycle)
                    ProcessCallback(entity, ref instanceData, animation, unfilteredChunkIndex);

                  animation.Flags |= AnimationData.AnimationFlags.Reversing;
                  instanceData.Info.CurrentFrame = (sbyte)(frameCount - 1);
                } else if (animation.IsRepeat) {
                  if (animation.CallbackOperation != 0 && animation.IsCallbackTypeRepeat)
                    ProcessCallback(entity, ref instanceData, animation, unfilteredChunkIndex);

                  instanceData.Info.CurrentFrame = 0;
                } else { // Remove
                  instanceData.Info.CurrentFrame = (sbyte)(frameCount - 1);

                  if (animation.CallbackOperation != 0 && animation.IsCallbackTypeRemove)
                    ProcessCallback(entity, ref instanceData, animation, unfilteredChunkIndex);

                  CommandBuffer.DestroyEntity(unfilteredChunkIndex, animationEntity);
                }
              }
            }
          }

          InstanceLookup[entity] = instanceData;
          animationDatas[i] = animation;

          CommandBuffer.AddComponent<AnimatedTag>(unfilteredChunkIndex, entity);
        }

        Processor.animationList.commands.EndForEachIndex();
      }

      private void ProcessCallback(in Entity entity, ref ObjectInstance instanceData, in AnimationData animation, int unfilteredChunkIndex) {
        var userData = animation.UserData;

        if (animation.CallbackOperation == AnimationData.Callback.UnShodanize) {
          Debug.Log($"<color=teal> AnimationData.Callback.UnShodanize ud:{userData} oi:{animation.ObjectIndex}");

          if (userData != 0) {
            Debug.Log($"AnimationData.Callback.UnShodanize Setting stuff");

            if (instanceData.Class == ObjectClass.Decoration) {
              var decoration = DecorationLookup[entity];
              decoration.Data2 = SHODAN_STATIC_MAGIC_COOKIE | ((uint)TextureType.Custom << TPOLY_INDEX_BITS);
              decoration.Cosmetic = 0;
              DecorationLookup[entity] = decoration;
            }
            instanceData.Info.CurrentFrame = 0;
          } else {
            Debug.Log($"AnimationData.Callback.UnShodanize addAnimation");

            instanceData.Info.TimeRemaining = 0;
            Processor.animationList.addAnimation(animation.ObjectIndex, false, true, false, 0, AnimationData.Callback.UnShodanize, 1, AnimationData.AnimationCallbackType.Remove);
          }
        } else if (animation.CallbackOperation == AnimationData.Callback.Animate) {
          if ((userData & 0x20000) == 0x20000) { // 1 << 17
            Processor.Multi((short)(userData & 0x7FFF));
          } else {
            Debug.Log($"AnimationData.Callback.Animate changeAnimation");

            Processor.ChangeAnimation(animation.ObjectIndex, 0, userData, false);
          }
        } else {
          Debug.LogWarning($"Not supported e:{entity.Index} o:{animation.ObjectIndex} t:{animation.CallbackType} op:{animation.CallbackOperation}");
        }
      }
    }

    private struct AsyncLoadTag : IComponentData { }
  }

#pragma warning disable CS0282
  [BurstCompile]
  public partial struct CollectAnimationDataJob : IJobEntity {
    [WriteOnly] public NativeArray<(Entity entity, AnimationData animationData)> CachedAnimations;

    public void Execute([EntityIndexInQuery] int entityInQueryIndex, in Entity entity, in AnimationData animationData) {
      CachedAnimations[entityInQueryIndex] = (entity, animationData);
    }
  }
#pragma warning restore CS0282


  public struct AnimatedTag : IComponentData { }

  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct AnimationData : IComponentData {
    public const int MAX_ANIMLIST_SIZE = 64;

    [Flags]
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
    public ushort FrameTime;

    public bool IsRepeat => (Flags & AnimationFlags.Repeat) == AnimationFlags.Repeat;
    public bool IsCyclic => (Flags & AnimationFlags.Cyclic) == AnimationFlags.Cyclic;
    public bool IsReversing => (Flags & AnimationFlags.Reversing) == AnimationFlags.Reversing;

    public bool IsCallbackTypeRemove => (CallbackType & AnimationCallbackType.Remove) == AnimationCallbackType.Remove;
    public bool IsCallbackTypeRepeat => (CallbackType & AnimationCallbackType.Repeat) == AnimationCallbackType.Repeat;
    public bool IsCallbackTypeCycle => (CallbackType & AnimationCallbackType.Cycle) == AnimationCallbackType.Cycle;
  }
}
