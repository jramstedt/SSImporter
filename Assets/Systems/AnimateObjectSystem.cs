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
using Unity.Physics;
using UnityEngine;
using static SS.TextureUtils;
using Random = Unity.Mathematics.Random;

namespace SS.System {
  [UpdateInGroup(typeof(VariableRateSimulationSystemGroup))]
  public partial class AnimateObjectSystem : SystemBase {
    public const ushort MAX_ANIMLIST_SIZE = 64;
    
    private NativeParallelHashMap<ushort, ushort> blockCounts;

    private Resources.ObjectProperties objectProperties;

    private ComponentLookup<MapElement> mapElementLookupRW;
    private ComponentLookup<ObjectInstance> instanceLookupRW;
    private ComponentLookup<ObjectInstance.Item> itemLookupRO;
    private ComponentLookup<ObjectInstance.Enemy> enemyLookupRO;
    private ComponentLookup<ObjectInstance.Trigger> triggerLookupRW;
    private ComponentLookup<ObjectInstance.Interface> interfaceLookupRW;
    private ComponentLookup<ObjectInstance.Decoration> decorationLookupRW;
    private ComponentLookup<ObjectInstance.DoorAndGrating> doorLookupRW;
    private ComponentLookup<PhysicsCollider> physicsColliderRW;
    private ComponentLookup<AnimatedTag> animatedTagRW;
    private NativeArray<Random> randoms;

    private EntityArchetype triggerEventArchetype;

    protected override async void OnCreate() {
      base.OnCreate();

      RequireForUpdate<Level>();
      RequireForUpdate<LevelInfo>();
      RequireForUpdate<Hacker>();
      RequireForUpdate<AsyncLoadTag>();

      mapElementLookupRW = GetComponentLookup<MapElement>();
      instanceLookupRW = GetComponentLookup<ObjectInstance>();
      itemLookupRO = GetComponentLookup<ObjectInstance.Item>(true);
      enemyLookupRO = GetComponentLookup<ObjectInstance.Enemy>(true);
      triggerLookupRW = GetComponentLookup<ObjectInstance.Trigger>();
      interfaceLookupRW = GetComponentLookup<ObjectInstance.Interface>();
      decorationLookupRW = GetComponentLookup<ObjectInstance.Decoration>();
      doorLookupRW = GetComponentLookup<ObjectInstance.DoorAndGrating>();
      physicsColliderRW = GetComponentLookup<PhysicsCollider>();
      animatedTagRW = GetComponentLookup<AnimatedTag>();

      randoms = new NativeArray<Random>(JobsUtility.ThreadIndexCount, Allocator.Persistent);
      for (int i = 0; i < randoms.Length; ++i)
        randoms[i] = Random.CreateFromIndex((uint)i);

      triggerEventArchetype = EntityManager.CreateArchetype(
        typeof(ScheduleEvent)
      );

      objectProperties = await Services.ObjectProperties;

      var artResources = await Res.Open(Res.dataPath + @"objart3.res");

      // TODO Make better somehow. Don't load specific file and scan trough.
      blockCounts = new(artResources.ResourceEntries.Count, Allocator.Persistent);
      foreach (var (id, resourceInfo) in artResources.ResourceEntries)
        blockCounts.Add(id, artResources.GetResourceBlockCount(resourceInfo));

      EntityManager.AddComponent<AsyncLoadTag>(SystemHandle);
    }

    protected override void OnDestroy() {
      base.OnDestroy();

      blockCounts.Dispose();
      randoms.Dispose();
    }

