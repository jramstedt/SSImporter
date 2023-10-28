using SS.ObjectProperties;
using SS.Resources;
using SS.System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.Rendering;

namespace SS {
  [BurstCompile]
  public static class TextureUtils {
    public const ushort CustomTextureIdBase = 2180;
    public const ushort ArtResourceIdBase = 1350;
    public const ushort DoorResourceIdBase = 2400;
    public const ushort ModelTextureIdBase = 475;
    public const ushort SmallTextureIdBase = 321;
    public const ushort RepulsorResourceIdBase = 80;
    public const ushort GraffitiResourceIdBase = 79;
    public const ushort IconResourceIdBase = 78;

    public const int TPOLY_INDEX_BITS = 7;
    public const int INDEX_MASK = 0x007F;
    public const int TPOLY_TYPE_BITS = 2;
    public const int TYPE_MASK = 0x0180;
    public const int SCALE_MASK = 0x0600;
    public const int STYLE_MASK = 0x0800;

    public const int RANDOM_TEXT_MAGIC_COOKIE = 0x7F;
    public const int REGULAR_STATIC_MAGIC_COOKIE = 0x77;
    public const int SHODAN_STATIC_MAGIC_COOKIE = 0x76;

    public const int NUM_HACK_CAMERAS = 8;

    [BurstCompile]
    public static int CalculateTextureData(
      in Entity entity,
      in Base baseProperties,
      in ObjectInstance instanceData,
      in Level level,
      in ComponentLookup<ObjectInstance> instanceLookup,
      in ComponentLookup<ObjectInstance.Decoration> decorationLookup,
      in bool isAnimating
    ) {

      var textureData = 0;

      var isIndirectable = instanceData.Class == ObjectClass.Decoration &&
        (baseProperties.DrawType == DrawType.TexturedPolygon || baseProperties.DrawType == DrawType.TerrainPolygon);

      //(instanceData.Class == ObjectClass.Decoration && baseProperties.DrawType == DrawType.TexturedPolygon) ||
      //instanceData.Triple == 0x70208 || // SUPERSCREEN_TRIPLE
      //instanceData.Triple == 0x70209 || // BIGSCREEN_TRIPLE
      //instanceData.Triple == 0x70206; // SCREEN_TRIPLE

      if (isIndirectable) { // Must be ObjectClass.Decoration
        var decorationData = decorationLookup.GetRefRO(entity).ValueRO;

        var data = decorationData.Data2;

        const int INDIRECTED_STUFF_INDICATOR_MASK = 0x1000;
        const int INDIRECTED_STUFF_DATA_MASK = 0xFFF;

        if (data != 0 || isAnimating) {
          if ((data & INDIRECTED_STUFF_INDICATOR_MASK) != 0) {
            var dataEntity = level.ObjectInstances.Value[(int)data & INDIRECTED_STUFF_DATA_MASK];
            var databObjectInstance = instanceLookup.GetRefRO(dataEntity).ValueRO;
            var dataDecorationInstance = decorationLookup.GetRefRO(dataEntity).ValueRO;

            textureData = (int)dataDecorationInstance.Data2 + databObjectInstance.Info.CurrentFrame;
          } else {
            textureData = (int)data + instanceData.Info.CurrentFrame;
          }
        } else if (instanceData.SubClass == 1 /* BIGSTUFF_SUBCLASS_FURNISHING */) {
          const int SECRET_FURNITURE_DEFAULT_O3DREP = 0x80;
          textureData = SECRET_FURNITURE_DEFAULT_O3DREP;
        }
      } else if (baseProperties.DrawType != DrawType.TerrainPolygon && baseProperties.DrawType != DrawType.TexturedPolygon) {
        textureData = baseProperties.BitmapIndex;

        if (baseProperties.DrawType != DrawType.Voxel && instanceData.Class != ObjectClass.DoorAndGrating && instanceData.Info.CurrentFrame != -1) {
          textureData += instanceData.Info.CurrentFrame;
        }
      }

      return textureData;
    }

    public static bool IsAnimated(ushort objectIndex, in NativeArray<AnimationData>.ReadOnly animationData) {
      for (var index = 0; index < animationData.Length; ++index)
        if (animationData[index].ObjectIndex == objectIndex) return true;

      return false;
    }

