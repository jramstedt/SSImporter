using SS.Resources;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SS.System {
  [UpdateInGroup(typeof(PresentationSystemGroup))]
  public partial class AnimateTexturesSystem : SystemBase {
    private EntityQuery textureAnimationQuery;

    private ComponentLookup<TextureAnimationData> textureAnimationDataLookupRO;

    private EntitiesGraphicsSystem entitiesGraphicsSystem;
    private MaterialProviderSystem materialProviderSystem;

    private TexturePropertiesData allTextureProperties;

    protected override async void OnCreate() {
      base.OnCreate();

      RequireForUpdate<Level>();
      RequireForUpdate<AsyncLoadTag>();

      textureAnimationQuery = new EntityQueryBuilder(Allocator.Temp)
        .WithAllRW<TextureAnimationData>()
        .Build(this);

      textureAnimationDataLookupRO = GetComponentLookup<TextureAnimationData>(true);

      entitiesGraphicsSystem = World.GetOrCreateSystemManaged<EntitiesGraphicsSystem>();
      materialProviderSystem = World.GetOrCreateSystemManaged<MaterialProviderSystem>();

      allTextureProperties = await Services.TextureProperties;

      EntityManager.AddComponent<AsyncLoadTag>(SystemHandle);
    }

    protected override void OnDestroy() {
      base.OnDestroy();
    }

    protected override void OnUpdate() {
      var animateTexturesJob = new AnimateTexturesJob {
        textureAnimationTypeHandleRW = GetComponentTypeHandle<TextureAnimationData>(),
        timeData = SystemAPI.Time
      };

      Dependency = animateTexturesJob.ScheduleParallel(textureAnimationQuery, Dependency);

      CompleteDependency();

      var level = SystemAPI.GetSingleton<Level>();
      var textureMap = level.TextureMap;
      var textureAnimationEntities = level.TextureAnimations;

      textureAnimationDataLookupRO.Update(this);

      for (byte textureMapIndex = 0; textureMapIndex < TextureMap.NUM_LOADED_TEXTURES; ++textureMapIndex) {
        ushort textureIndex = textureMap[textureMapIndex];
        var textureProperties = allTextureProperties[textureIndex];

        var textureAnimationEntity = textureAnimationEntities.Value[textureProperties.AnimationGroup];
        var textureAnimation = textureAnimationDataLookupRO[textureAnimationEntity];

        if (textureAnimation.TotalFrames == 0) continue;

        var newTextureOffset = (textureAnimation.CurrentFrame + textureProperties.GroupPosition) % textureAnimation.TotalFrames;
        var newTextureMapIndex = math.min(textureMapIndex + newTextureOffset, TextureMap.NUM_LOADED_TEXTURES - 1); // Alpha grove has unused texture with loop at the end of the list. Caused overflow here.

        var newMaterialID = materialProviderSystem.GetTextureMaterial(textureMap[newTextureMapIndex]);
        var newBitmapSetOp = materialProviderSystem.GetBitmapLoader(newMaterialID);

        if (!newBitmapSetOp.IsCompleted) continue;
        var newBitmapSet = newBitmapSetOp.Result;

        var materialID = materialProviderSystem.GetTextureMaterial(textureIndex);
        var material = entitiesGraphicsSystem.GetMaterial(materialID);

        material.SetTexture(MaterialProviderSystem.shaderTextureName, newBitmapSet.Texture);

        if (newBitmapSet.Description.Transparent)
          material.EnableKeyword(ShaderKeywordStrings._ALPHATEST_ON);
        else
          material.DisableKeyword(ShaderKeywordStrings._ALPHATEST_ON);
      }
    }

    [BurstCompile]
    struct AnimateTexturesJob : IJobChunk {
      public ComponentTypeHandle<TextureAnimationData> textureAnimationTypeHandleRW;

      [ReadOnly] public TimeData timeData;

      public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask) {
        var textureAnimations = chunk.GetNativeArray(ref textureAnimationTypeHandleRW);

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

    private struct AsyncLoadTag : IComponentData { }
  }
}