    protected override void OnUpdate() {
      var ecbSingleton = SystemAPI.GetSingleton<EndVariableRateSimulationEntityCommandBufferSystem.Singleton>();

      var level = SystemAPI.GetSingleton<Level>();
      var player = SystemAPI.GetSingleton<Hacker>();
      var levelInfo = SystemAPI.GetSingleton<LevelInfo>();

      mapElementLookupRW.Update(this);
      instanceLookupRW.Update(this);
      itemLookupRO.Update(this);
      enemyLookupRO.Update(this);
      triggerLookupRW.Update(this);
      interfaceLookupRW.Update(this);
      decorationLookupRW.Update(this);
      doorLookupRW.Update(this);
      physicsColliderRW.Update(this);
      animatedTagRW.Update(this);

      var processorCommandBuffer = ecbSingleton.CreateCommandBuffer(World.Unmanaged);

      var animationCommandListSystem = World.GetExistingSystem<AnimationCommandListSystem>();
      var animationCommandListSystemData = SystemAPI.GetComponent<AnimateObjectSystemData>(animationCommandListSystem);

      var removedList = new NativeList<byte>(MAX_ANIMLIST_SIZE, Allocator.TempJob);
      var callbackList = new NativeList<byte>(MAX_ANIMLIST_SIZE, Allocator.TempJob);
      
      // TODO Animating class objects

      Dependency = new AnimateAnimationJob {
        ObjectInstancesBlobAsset = level.ObjectInstances,
        ObjectDatasBlobAsset = objectProperties.ObjectDatasBlobAsset,

        TimeData = SystemAPI.Time,
        LevelIndex = 0, // TODO Level index

        BlockCounts = blockCounts,
        Animations = level.Animations.AsArray(),

        PhysicsColliderRW = physicsColliderRW,
        AnimatedTagRW = animatedTagRW,

        InstanceLookupRW = instanceLookupRW,
        ItemLookupRO = itemLookupRO,
        EnemyLookupRO = enemyLookupRO,
        DecorationLookupRO = decorationLookupRW,

        CallbackOut = callbackList.AsParallelWriter(),
        RemoveOut = removedList.AsParallelWriter()
      }.Schedule(level.Animations.Length, Dependency);

      Dependency = new ProcessCallbacksJob() {
        Processor = new TriggerProcessor {
          CommandBuffer = processorCommandBuffer.AsParallelWriter(),
          TriggerEventArchetype = triggerEventArchetype,

          Player = player,
          TimeData = SystemAPI.Time,
          LevelInfo = levelInfo,

          TileMapBlobAsset = level.TileMap,
          ObjectInstancesBlobAsset = level.ObjectInstances,

          MapElementLookupRW = mapElementLookupRW,
          InstanceLookupRW = instanceLookupRW,
          TriggerLookupRW = triggerLookupRW,
          InterfaceLookupRW = interfaceLookupRW,
          DecorationLookupRW = decorationLookupRW,
          DoorLookupRW = doorLookupRW,

          RandomsRW = randoms,

          animationList = new AnimateObjectSystemData.Writer {
            Commands = animationCommandListSystemData.Commands.AsWriter()
          }
        },
        
        ObjectInstancesBlobAsset = level.ObjectInstances,
        Animations = level.Animations,
        
        InstanceLookupRW = instanceLookupRW,
        DecorationLookupRW = decorationLookupRW,
        
        Callback = callbackList.AsDeferredJobArray(),
        Remove = removedList.AsDeferredJobArray()
      }.Schedule(Dependency);
    }

    [BurstCompile]
    struct AnimateAnimationJob : IJobFor {
      [ReadOnly] public BlobAssetReference<BlobArray<Entity>> ObjectInstancesBlobAsset;
      [ReadOnly] public BlobAssetReference<ObjectDatas> ObjectDatasBlobAsset;

      [ReadOnly] public TimeData TimeData;
      [ReadOnly] public byte LevelIndex;

      [ReadOnly] public NativeParallelHashMap<ushort, ushort> BlockCounts;
      public NativeArray<AnimationData> Animations;

      public ComponentLookup<PhysicsCollider> PhysicsColliderRW;
      public ComponentLookup<AnimatedTag> AnimatedTagRW;

      public ComponentLookup<ObjectInstance> InstanceLookupRW;
      [ReadOnly] public ComponentLookup<ObjectInstance.Item> ItemLookupRO;
      [ReadOnly] public ComponentLookup<ObjectInstance.Enemy> EnemyLookupRO;
      [ReadOnly] public ComponentLookup<ObjectInstance.Decoration> DecorationLookupRO;
      
      [WriteOnly] public NativeList<byte>.ParallelWriter CallbackOut;
      [WriteOnly] public NativeList<byte>.ParallelWriter RemoveOut;

