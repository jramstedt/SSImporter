using SS.Resources;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using static Unity.Mathematics.math;

namespace SS.System {
  [UpdateInGroup(typeof(PresentationSystemGroup))]
  public sealed class LightmapBuilderSystem : SystemBase {
    private EntityQuery mapElementQuery;

    public Texture2D lightmap;

    protected override void OnCreate() {
      base.OnCreate();

      RequireSingletonForUpdate<LevelInfo>();

      mapElementQuery = GetEntityQuery(new EntityQueryDesc {
        All = new ComponentType[] {
          ComponentType.ReadOnly<TileLocation>(),
          ComponentType.ReadOnly<MapElement>(),
          ComponentType.ReadOnly<LightmapRebuildTag>()
        }
      });
    }

    protected override void OnUpdate() {
      var entityCount = mapElementQuery.CalculateEntityCount();
      if (entityCount == 0) return;

      var lightmapJob = new UpdateLightmapJob {
        tileLocationTypeHandle = GetComponentTypeHandle<TileLocation>(),
        mapElementTypeHandle = GetComponentTypeHandle<MapElement>(),

        lightmap = lightmap.GetRawTextureData<byte>(),
        stride = lightmap.format == TextureFormat.RG16 ? 2 : 4,
        levelInfo = GetSingleton<LevelInfo>()
      };

      var buildMapElements = lightmapJob.ScheduleParallel(mapElementQuery, dependsOn: Dependency);
      Dependency = buildMapElements;

      buildMapElements.Complete();

      EntityManager.RemoveComponent<LightmapRebuildTag>(mapElementQuery);

      lightmap.Apply(false, false);
    }
  }

  [BurstCompile]
  struct UpdateLightmapJob : IJobEntityBatch {
    [ReadOnly] public ComponentTypeHandle<TileLocation> tileLocationTypeHandle;
    [ReadOnly] public ComponentTypeHandle<MapElement> mapElementTypeHandle;
    [WriteOnly, NativeDisableParallelForRestriction] public NativeArray<byte> lightmap;
    [ReadOnly] public int stride;
    [ReadOnly] public LevelInfo levelInfo;

    public void Execute(ArchetypeChunk batchInChunk, int batchIndex) {
      var tileLocations = batchInChunk.GetNativeArray(tileLocationTypeHandle);
      var mapElements = batchInChunk.GetNativeArray(mapElementTypeHandle);

      for (int i = 0; i < batchInChunk.Count; ++i) {
        var tileLocation = tileLocations[i];
        var mapElement = mapElements[i];

        var pixelIndex = (tileLocation.Y * levelInfo.Width + tileLocation.X) * stride;

        lightmap[pixelIndex] = (byte)(clamp(mapElement.ShadeFloor - mapElement.ShadeFloorModifier, 0, 0x0F));
        lightmap[pixelIndex+1] = (byte)(clamp(mapElement.ShadeCeiling - mapElement.ShadeCeilingModifier, 0, 0x0F));
      }
    }
  }

  public struct LightmapRebuildTag : IComponentData { }
}