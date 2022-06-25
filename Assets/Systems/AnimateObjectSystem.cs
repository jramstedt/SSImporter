using System.Runtime.InteropServices;
using SS.Resources;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Core;
using Unity.Entities;

namespace SS.System {
  public partial class AnimateObjectSystem : SystemBase {
    private EntityQuery animationQuery;

    protected override void OnCreate() {
      base.OnCreate();

    animationQuery = GetEntityQuery(new EntityQueryDesc {
        All = new ComponentType[] {
          ComponentType.ReadWrite<AnimationData>()
        }
      });
    }

    protected override void OnUpdate() {
      var level = GetSingleton<Level>();

      var animateJob = new AnimateAnimationJob {
        animationTypeHandle = GetComponentTypeHandle<AnimationData>(),
        ObjectInstancesBlobAsset = level.ObjectInstances,
        timeData = Time,
        InstanceFromEntity = GetComponentDataFromEntity<ObjectInstance>(),
        DecorationFromEntity = GetComponentDataFromEntity<ObjectInstance.Decoration>(),
        ItemFromEntity = GetComponentDataFromEntity<ObjectInstance.Item>(),
        EnemyFromEntity = GetComponentDataFromEntity<ObjectInstance.Enemy>(),
      };

      var animate = animateJob.ScheduleParallel(animationQuery, dependsOn: Dependency);
      Dependency = animate;
    }

    [BurstCompile]
    struct AnimateAnimationJob : IJobEntityBatch {
      public ComponentTypeHandle<AnimationData> animationTypeHandle;

      [ReadOnly] public BlobAssetReference<BlobArray<Entity>> ObjectInstancesBlobAsset;
      [ReadOnly] public BlobAssetReference<ObjectDatas> ObjectDatasBlobAsset;

      [ReadOnly] public TimeData timeData;
      [ReadOnly] public byte Level;

      [NativeDisableContainerSafetyRestriction] public ComponentDataFromEntity<ObjectInstance> InstanceFromEntity;
      [NativeDisableContainerSafetyRestriction] public ComponentDataFromEntity<ObjectInstance.Decoration> DecorationFromEntity;
      [NativeDisableContainerSafetyRestriction] public ComponentDataFromEntity<ObjectInstance.Item> ItemFromEntity;
      [NativeDisableContainerSafetyRestriction] public ComponentDataFromEntity<ObjectInstance.Enemy> EnemyFromEntity;

      public void Execute(ArchetypeChunk batchInChunk, int batchIndex) {
        var animations = batchInChunk.GetNativeArray(animationTypeHandle);

        for (int i = 0; i < batchInChunk.Count; ++i) {
          var animation = animations[i];
          var entity = ObjectInstancesBlobAsset.Value[animation.ObjectIndex];
          var instanceData = InstanceFromEntity[entity];

          var frameCount = 1;
          if (instanceData.Class == ObjectClass.DoorAndGrating) {
            // GetResourceBlockCount
          } else if (instanceData.Class == ObjectClass.Decoration) {
            var decoration = DecorationFromEntity[entity];
            frameCount = decoration.Cosmetic;
            if (frameCount == 0) frameCount = 1;
          } else if (instanceData.Class == ObjectClass.Item) {
            var item = ItemFromEntity[entity];
            frameCount = item.Cosmetic;
            if (frameCount == 0) frameCount = 4;
          } else if (instanceData.Class == ObjectClass.Enemy) {
            const int MAX_TELEPORT_FRAME = 10;
            const int DIEGO_DEATH_BATTLE_LEVEL = 8;

            var enemy = EnemyFromEntity[entity];

            if (instanceData.Triple == 0xe0401 && enemy.Posture == ObjectInstance.Enemy.PostureType.Death && Level != DIEGO_DEATH_BATTLE_LEVEL) // DIEGO_TRIPLE
              frameCount = MAX_TELEPORT_FRAME;
          } else {
            var baseData = ObjectDatasBlobAsset.Value.BasePropertyData(instanceData.Triple);
            frameCount = baseData.BitmapFrameCount;
          }
        }
      }
    }
  }

  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct AnimationData : IComponentData {
    public const int MAX_ANIMLIST_SIZE = 64;

    public enum AnimationFlags : byte {
        Repeat = 0x01,
        Reverse,
        Cycle
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
    public ushort Speed;
  }
}