      public void Execute(int index) {
        var deltaTime = TimeUtils.SecondsToFastTicks(TimeData.DeltaTime);
        
        var animation = Animations[index];

        var entity = ObjectInstancesBlobAsset.Value[animation.ObjectIndex];
        ref var instanceData = ref InstanceLookupRW.GetRefRW(entity).ValueRW;

        var frameCount = 1;
        if (instanceData.Class == ObjectClass.DoorAndGrating) {
          var resourceId = DoorResourceIdBase + ObjectDatasBlobAsset.Value.ClassPropertyIndex(instanceData);
          frameCount = BlockCounts[(ushort)resourceId];
        } else if (instanceData.Class == ObjectClass.Decoration) {
          var decoration = DecorationLookupRO.GetRefRO(entity).ValueRO;
          frameCount = decoration.Cosmetic;
          if (frameCount == 0) frameCount = 1;
        } else if (instanceData.Class == ObjectClass.Item) {
          var item = ItemLookupRO.GetRefRO(entity).ValueRO;
          frameCount = item.Cosmetic;
          if (frameCount == 0) frameCount = 4;
        } else if (instanceData.Class == ObjectClass.Enemy) {
          const int MAX_TELEPORT_FRAME = 10;
          const int DIEGO_DEATH_BATTLE_LEVEL = 8;

          var enemy = EnemyLookupRO.GetRefRO(entity).ValueRO;

          if (instanceData.Triple == 0xe0401 /* DIEGO_TRIPLE */ && enemy.Posture == ObjectInstance.Enemy.PostureType.Death && LevelIndex != DIEGO_DEATH_BATTLE_LEVEL)
            frameCount = MAX_TELEPORT_FRAME;
        } else {
          var baseData = ObjectDatasBlobAsset.Value.BasePropertyData(instanceData);
          frameCount = baseData.BitmapFrameCount;
        }

        var frameDeltaTime = deltaTime + instanceData.Info.TimeRemaining;
        var framesAnimated = frameDeltaTime / animation.FrameTime;
        instanceData.Info.TimeRemaining = (byte)(frameDeltaTime % animation.FrameTime);
        while (framesAnimated-- > 0) {
          if (animation.IsReversing) {
            --instanceData.Info.CurrentFrame;

            if (instanceData.Info.CurrentFrame < 0) {
              if (animation.IsCyclic) {
                if (animation.CallbackOperation != 0 && animation.IsCallbackTypeCycle)
                  CallbackOut.AddNoResize((byte)index); // ProcessCallback(entity, ref instanceData, animation);

                animation.Flags &= ~AnimationData.AnimationFlags.Reversing;
                instanceData.Info.CurrentFrame = 0;
              } else if (animation.IsRepeat) {
                if (animation.CallbackOperation != 0 && animation.IsCallbackTypeRepeat)
                  CallbackOut.AddNoResize((byte)index); // ProcessCallback(entity, ref instanceData, animation);

                instanceData.Info.CurrentFrame = (sbyte)(frameCount - 1);
              } else { // Remove
                instanceData.Info.CurrentFrame = 0;
                RemoveOut.AddNoResize((byte)index);

                /*
                if (animation.CallbackOperation != 0 && animation.IsCallbackTypeRemove)
                  ProcessCallback(entity, ref instanceData, animation);
                */
              }
            }
          } else {
            ++instanceData.Info.CurrentFrame;

            if (instanceData.Info.CurrentFrame >= frameCount) {
              if (animation.IsCyclic) {
                if (animation.CallbackOperation != 0 && animation.IsCallbackTypeCycle) {
                  // Debug.Log($"<color=yellow> CallbackOperation i:{index}");
                  CallbackOut.AddNoResize((byte)index); // ProcessCallback(entity, ref instanceData, animation);
                }

                animation.Flags |= AnimationData.AnimationFlags.Reversing;
                instanceData.Info.CurrentFrame = (sbyte)(frameCount - 1);
              } else if (animation.IsRepeat) {
                if (animation.CallbackOperation != 0 && animation.IsCallbackTypeRepeat) {
                  // Debug.Log($"<color=yellow> CallbackOperation i:{index}");
                  CallbackOut.AddNoResize((byte)index); // ProcessCallback(entity, ref instanceData, animation);
                }

                instanceData.Info.CurrentFrame = 0;
              } else { // Remove
                // Debug.Log($"<color=yellow> Remove i:{index}");
                
                instanceData.Info.CurrentFrame = (sbyte)(frameCount - 1);
                RemoveOut.AddNoResize((byte)index);
                
                /*
                if (animation.CallbackOperation != 0 && animation.IsCallbackTypeRemove)
                  ProcessCallback(entity, ref instanceData, animation);
                */
              }
            }
          }
        }
        
        if (instanceData.Class == ObjectClass.DoorAndGrating) {
          var collisionResponsePolicy = instanceData.Info.CurrentFrame >= ObjectInstance.DoorAndGrating.DOOR_OPEN_FRAME ? CollisionResponsePolicy.None : CollisionResponsePolicy.Collide;
          PhysicsColliderRW.GetRefRW(entity).ValueRW.Value.Value.SetCollisionResponse(collisionResponsePolicy);
        }

        Animations[index] = animation;
        
        if(AnimatedTagRW.HasComponent(entity))
          AnimatedTagRW.SetComponentEnabled(entity, true);
      }
    }
    
