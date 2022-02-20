
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using SS.Resources;
using Unity.Burst;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

namespace SS.System {
  public sealed class AnimateTexturesSystem : SystemBase {
    public BitmapSet[] textures;
    public TextureProperties[] textureProperties;
    public Dictionary<ushort, Material> mapMaterial;
    public NativeArray<Entity> textureAnimationEntities; // Ugly
    
    private EntityQuery textureAnimationQuery;

    protected override void OnCreate() {
      base.OnCreate();

      textureAnimationQuery = GetEntityQuery(new EntityQueryDesc {
        All = new ComponentType[] {
          ComponentType.ReadWrite<TextureAnimationData>()
        }
      });
    }

    protected override void OnUpdate() {
      var animateTexturesJob = new AnimateTexturesJob {
        textureAnimationTypeHandle = GetComponentTypeHandle<TextureAnimationData>(),
        timeData = World.Time
      };

      var animateTextures = animateTexturesJob.ScheduleParallel(textureAnimationQuery, dependsOn: Dependency);
      Dependency = animateTextures;

      animateTextures.Complete();
      
      foreach (var (textureIndex, material) in mapMaterial) {
        var textureAnimationEntity = textureAnimationEntities[textureProperties[textureIndex].AnimationGroup];
        var textureAnimation = GetComponent<TextureAnimationData>(textureAnimationEntity);

        if (textureAnimation.TotalFrames == 0) continue;

        var newTextureOffset = (textureAnimation.CurrentFrame + textureProperties[textureIndex].GroupPosition) % textureAnimation.TotalFrames;
        var newBitmapSet = textures[textureIndex + newTextureOffset];

        material.SetTexture(Shader.PropertyToID(@"_BaseMap"), newBitmapSet.Texture);
        if (newBitmapSet.Transparent) material.EnableKeyword(@"TRANSPARENCY_ON");
        else material.DisableKeyword(@"TRANSPARENCY_ON");
      }
    }

    struct AnimateTexturesJob : IJobEntityBatch {
      public ComponentTypeHandle<TextureAnimationData> textureAnimationTypeHandle;

      [ReadOnly] public TimeData timeData;

      [BurstCompile]
      public void Execute(ArchetypeChunk batchInChunk, int batchIndex) {
        var textureAnimations = batchInChunk.GetNativeArray(textureAnimationTypeHandle);

        var deltaTime = (ushort)(timeData.DeltaTime * 1000);
        
        for (int i = 0; i < batchInChunk.Count; ++i) {
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
                if (textureAnimation.Flags.HasFlag(TextureAnimationData.FlagMask.Cyclic)) {
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

    public bool IsCyclic => Flags.HasFlag(FlagMask.Cyclic);
    public bool IsReversing => Flags.HasFlag(FlagMask.Reversing);
  }
}