using SS.Resources;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;

namespace SS.System {
  [UpdateInGroup(typeof(PresentationSystemGroup))]
  public partial class PaletteEffectSystem : SystemBase {
    private Texture2D clut;
    private ShadeTableData shadeTable;
    private NativeArray<Color32> palette;

    private EntityQuery paletteEffectQuery;
    private int lastTicks;

    protected override void OnCreate() {
      base.OnCreate();
      
      var rawPalette = Services.Palette.WaitForCompletion();
      palette = rawPalette.ToNativeArray();
      shadeTable = Services.ShadeTable.WaitForCompletion();
      clut = Services.ColorLookupTableTexture.WaitForCompletion();

      paletteEffectQuery = GetEntityQuery(new EntityQueryDesc {
        All = new ComponentType[] {
          ComponentType.ReadWrite<PaletteEffect>()
        }
      });

      lastTicks = TimeUtils.SecondsToSlowTicks(SystemAPI.Time.ElapsedTime);
    }

    protected override void OnUpdate() {
      var ticks = TimeUtils.SecondsToSlowTicks(SystemAPI.Time.ElapsedTime);
      var delta = ticks - lastTicks;

      if(delta <= 0) return;
      lastTicks = ticks;

      var effectJob = new EffectJob {
        paletteEffectTypeHandle = GetComponentTypeHandle<PaletteEffect>(),
        palette = palette,
        deltaTicks = delta
      };
      Dependency = effectJob.ScheduleParallel(paletteEffectQuery, Dependency);

      var textureData = clut.GetRawTextureData<Color32>();
      var fillClutJob = new FillClutJob {
        palette = palette,
        shadeTable = shadeTable,
        textureData = textureData
      };
      Dependency = fillClutJob.Schedule(textureData.Length, 256, Dependency);

      CompleteDependency();
      clut.Apply(false, false);
    }

    protected override void OnDestroy() {
      base.OnDestroy();

      palette.Dispose();
    }

    [BurstCompile]
    struct EffectJob : IJobChunk {
      public ComponentTypeHandle<PaletteEffect> paletteEffectTypeHandle;

      [NativeDisableParallelForRestriction] public NativeArray<Color32> palette;

      [ReadOnly] public int deltaTicks;

      public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask) {
        var paletteEffects = chunk.GetNativeArray(paletteEffectTypeHandle);

        using (var tmpPal = new NativeArray<Color32>(256, Allocator.Temp)) {
          for (int i = 0; i < chunk.Count; ++i) {
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

    [BurstCompile]
    struct FillClutJob : IJobParallelFor {
      [ReadOnly] public NativeArray<Color32> palette;
      [ReadOnly] public ShadeTableData shadeTable;
      [WriteOnly] public NativeArray<Color32> textureData;

      public void Execute(int index) {
        textureData[index] = palette[shadeTable[index]];
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
