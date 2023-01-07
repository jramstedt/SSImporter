using SS.ObjectProperties;
using SS.Resources;
using Unity.Burst;
using Unity.Entities;

namespace SS {
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
    public static int CalculateTextureData(in Base baseProperties, in ObjectInstance instanceData, in ObjectInstance.Decoration decorationData, in Level level, in ComponentLookup<ObjectInstance> instanceLookup, in ComponentLookup<ObjectInstance.Decoration> decorationLookup) {
      var textureData = 0;

      var isIndirectable = instanceData.Class == ObjectClass.Decoration &&
        (baseProperties.DrawType == DrawType.TexturedPolygon || baseProperties.DrawType == DrawType.TerrainPolygon);

      //(instanceData.Class == ObjectClass.Decoration && baseProperties.DrawType == DrawType.TexturedPolygon) ||
      //instanceData.Triple == 0x70208 || // SUPERSCREEN_TRIPLE
      //instanceData.Triple == 0x70209 || // BIGSCREEN_TRIPLE
      //instanceData.Triple == 0x70206; // SCREEN_TRIPLE

      if (isIndirectable) { // Must be ObjectClass.Decoration
        var data = decorationData.Data2;

        const int INDIRECTED_STUFF_INDICATOR_MASK = 0x1000;
        const int INDIRECTED_STUFF_DATA_MASK = 0xFFF;

        if (data != 0 /*|| animlist.ObjectIndex == objindex*/) {
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
      } else if (baseProperties.DrawType != DrawType.TerrainPolygon || baseProperties.DrawType != DrawType.TexturedPolygon) {
        textureData = baseProperties.BitmapIndex;

        if (baseProperties.DrawType != DrawType.Voxel && instanceData.Class != ObjectClass.DoorAndGrating && instanceData.Info.CurrentFrame != -1) {
          textureData += instanceData.Info.CurrentFrame;
        }
      }

      return textureData;
    }
  }

  public enum TextureType {
    Alt, // TPOLY_TYPE_ALT_TMAP
    Custom, // TPOLY_TYPE_CUSTOM_MAT
    Text, // TPOLY_TYPE_TEXT_BITMAP
    ScrollText // TPOLY_TYPE_SCROLL_TEXT
  }
}
