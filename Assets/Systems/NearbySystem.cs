using SS.Resources;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine;
using static SS.TextureUtils;

namespace SS.System {
  [UpdateInGroup(typeof(VariableRateSimulationSystemGroup))]
  public partial struct NearbySystem : ISystem {
    private EntityQuery objectQuery;

    private EntityTypeHandle entityTypeHandle;
    private ComponentTypeHandle<ObjectInstance> objectInstanceTypeHandleRO;
    private ComponentTypeHandle<ObjectInstance.Decoration> decorationTypeHandleRO;
    
    private bool once;

    [BurstCompile]
    public void OnCreate(ref SystemState state) {
      state.RequireForUpdate<EndVariableRateSimulationEntityCommandBufferSystem.Singleton>();
      state.RequireForUpdate<Level>();
      state.RequireForUpdate<Hacker>();
      
      once = false;

      entityTypeHandle = state.GetEntityTypeHandle();
      objectInstanceTypeHandleRO = state.GetComponentTypeHandle<ObjectInstance>(true);
      decorationTypeHandleRO = state.GetComponentTypeHandle<ObjectInstance.Decoration>(true);
      
      objectQuery = new EntityQueryBuilder(Allocator.Temp)
        .WithAll<ObjectInstance, ObjectInstance.Decoration>() // TODO Currently only checks decorators. Are others needed?
        .Build(ref state);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state) {
      if (once) return;
      once = true;
      
      var ecbSingleton = SystemAPI.GetSingleton<EndVariableRateSimulationEntityCommandBufferSystem.Singleton>();
      var level = SystemAPI.GetSingleton<Level>();
      var player = SystemAPI.GetSingleton<Hacker>();
      
      entityTypeHandle.Update(ref state);
      objectInstanceTypeHandleRO.Update(ref state);
      decorationTypeHandleRO.Update(ref state);
      
      var commandBuffer = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
      
      var animationCommandListSystem = state.WorldUnmanaged.GetExistingUnmanagedSystem<AnimationCommandListSystem>();
      var animationCommandListSystemData = SystemAPI.GetComponent<AnimateObjectSystemData>(animationCommandListSystem); // TODO GetSingleton?

      var checkNearbyJob = new CheckNearbyJob {
        Player = player,
        Level = level,

        EntityTypeHandle = entityTypeHandle,
        ObjectInstanceTypeHandleRO = objectInstanceTypeHandleRO,
        DecorationTypeHandleRO = decorationTypeHandleRO,

        AnimationList = new AnimateObjectSystemData.Writer {
          Commands = animationCommandListSystemData.Commands.AsWriter()
        },
        
        CommandBuffer = commandBuffer.AsParallelWriter()
      };

      state.Dependency = checkNearbyJob.ScheduleParallel(objectQuery, state.Dependency);
    }
    
    [BurstCompile]
    public void OnDestroy(ref SystemState state) { }
  }

  [BurstCompile]
  struct CheckNearbyJob : IJobChunk {
    private const int STOCHASTIC_SHODAN_MASK = 0xF;

    [ReadOnly] public Hacker Player;
    [ReadOnly] public Level Level;

    [ReadOnly] public EntityTypeHandle EntityTypeHandle;
    [ReadOnly] public ComponentTypeHandle<ObjectInstance> ObjectInstanceTypeHandleRO;
    [ReadOnly] public ComponentTypeHandle<ObjectInstance.Decoration> DecorationTypeHandleRO;

    public AnimateObjectSystemData.Writer AnimationList;
    
    [WriteOnly] public EntityCommandBuffer.ParallelWriter CommandBuffer;

    public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask) {
      var objectEntities = chunk.GetNativeArray(EntityTypeHandle);
      var objectInstances = chunk.GetNativeArray(ref ObjectInstanceTypeHandleRO);
      var decorationDatas = chunk.GetNativeArray(ref DecorationTypeHandleRO);

      var playerIndex = Player.playerObjectIndex;
      var playerEntity = Level.ObjectInstances.Value[playerIndex];

      AnimationList.Commands.BeginForEachIndex(JobsUtility.ThreadIndex);

      for (int i = 0; i < chunk.Count; ++i) {
        var objectEntity = objectEntities[i];
        var objectInstance = objectInstances[i];
        var decorationData = decorationDatas[i];

        if (!objectInstance.Active) continue;
        if (objectEntity == playerEntity) continue;

        if (objectInstance.Triple == 0x70006 /* TV_TRIPLE */ ||
            objectInstance.Triple == 0x70007 /* MONITOR2_TRIPLE */ ||
            objectInstance.Triple == 0x70206 /* SCREEN_TRIPLE */ ||
            objectInstance.Triple == 0x70209 /* BIGSCREEN_TRIPLE */ ||
            objectInstance.Triple == 0x70208 /* SUPERSCREEN_TRIPLE */) {

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

            CommandBuffer.SetComponent(unfilteredChunkIndex, objectEntity, decorationData);
            CommandBuffer.SetComponent(unfilteredChunkIndex, objectEntity, objectInstance);

            AnimationList.AddAnimation(decorationData.Link.ObjectIndex, false, false, false, 0, AnimationData.Callback.UnShodanize, 0, AnimationData.AnimationCallbackType.Remove);
            // }
          }
        }
      }

      AnimationList.Commands.EndForEachIndex();
    }
  }
}