    [BurstCompile]
    struct ProcessCallbacksJob : IJob {
      public TriggerProcessor Processor;
      
      [ReadOnly] public BlobAssetReference<BlobArray<Entity>> ObjectInstancesBlobAsset;
      public NativeList<AnimationData> Animations;
      
      public ComponentLookup<ObjectInstance> InstanceLookupRW;
      public ComponentLookup<ObjectInstance.Decoration> DecorationLookupRW;

      [ReadOnly] public NativeArray<byte> Callback;
      [ReadOnly] public NativeArray<byte> Remove;
      
      public void Execute() {
        Processor.unfilteredChunkIndex = 0;
        Processor.animationList.Commands.BeginForEachIndex(JobsUtility.ThreadIndex);
        
        // Debug.Log($"<color=green> ProcessCallbacksJob cl:{Callback.Length} rl:{Remove.Length}");

        for (var i = 0; i < Callback.Length; ++i) {
          var animation = Animations[Callback[i]];
          var entity = ObjectInstancesBlobAsset.Value[animation.ObjectIndex];
          ref var instanceData = ref InstanceLookupRW.GetRefRW(entity).ValueRW;
          ProcessCallback(entity, ref instanceData, animation);
        }

        for (var i = 0; i < Remove.Length; ++i)
          RemoveAnimation(Remove[i]);

        Processor.animationList.Commands.EndForEachIndex();
      }
      
      private void ProcessCallback(in Entity entity, ref ObjectInstance instanceData, in AnimationData animation) {
        var userData = animation.UserData;

        if (animation.CallbackOperation == AnimationData.Callback.UnShodanize) {
          Debug.Log($"<color=teal> AnimationData.Callback.UnShodanize ud:{userData} oi:{animation.ObjectIndex}");

          if (userData != 0) {
            Debug.Log($"AnimationData.Callback.UnShodanize Setting stuff");

            if (instanceData.Class == ObjectClass.Decoration) {
              ref var decoration = ref DecorationLookupRW.GetRefRW(entity).ValueRW;
              decoration.Data2 = SHODAN_STATIC_MAGIC_COOKIE | ((uint)TextureType.Custom << TPOLY_INDEX_BITS);
              decoration.Cosmetic = 0;
            }
            instanceData.Info.CurrentFrame = 0;
          } else {
            Debug.Log($"AnimationData.Callback.UnShodanize addAnimation");

            instanceData.Info.TimeRemaining = 0;
            Processor.animationList.AddAnimation(animation.ObjectIndex, false, true, false, 0, AnimationData.Callback.UnShodanize, 1, AnimationData.AnimationCallbackType.Remove);
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

      private void RemoveAnimation(byte animationIndex) {
        var animation = Animations[animationIndex];
        var entity = ObjectInstancesBlobAsset.Value[animation.ObjectIndex];
        ref var instanceData = ref InstanceLookupRW.GetRefRW(entity).ValueRW;
        
        Animations.RemoveAtSwapBack(animationIndex);
        
        if (animation.CallbackOperation != 0 && animation.IsCallbackTypeRemove)
          ProcessCallback(entity, ref instanceData, animation);
      }
    }

    private struct AsyncLoadTag : IComponentData { }
  }

  public struct AnimatedTag : IComponentData, IEnableableComponent { }

  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct AnimationData {
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

    public readonly bool IsRepeat => (Flags & AnimationFlags.Repeat) == AnimationFlags.Repeat;
    public readonly bool IsCyclic => (Flags & AnimationFlags.Cyclic) == AnimationFlags.Cyclic;
    public readonly bool IsReversing => (Flags & AnimationFlags.Reversing) == AnimationFlags.Reversing;

    public readonly bool IsCallbackTypeRemove => (CallbackType & AnimationCallbackType.Remove) == AnimationCallbackType.Remove;
    public readonly bool IsCallbackTypeRepeat => (CallbackType & AnimationCallbackType.Repeat) == AnimationCallbackType.Repeat;
    public readonly bool IsCallbackTypeCycle => (CallbackType & AnimationCallbackType.Cycle) == AnimationCallbackType.Cycle;
  }
}
