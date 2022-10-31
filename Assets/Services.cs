using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SS.ObjectProperties;
using SS.Resources;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Rendering;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace SS {
  public static class Services {
    public static readonly AsyncOperationHandle<Palette> Palette;
    public static readonly AsyncOperationHandle<ShadeTableData> ShadeTable;
    public static readonly AsyncOperationHandle<Texture2D> ColorLookupTableTexture;
    public static readonly AsyncOperationHandle<Texture2D> LightmapTexture;
    public static readonly AsyncOperationHandle<TexturePropertiesData> TextureProperties;
    public static readonly AsyncOperationHandle<Resources.ObjectProperties> ObjectProperties;

    static Services() {
      Debug.Log(@"Services");

      var dataPath = Initialization.dataPath;

      Palette = Addressables.LoadAssetAsync<Palette>(0x02BC);

      ShadeTable = Addressables.LoadAssetAsync<ShadeTableData>(new ResourceLocationBase(@"SHADTABL.DAT", dataPath + @"\SHADTABL.DAT", typeof(RawDataProvider).FullName, typeof(ShadeTableData)));

      ColorLookupTableTexture = Addressables.ResourceManager.StartOperation(new CreateColorLookupTable(Palette, ShadeTable), default);
      LightmapTexture = Addressables.ResourceManager.StartOperation(new CreateLightmap(), default);

      TextureProperties = Addressables.LoadAssetAsync<TexturePropertiesData>(new ResourceLocationBase(@"TEXTPROP.DAT", dataPath + @"\TEXTPROP.DAT", typeof(RawDataProvider).FullName, typeof(TexturePropertiesData)));

      ObjectProperties = Addressables.LoadAssetAsync<Resources.ObjectProperties>(new ResourceLocationBase(@"OBJPROP.DAT", dataPath + @"\OBJPROP.DAT", typeof(ObjectPropertiesProvider).FullName, typeof(Resources.ObjectProperties)));
    }

    private class CreateColorLookupTable : AsyncOperationBase<Texture2D>, IUpdateReceiver {
      private AsyncOperationHandle<Palette> paletteOp;
      private AsyncOperationHandle<ShadeTableData> shadeTableOp;

      public CreateColorLookupTable (AsyncOperationHandle<Palette> paletteOp, AsyncOperationHandle<ShadeTableData> shadeTableOp) : base() {
        this.paletteOp = paletteOp;
        this.shadeTableOp = shadeTableOp;
      }

      public override void GetDependencies(List<AsyncOperationHandle> dependencies) {
        dependencies.Add(paletteOp);
        dependencies.Add(shadeTableOp);
      }

      public void Update(float unscaledDeltaTime) {
        if (this.paletteOp.IsDone && this.shadeTableOp.IsDone) {
          var palette = paletteOp.Result;
          var shadeTable = shadeTableOp.Result;

          Texture2D clut = new Texture2D(256, 16, TextureFormat.RGBA32, false, false);
          clut.filterMode = FilterMode.Point;
          clut.wrapMode = TextureWrapMode.Clamp;

          var textureData = clut.GetRawTextureData<Color32>();

          for (int i = 0; i < textureData.Length; ++i)
            textureData[i] = palette[shadeTable[i]];

          clut.Apply(false, false);

          Complete(clut, true, null);
        }
      }

      protected override void Execute() {
        if (this.paletteOp.IsValid() && this.shadeTableOp.IsValid())
          Update(0f);
        else
          Complete(null, false, @"Invalid dependencies.");
      }
    }

    private class CreateLightmap : AsyncOperationBase<Texture2D> {
      protected override void Execute() {
        Texture2D lightmap;
        if (SystemInfo.SupportsTextureFormat(TextureFormat.RG16)) {
          lightmap = new Texture2D(64, 64, TextureFormat.RG16, false, true);
        } else if (SystemInfo.SupportsTextureFormat(TextureFormat.RGBA32)) {
          lightmap = new Texture2D(64, 64, TextureFormat.RGBA32, false, true);
        } else {
          Complete(null, false, new Exception("No supported TextureFormat found."));
          return;
        }

        lightmap.name = @"Lightmap";

        Complete(lightmap, true, null);
      }
    }
  }
}