    [BurstCompile]
    public static BatchMaterialID GetResource(
      in Entity entity,
      in ObjectInstance instanceData,
      in Level level,
      in BlobAssetReference<ObjectDatas> objectProperties,
      in MaterialProviderSystem materialProviderSystem,
      in ComponentLookup<ObjectInstance> instanceLookup,
      in ComponentLookup<ObjectInstance.Decoration> decorationLookup,
      in NativeArray<AnimationData>.ReadOnly animationData,
      in bool decal,
      out ushort refWidthOverride
    ) {
      var baseProperties = objectProperties.Value.BasePropertyData(instanceData);

      refWidthOverride = 0;

      if (baseProperties.DrawType == DrawType.TerrainPolygon) {
        const int DESTROYED_SCREEN_ANIM_BASE = 0x1B;

        if (instanceData.Class == ObjectClass.Decoration) {
          var decorationData = decorationLookup.GetRefRO(entity).ValueRO; // TODO FIXME this is also called in CalculateTextureData
          var isAnimating = IsAnimated(decorationData.Link.ObjectIndex, animationData);
          var textureData = CalculateTextureData(entity, baseProperties, instanceData, level, instanceLookup, decorationLookup, isAnimating);

          if (instanceData.Triple == 0x70207) { // TMAP_TRIPLE
            refWidthOverride = 128;
            return materialProviderSystem.GetMaterial((ushort)(0x03E8 + level.TextureMap[textureData]), 0, true, decal);
          } else if (instanceData.Triple == 0x70208) { // SUPERSCREEN_TRIPLE
            var lightmapped = decorationData.Data2 == DESTROYED_SCREEN_ANIM_BASE + 3; // screen is full bright if not destroyed
            refWidthOverride = 128; // 1 << 7
            return materialProviderSystem.ParseTextureData(textureData, lightmapped, decal, out var textureType, out var scale);
          } else if (instanceData.Triple == 0x70209) { // BIGSCREEN_TRIPLE
            var lightmapped = decorationData.Data2 == DESTROYED_SCREEN_ANIM_BASE + 3; // screen is full bright if not destroyed
            refWidthOverride = 64; // 1 << 6
            return materialProviderSystem.ParseTextureData(textureData, lightmapped, decal, out var textureType, out var scale);
          } else if (instanceData.Triple == 0x70206) { // SCREEN_TRIPLE
            var lightmapped = decorationData.Data2 == DESTROYED_SCREEN_ANIM_BASE + 3; // screen is full bright if not destroyed
            refWidthOverride = 32; // 1 << 5
            return materialProviderSystem.ParseTextureData(textureData, lightmapped, decal, out var textureType, out var scale);
          } else {
            var materialID = materialProviderSystem.ParseTextureData(textureData, true, decal, out var textureType, out var scale);
            refWidthOverride = (ushort)(1 << scale);
            return materialID;
          }
        }
      } else if (baseProperties.DrawType == DrawType.FlatTexture) {
        if (instanceData.Class == ObjectClass.Decoration) {
          if (instanceData.Triple == 0x70203) { // WORDS_TRIPLE
            // TODO
            return BatchMaterialID.Null;
          } else if (instanceData.Triple == 0x70201) { // ICON_TRIPLE
            return materialProviderSystem.GetMaterial(IconResourceIdBase, (ushort)instanceData.Info.CurrentFrame, true, decal);
          } else if (instanceData.Triple == 0x70202) { // GRAF_TRIPLE
            return materialProviderSystem.GetMaterial(GraffitiResourceIdBase, (ushort)instanceData.Info.CurrentFrame, true, decal);
          } else if (instanceData.Triple == 0x7020a) { // REPULSWALL_TRIPLE
            return materialProviderSystem.GetMaterial(RepulsorResourceIdBase, (ushort)instanceData.Info.CurrentFrame, true, decal);
          }
        } else if (instanceData.Class == ObjectClass.DoorAndGrating) {
          // Debug.Log($"{DoorResourceIdBase} {objectProperties.ClassPropertyIndex(instanceData)} : {instanceData.Info.CurrentFrame}");
          return materialProviderSystem.GetMaterial((ushort)(DoorResourceIdBase + objectProperties.Value.ClassPropertyIndex(instanceData)), (ushort)instanceData.Info.CurrentFrame, true, decal);
        }
      }

      return BatchMaterialID.Null;
    }
  }

  public enum TextureType {
    Alt, // TPOLY_TYPE_ALT_TMAP
    Custom, // TPOLY_TYPE_CUSTOM_MAT
    Text, // TPOLY_TYPE_TEXT_BITMAP
    ScrollText // TPOLY_TYPE_SCROLL_TEXT
  }
}
