using SS.Resources;
using Unity.Burst;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using UnityEngine;

namespace SS.System {
  [UpdateInGroup(typeof(PresentationSystemGroup))]
  public sealed class PaletteEffectSystem : SystemBase {
    public Texture2D clut;
    public ShadeTableData shadeTable;
    public NativeArray<Color32> palette;

    private EntityQuery paletteEffectQuery;

    protected override void OnCreate() {
      paletteEffectQuery = GetEntityQuery(new EntityQueryDesc {
        All = new ComponentType[] {
          ComponentType.ReadWrite<PaletteEffect>()
        }
      });
    }

    protected override void OnUpdate() {
      var effectJob = new EffectJob {
        paletteEffectTypeHandle = GetComponentTypeHandle<PaletteEffect>(),
        palette = palette,
        timeData = World.Time
      };

      var effect = effectJob.ScheduleParallel(paletteEffectQuery, dependsOn: Dependency);
      Dependency = effect;

      effect.Complete();

      var textureData = clut.GetRawTextureData<Color32>();
      for (int i = 0; i < textureData.Length; ++i)
        textureData[i] = palette[shadeTable[i]];

      clut.Apply(false, false);
    }

    [BurstCompile]
    struct EffectJob : IJobEntityBatch {
      public ComponentTypeHandle<PaletteEffect> paletteEffectTypeHandle;

      [NativeDisableParallelForRestriction] public NativeArray<Color32> palette;

      [ReadOnly] public TimeData timeData;

      public void Execute(ArchetypeChunk batchInChunk, int batchIndex) {
        var paletteEffects = batchInChunk.GetNativeArray(paletteEffectTypeHandle);

        var deltaTime = (ushort)(timeData.DeltaTime * 1000f);

        using (var tmpPal = new NativeArray<Color32>(256, Allocator.Temp)) {
          for (int i = 0; i < batchInChunk.Count; ++i) {
            var paletteEffect = paletteEffects[i];

            var colors = paletteEffect.Last - paletteEffect.First + 1;

            var frameDeltaTime = deltaTime + paletteEffect.TimeRemaining;
            var addToColor = (frameDeltaTime / paletteEffect.FrameTime) % colors;
            paletteEffect.TimeRemaining = (ushort)(frameDeltaTime % paletteEffect.FrameTime);

            if (addToColor > 0) {
              NativeArray<Color32>.Copy(palette, paletteEffect.First + addToColor, tmpPal, paletteEffect.First, colors - addToColor);
              NativeArray<Color32>.Copy(palette, paletteEffect.First, tmpPal, paletteEffect.First + colors - addToColor, addToColor);
              NativeArray<Color32>.Copy(tmpPal, paletteEffect.First, palette, paletteEffect.First, colors);
            }

            paletteEffects[i] = paletteEffect;
          }
        }
      }
    }
  }

  struct PaletteEffect : IComponentData {
    public byte First;
    public byte Last;
    public byte FrameTime;
    public ushort TimeRemaining;
  }
}
