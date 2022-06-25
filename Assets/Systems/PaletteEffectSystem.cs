using SS.Resources;
using Unity.Burst;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using UnityEngine;

namespace SS.System {
  [UpdateInGroup(typeof(PresentationSystemGroup))]
  public partial class PaletteEffectSystem : SystemBase {
    private Texture2D clut;
    private ShadeTableData shadeTable;
    private NativeArray<Color32> palette;

    private EntityQuery paletteEffectQuery;
    private int lastTicks;

    protected override async void OnCreate() {
      base.OnCreate();
      
      var rawPalette = await Services.Palette;
      palette = rawPalette.ToNativeArray();
      shadeTable = await Services.ShadeTable;
      clut = await Services.ColorLookupTableTexture;

      paletteEffectQuery = GetEntityQuery(new EntityQueryDesc {
        All = new ComponentType[] {
          ComponentType.ReadWrite<PaletteEffect>()
        }
      });

      lastTicks = TimeUtils.SecondsToSlowTicks(Time.ElapsedTime);
    }

    protected override void OnUpdate() {
      var ticks = TimeUtils.SecondsToSlowTicks(Time.ElapsedTime);
      var delta = ticks - lastTicks;

      if(delta <= 0) return;
      lastTicks = ticks;

      var effectJob = new EffectJob {
        paletteEffectTypeHandle = GetComponentTypeHandle<PaletteEffect>(),
        palette = palette,
        deltaTicks = delta
      };

      var effect = effectJob.ScheduleParallel(paletteEffectQuery, dependsOn: Dependency);
      Dependency = effect;

      effect.Complete();

      var textureData = clut.GetRawTextureData<Color32>();
      for (int i = 0; i < textureData.Length; ++i)
        textureData[i] = palette[shadeTable[i]];

      clut.Apply(false, false);
    }

    protected override void OnDestroy() {
      base.OnDestroy();

      palette.Dispose();
    }

    [BurstCompile]
    struct EffectJob : IJobEntityBatch {
      public ComponentTypeHandle<PaletteEffect> paletteEffectTypeHandle;

      [NativeDisableParallelForRestriction] public NativeArray<Color32> palette;

      [ReadOnly] public int deltaTicks;

      public void Execute(ArchetypeChunk batchInChunk, int batchIndex) {
        var paletteEffects = batchInChunk.GetNativeArray(paletteEffectTypeHandle);

        using (var tmpPal = new NativeArray<Color32>(256, Allocator.Temp)) {
          for (int i = 0; i < batchInChunk.Count; ++i) {
            var paletteEffect = paletteEffects[i];

            var colors = paletteEffect.Last - paletteEffect.First + 1;

            var frameDeltaTime = deltaTicks + paletteEffect.TimeRemaining;
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
