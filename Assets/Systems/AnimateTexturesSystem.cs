
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using SS.Resources;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

namespace SS.System {
  [UpdateInGroup(typeof(PresentationSystemGroup))]
  public partial class AnimateTexturesSystem : SystemBase {
    public BitmapSet[] textures;
    public TextureProperties[] textureProperties;
    public Dictionary<ushort, Material> mapMaterial;
    public NativeArray<Entity> textureAnimationEntities; // Ugly

    private EntityQuery textureAnimationQuery;

    private ComponentLookup<TextureAnimationData> textureAnimationDataLookup;

    protected override void OnCreate() {
      base.OnCreate();

      RequireForUpdate<Level>();

      textureAnimationQuery = GetEntityQuery(new EntityQueryDesc {
        All = new ComponentType[] {
          ComponentType.ReadWrite<TextureAnimationData>()
        }
      });

      this.textureAnimationDataLookup = GetComponentLookup<TextureAnimationData>();
    }

    protected override void OnUpdate() {
      this.textureAnimationDataLookup.Update(this);

      var animateTexturesJob = new AnimateTexturesJob {
        textureAnimationTypeHandle = GetComponentTypeHandle<TextureAnimationData>(),
        timeData = SystemAPI.Time
      };

      Dependency = animateTexturesJob.ScheduleParallel(textureAnimationQuery, Dependency);
      CompleteDependency();

      foreach (var (textureIndex, material) in mapMaterial) {
        var textureAnimationEntity = textureAnimationEntities[textureProperties[textureIndex].AnimationGroup];
        var textureAnimation = this.textureAnimationDataLookup[textureAnimationEntity];

        if (textureAnimation.TotalFrames == 0) continue;

        var newTextureOffset = (textureAnimation.CurrentFrame + textureProperties[textureIndex].GroupPosition) % textureAnimation.TotalFrames;
        var newBitmapSet = textures[Mathf.Min(textureIndex + newTextureOffset, textures.Length - 1)]; // Alpha grove has unused texture with loop at the end of the list. Caused overflow here.

        material.SetTexture(Shader.PropertyToID(@"_BaseMap"), newBitmapSet.Texture);
        if (newBitmapSet.Description.Transparent) material.EnableKeyword(@"TRANSPARENCY_ON");
        else material.DisableKeyword(@"TRANSPARENCY_ON");
      }
    }

    protected override void OnDestroy() {
      base.OnDestroy();

      textureAnimationEntities.Dispose();
    }

    [BurstCompile]
    struct AnimateTexturesJob : IJobChunk {
      public ComponentTypeHandle<TextureAnimationData> textureAnimationTypeHandle;

      [ReadOnly] public TimeData timeData;

      public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask) {
        var textureAnimations = chunk.GetNativeArray(textureAnimationTypeHandle);

        var deltaTime = (ushort)(timeData.DeltaTime * 1000);
        
        for (int i = 0; i < chunk.Count; ++i) {
          var textureAnimation = textureAnimations[i];

          if (textureAnimation.TotalFrames == 0) continue;

          var frameDeltaTime = deltaTime + textureAnimation.TimeRemaining;
          var framesAnimated = frameDeltaTime / textureAnimation.FrameTime;
          textureAnimation.TimeRemaining = (ushort)(frameDeltaTime % textureAnimation.FrameTime);
          while (framesAnimated-- > 0) {
            if (textureAnimation.IsReversing) {
              --textureAnimation.CurrentFrame;
              if (textureAnimation.CurrentFrame < 0) {
                textureAnimation.Flags &= ~TextureAnimationData.FlagMask.Reversing;
                textureAnimation.CurrentFrame = 0;
              }
            } else {
              ++textureAnimation.CurrentFrame;
              if (textureAnimation.CurrentFrame >= textureAnimation.TotalFrames) {
                if (textureAnimation.IsCyclic) {
                  textureAnimation.Flags |= TextureAnimationData.FlagMask.Reversing;
                  textureAnimation.CurrentFrame = (sbyte)(textureAnimation.TotalFrames - 1);
                } else {
                  textureAnimation.CurrentFrame = 0;
                }
              }
            }
          }

          textureAnimations[i] = textureAnimation;
        }
      }
    }
  }

  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct TextureAnimationData : IComponentData {
    [Flags]
    public enum FlagMask : byte {
        Cyclic = 0x01, // Ping Pong
        Reversing = 0x80
    }

    public readonly ushort FrameTime;
    public ushort TimeRemaining;
    public sbyte CurrentFrame;
    public readonly byte TotalFrames;
    public FlagMask Flags;

    public bool IsCyclic => (Flags & FlagMask.Cyclic) == FlagMask.Cyclic;
    public bool IsReversing => (Flags & FlagMask.Reversing) == FlagMask.Reversing;
  }
}