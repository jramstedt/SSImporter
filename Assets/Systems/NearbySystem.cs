using SS.Resources;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine;
using static SS.TextureUtils;

namespace SS.System {
  [UpdateInGroup(typeof(VariableRateSimulationSystemGroup))]
  public partial class NearbySystem : SystemBase {
    private EntityQuery objectQuery;

    private bool once = false;

    protected override void OnCreate() {
      base.OnCreate();

      RequireForUpdate<Level>();

      objectQuery = GetEntityQuery(new EntityQueryDesc {
        All = new ComponentType[] {
          ComponentType.ReadOnly<ObjectInstance>()
        }
      });
    }

    protected override void OnUpdate() {
      if (once) return;
      once = true;

      var animationCommandListSystem = World.GetExistingSystem<AnimationCommandListSystem>();
      var AnimationCommandListSystemData = SystemAPI.GetComponent<AnimateObjectSystemData>(animationCommandListSystem);

      var level = SystemAPI.GetSingleton<Level>();
      var player = SystemAPI.GetSingleton<Hacker>();

      var checkNearbyJob = new CheckNearbyJob {
        Player = player,
        Level = level,

        EntityTypeHandle = GetEntityTypeHandle(),
        ObjectInstanceTypeHandle = GetComponentTypeHandle<ObjectInstance>(),

        InstanceLookup = GetComponentLookup<ObjectInstance>(),
        DecorationLookup = GetComponentLookup<ObjectInstance.Decoration>(),

        animationList = new AnimateObjectSystemData.Writer {
          commands = AnimationCommandListSystemData.commands.AsWriter()
        }
      };

      Dependency = checkNearbyJob.ScheduleParallel(objectQuery, Dependency);
    }
  }

  [BurstCompile]
  struct CheckNearbyJob : IJobChunk {
    private const int STOCHASTIC_SHODAN_MASK = 0xF;

    [NativeSetThreadIndex] internal readonly int threadIndex;

    [ReadOnly] public Hacker Player;
    [ReadOnly] public Level Level;

    [ReadOnly] public EntityTypeHandle EntityTypeHandle;
    public ComponentTypeHandle<ObjectInstance> ObjectInstanceTypeHandle;

    [NativeDisableContainerSafetyRestriction, ReadOnly] public ComponentLookup<ObjectInstance> InstanceLookup;
    [NativeDisableContainerSafetyRestriction] public ComponentLookup<ObjectInstance.Decoration> DecorationLookup;

    public AnimateObjectSystemData.Writer animationList;

    public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask) {
      var objectEntities = chunk.GetNativeArray(EntityTypeHandle);
      var objectInstances = chunk.GetNativeArray(ref ObjectInstanceTypeHandle);

      var playerIndex = Player.playerObjectIndex;
      var playerEntity = Level.ObjectInstances.Value[playerIndex];

      animationList.commands.BeginForEachIndex(threadIndex);

      for (int i = 0; i < chunk.Count; ++i) {
        var objectEntity = objectEntities[i];
        var objectInstance = objectInstances[i];

        if (!objectInstance.Active) continue;
        if (objectEntity == playerEntity) continue;

        if (objectInstance.Triple == 0x70006 /* TV_TRIPLE */ ||
            objectInstance.Triple == 0x70007 /* MONITOR2_TRIPLE */ ||
            objectInstance.Triple == 0x70206 /* SCREEN_TRIPLE */ ||
            objectInstance.Triple == 0x70209 /* BIGSCREEN_TRIPLE */ ||
            objectInstance.Triple == 0x70208 /* SUPERSCREEN_TRIPLE */) {

          var decorationData = DecorationLookup[objectEntity];

          // var textureData = CalculateTextureData(objectInstance, decorationData, Level, InstanceLookup, DecorationLookup);
          var textureData = decorationData.Data2;
          var index = textureData & INDEX_MASK;
          var type = (TextureType)((textureData & TYPE_MASK) >> TPOLY_INDEX_BITS);

          if (type == TextureType.Custom && index == SHODAN_STATIC_MAGIC_COOKIE) {
            // if ((rand() & STOCHASTIC_SHODAN_MASK) == 1) {

              Debug.Log($"CheckNearbyJob SHODAN_STATIC_MAGIC_COOKIE {decorationData.Link.ObjectIndex}");

              decorationData.Data2 = Shodan.FIRST_SHODAN_ANIM;
              decorationData.Cosmetic = Shodan.NUM_SHODAN_FRAMES;
              objectInstance.Info.CurrentFrame = 0;

              DecorationLookup[objectEntity] = decorationData;
              objectInstances[i] = objectInstance;

              animationList.addAnimation(decorationData.Link.ObjectIndex, false, false, false, 0, AnimationData.Callback.UnShodanize, 0, AnimationData.AnimationCallbackType.Remove);
            // }
          }
        }
      }

      animationList.commands.EndForEachIndex();
    }
  }
}
