using System.Threading.Tasks;
using SS.ObjectProperties;
using SS.Resources;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceLocations;
using SpriteLibraryClass = SS.Resources.SpriteLibrary;

namespace SS {
  public static class Services {
    public static readonly Task<Palette> Palette;
    public static readonly Task<ShadeTableData> ShadeTable;
    public static readonly Task<Texture2D> ColorLookupTableTexture;
    public static readonly Task<TexturePropertiesData> TextureProperties;
    public static readonly Task<Resources.ObjectProperties> ObjectProperties;
    public static readonly Task<Resources.SpriteLibrary> SpriteLibrary;

    static Services() {
      Debug.Log(@"Services");

      var dataPath = Initialization.dataPath;

      var paletteOp = Addressables.LoadAssetAsync<Palette>(0x02BC);
      Palette = paletteOp.Task;

      var shadetableOp = Addressables.LoadAssetAsync<ShadeTableData>(new ResourceLocationBase(@"SHADTABL.DAT", dataPath + @"\SHADTABL.DAT", typeof(RawDataProvider).FullName, typeof(ShadeTableData)));
      ShadeTable = shadetableOp.Task;

      ColorLookupTableTexture = CreateColorLookupTable();

      var texturePropertiesOp = Addressables.LoadAssetAsync<TexturePropertiesData>(new ResourceLocationBase(@"TEXTPROP.DAT", dataPath + @"\TEXTPROP.DAT", typeof(RawDataProvider).FullName, typeof(TexturePropertiesData)));
      TextureProperties = texturePropertiesOp.Task;

      var objectPropertiesOp = Addressables.LoadAssetAsync<Resources.ObjectProperties>(new ResourceLocationBase(@"OBJPROP.DAT", dataPath + @"\OBJPROP.DAT", typeof(ObjectPropertiesProvider).FullName, typeof(Resources.ObjectProperties)));
      ObjectProperties = objectPropertiesOp.Task;

      SpriteLibrary = SpriteLibraryClass.ConstructInstance();
    }

    private static async Task<Texture2D> CreateColorLookupTable() {
      var palette = await Palette;
      var shadeTable = await ShadeTable;

      Texture2D clut = new Texture2D(256, 16, TextureFormat.RGBA32, false, false);
      clut.filterMode = FilterMode.Point;
      clut.wrapMode = TextureWrapMode.Clamp;

      var textureData = clut.GetRawTextureData<Color32>();

      for (int i = 0; i < textureData.Length; ++i)
        textureData[i] = palette[shadeTable[i]];

      clut.Apply(false, false);
      return clut;
    }
  }
}
