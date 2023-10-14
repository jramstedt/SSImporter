using SS.Resources;
using System;
using UnityEngine;
using Unity.IO.LowLevel.Unsafe;
using Unity.Rendering;

namespace SS {
  public static class Services {
    public static readonly IResHandle<Palette> Palette;
    public static readonly IResHandle<ShadeTableData> ShadeTable;
    public static readonly IResHandle<Texture2D> ColorLookupTableTexture;
    public static readonly IResHandle<Texture2D> LightmapTexture;
    public static readonly IResHandle<TexturePropertiesData> TextureProperties;
    public static readonly IResHandle<Resources.ObjectProperties> ObjectProperties;

    static Services() {
      Debug.Log(@"Services");

      Palette = Res.Load<Palette>(0x02BC);

      ShadeTable = Res.Open<ShadeTableData>(Res.dataPath + @"\SHADTABL.DAT");

      ColorLookupTableTexture = new CreateColorLookupTable();
      LightmapTexture = new CreateLightmap();

      TextureProperties = Res.Open<TexturePropertiesData>(Res.dataPath + @"\TEXTPROP.DAT");

      ObjectProperties = Res.OpenObjectProperties(Res.dataPath + @"\OBJPROP.DAT");
    }

    private class CreateColorLookupTable : LoaderBase<Texture2D> {
      public CreateColorLookupTable() {
        CreateColorLookupTableAsync();
      }

      private async void CreateColorLookupTableAsync() {
        var palette = await Palette;
        var shadeTable = await ShadeTable;

        Texture2D colorLookupTable = new(256, 16, TextureFormat.RGBA32, false, false) {
          name = @"Color lookup table",
          filterMode = FilterMode.Point,
          wrapMode = TextureWrapMode.Clamp
        };

        var textureData = colorLookupTable.GetRawTextureData<Color32>();

        for (int i = 0; i < textureData.Length; ++i)
          textureData[i] = palette[shadeTable[i]];

        colorLookupTable.Apply(false, false);

        Shader.SetGlobalTexture(Shader.PropertyToID(@"_CLUT"), colorLookupTable);

        InvokeCompletionEvent(colorLookupTable);
      }
    }

    private class CreateLightmap : LoaderBase<Texture2D> {
      public CreateLightmap () {
        Texture2D lightmap;
        if (SystemInfo.SupportsTextureFormat(TextureFormat.RG16))
          lightmap = new(64, 64, TextureFormat.RG16, false, true);
        else if (SystemInfo.SupportsTextureFormat(TextureFormat.RGBA32))
          lightmap = new(64, 64, TextureFormat.RGBA32, false, true);
        else
          throw new Exception("No supported TextureFormat found.");

        lightmap.name = @"Lightmap";

        Shader.SetGlobalTexture(Shader.PropertyToID(@"_LightGrid"), lightmap);

        InvokeCompletionEvent(lightmap);
      }
    }

    /*
     * TODO Caching and refcounting Res.Load
     * 
    private static Texture2D ColorLookupTable;
    private static Texture2D Lightmap;

    public static IResHandle<Palette> Palette => Res.Load<Palette>(0x02BC);
    public static IResHandle<ShadeTableData> ShadeTable => Res.Open<ShadeTableData>(Res.dataPath + @"\SHADTABL.DAT");
    public static IResHandle<Texture2D> ColorLookupTableTexture => CreateColorLookupTable();
    public static IResHandle<Texture2D> LightmapTexture => CreateLightmap();
    public static IResHandle<TexturePropertiesData> TextureProperties => Res.Open<TexturePropertiesData>(Res.dataPath + @"\TEXTPROP.DAT");
    public static IResHandle<Resources.ObjectProperties> ObjectProperties => Res.OpenObjectProperties(Res.dataPath + @"\OBJPROP.DAT");
    */

    /*
    private class CreateInversePaletteLookupTable : AsyncOperationBase<Texture2D> {
      protected override void Execute() {
        // TODO SupportsTextureFormat

        Texture2D inverseLookup = new(256, 3, TextureFormat.R8, false, false) {
          filterMode = FilterMode.Point,
          wrapMode = TextureWrapMode.Clamp
        };

        var textureData = inverseLookup.GetRawTextureData<byte>();

        var red = textureData.GetSubArray(0, 256);
        var green = textureData.GetSubArray(256, 256);
        var blue = textureData.GetSubArray(512, 256);
      }
    }
    */
  }
}
