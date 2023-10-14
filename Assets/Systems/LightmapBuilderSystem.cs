using SS.Resources;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using static Unity.Mathematics.math;

namespace SS.System {
  [UpdateInGroup(typeof(PresentationSystemGroup))]
  public partial class LightmapBuilderSystem : SystemBase {
    private EntityQuery mapElementQuery;

    private Texture2D lightmap;

    protected override async void OnCreate() {
      base.OnCreate();

      RequireForUpdate<LevelInfo>();
      RequireForUpdate<AsyncLoadTag>();

      mapElementQuery = GetEntityQuery(new EntityQueryDesc {
        All = new ComponentType[] {
          ComponentType.ReadOnly<TileLocation>(),
          ComponentType.ReadOnly<MapElement>(),
          ComponentType.ReadOnly<LightmapRebuildTag>()
        }
      });

      lightmap = await Services.LightmapTexture;

      EntityManager.AddComponent<AsyncLoadTag>(SystemHandle);
    }

    protected override void OnUpdate() {
      if (mapElementQuery.IsEmptyIgnoreFilter) return;

      var lightmapJob = new UpdateLightmapJob {
        tileLocationTypeHandle = GetComponentTypeHandle<TileLocation>(),
        mapElementTypeHandle = GetComponentTypeHandle<MapElement>(),

        lightmap = lightmap.GetRawTextureData<byte>(),
        stride = lightmap.format == TextureFormat.RG16 ? 2 : 4,
        levelInfo = SystemAPI.GetSingleton<LevelInfo>()
      };

      Dependency = lightmapJob.ScheduleParallel(mapElementQuery, Dependency);

      CompleteDependency();

      EntityManager.RemoveComponent<LightmapRebuildTag>(mapElementQuery);

      lightmap.Apply(false, false);
    }

    private struct AsyncLoadTag : IComponentData { }
  }

  [BurstCompile]
  struct UpdateLightmapJob : IJobChunk {
    [ReadOnly] public ComponentTypeHandle<TileLocation> tileLocationTypeHandle;
    [ReadOnly] public ComponentTypeHandle<MapElement> mapElementTypeHandle;
    [WriteOnly, NativeDisableParallelForRestriction] public NativeArray<byte> lightmap;
    [ReadOnly] public int stride;
    [ReadOnly] public LevelInfo levelInfo;

    public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask) {
      var tileLocations = chunk.GetNativeArray(ref tileLocationTypeHandle);
      var mapElements = chunk.GetNativeArray(ref mapElementTypeHandle);

      for (int i = 0; i < chunk.Count; ++i) {
        var tileLocation = tileLocations[i];
        var mapElement = mapElements[i];

        var pixelIndex = (tileLocation.Y * levelInfo.Width + tileLocation.X) * stride;

        lightmap[pixelIndex] = (byte)(clamp(mapElement.ShadeFloor - mapElement.ShadeFloorModifier, 0, 0x0F) << 4);
        lightmap[pixelIndex + 1] = (byte)(clamp(mapElement.ShadeCeiling - mapElement.ShadeCeilingModifier, 0, 0x0F) << 4);
      }
    }
  }

  public struct LightmapRebuildTag : IComponentData { }
}
