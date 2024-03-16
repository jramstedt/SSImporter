using SS.Resources;
using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static SS.TextureUtils;
using static Unity.Mathematics.math;
using Random = Unity.Mathematics.Random;

namespace SS.System {
  [CreateAfter(typeof(EntitiesGraphicsSystem))]
  [UpdateInGroup(typeof(InitializationSystemGroup))]
  public partial class MaterialProviderSystem : SystemBase {
    public static readonly int shaderTextureName = Shader.PropertyToID(@"_Texture");

    private NativeParallelHashMap<byte, (float opacity, float purity, Color32 color)> translucencyTable;

    private NativeParallelHashMap<(uint resRef, bool lightmapped, bool decal), BatchMaterialID> bitmapMaterials;
    private NativeParallelHashMap<(int cameraIndex, bool lightmapped, bool decal), BatchMaterialID> cameraMaterials;
    private NativeParallelHashMap<(ushort wordIndex, byte color, byte style), BatchMaterialID> wordMaterials;
    private NativeParallelHashMap<(ushort textIndex, byte color, byte style, bool lightmapped, bool decal, bool scroll), BatchMaterialID> textMaterials;
    private NativeParallelHashMap<(UnityEngine.Hash128 textIndex, byte color, byte style, bool lightmapped, bool decal), BatchMaterialID> freeTextMaterials;
    private NativeParallelHashMap<byte, BatchMaterialID> translucentMaterials;

    private readonly Dictionary<BatchMaterialID, IResHandle<BitmapSet>> bitmapSetLoaders = new();

    private EntitiesGraphicsSystem entitiesGraphicsSystem;
    private NativeArray<Random> randoms;

    private BatchMaterialID colorMaterialID;
    private BatchMaterialID noiseMaterialID;
    private BatchMaterialID decalNoiseMaterialID;

    private Material clutMaterialTemplate;
    private Material clutDecalMaterialTemplate;
    private Material clutColorMaterialTemplate;
    private Material translucencyMaterialTemplate;

    private Material decalMaterialTemplate;
    private Material cameraMaterialTemplate;

    private CameraBitmapSet[] cameraBitmapSets;
    private BitmapDesc defaultBitmapDesc;

    protected override void OnCreate() {
      base.OnCreate();

      bitmapMaterials = new(1024, Allocator.Persistent);
      cameraMaterials = new(128, Allocator.Persistent);
      wordMaterials = new(ObjectConstants.NUM_OBJECTS_BIGSTUFF, Allocator.Persistent);
      textMaterials = new(ObjectConstants.NUM_OBJECTS_BIGSTUFF, Allocator.Persistent);
      freeTextMaterials = new(ObjectConstants.NUM_OBJECTS_BIGSTUFF, Allocator.Persistent);
      translucentMaterials = new(256, Allocator.Persistent);

      entitiesGraphicsSystem = World.GetOrCreateSystemManaged<EntitiesGraphicsSystem>();

      randoms = new NativeArray<Random>(JobsUtility.ThreadIndexCount, Allocator.Persistent);
      for (int i = 0; i < randoms.Length; ++i)
        randoms[i] = Random.CreateFromIndex((uint)i);

      clutMaterialTemplate = new Material(Shader.Find("Shader Graphs/URP CLUT"));

      clutDecalMaterialTemplate = new Material(Shader.Find(@"Shader Graphs/URP CLUT Decal"));

      translucencyMaterialTemplate = new Material(Shader.Find(@"Shader Graphs/ForceField"));

      clutColorMaterialTemplate = new Material(Shader.Find("Universal Render Pipeline/System Shock/CLUT Color"));
      clutColorMaterialTemplate.DisableKeyword(ShaderKeywordStrings._ALPHAPREMULTIPLY_ON);
      clutColorMaterialTemplate.DisableKeyword(ShaderKeywordStrings._SURFACE_TYPE_TRANSPARENT);
      clutColorMaterialTemplate.DisableKeyword(ShaderKeywordStrings._ALPHAMODULATE_ON);
      clutColorMaterialTemplate.EnableKeyword(@"_LIGHTGRID");
      clutColorMaterialTemplate.SetFloat(@"_BlendOp", (float)BlendOp.Add);
      clutColorMaterialTemplate.SetFloat(@"_SrcBlend", (float)BlendMode.One);
      clutColorMaterialTemplate.SetFloat(@"_DstBlend", (float)BlendMode.Zero);

      decalMaterialTemplate = new Material(Shader.Find(@"Shader Graphs/URP Decal"));

      cameraMaterialTemplate = new Material(Shader.Find("Universal Render Pipeline/Unlit")); // TODO Create color material with nearest lookup
      cameraMaterialTemplate.DisableKeyword(ShaderKeywordStrings._ALPHAPREMULTIPLY_ON);
      cameraMaterialTemplate.DisableKeyword(ShaderKeywordStrings._SURFACE_TYPE_TRANSPARENT);
      cameraMaterialTemplate.DisableKeyword(ShaderKeywordStrings._ALPHAMODULATE_ON);
      cameraMaterialTemplate.SetFloat(@"_BlendOp", (float)BlendOp.Add);
      cameraMaterialTemplate.SetFloat(@"_SrcBlend", (float)BlendMode.One);
      cameraMaterialTemplate.SetFloat(@"_DstBlend", (float)BlendMode.Zero);

      colorMaterialID = entitiesGraphicsSystem.RegisterMaterial(clutColorMaterialTemplate);

      {
        var noiseBitmapSet = CreateTexture(@"Noise", 32, 32);
        Material noiseMaterial = new(clutMaterialTemplate);
        noiseMaterial.SetTexture(shaderTextureName, noiseBitmapSet.Texture);
        noiseMaterial.DisableKeyword(ShaderKeywordStrings._ALPHATEST_ON);
        noiseMaterial.DisableKeyword(@"_LIGHTGRID");

        noiseMaterialID = entitiesGraphicsSystem.RegisterMaterial(noiseMaterial);
        bitmapSetLoaders.Add(noiseMaterialID, new CompletedLoader<BitmapSet>(noiseBitmapSet));

        noiseMaterial = new(clutDecalMaterialTemplate);
        noiseMaterial.SetTexture(shaderTextureName, noiseBitmapSet.Texture);
        noiseMaterial.DisableKeyword(ShaderKeywordStrings._ALPHATEST_ON);
        noiseMaterial.DisableKeyword(@"_LIGHTGRID");

        decalNoiseMaterialID = entitiesGraphicsSystem.RegisterMaterial(noiseMaterial);
        bitmapSetLoaders.Add(decalNoiseMaterialID, new CompletedLoader<BitmapSet>(noiseBitmapSet));
      }

      {
        cameraBitmapSets = new CameraBitmapSet[NUM_HACK_CAMERAS];

        for (var i = 0; i < NUM_HACK_CAMERAS; ++i) {
          cameraBitmapSets[i] = new CameraBitmapSet() {
            Texture = new RenderTexture(new RenderTextureDescriptor(128, 128, RenderTextureFormat.ARGB32, 16)) {
              name = @"Camera",
              filterMode = FilterMode.Point,
              wrapMode = TextureWrapMode.Repeat
            },
            Description = new BitmapDesc() {
              Transparent = false,
              Size = new(32, 32),
              AnchorPoint = new(),
              AnchorRect = new()
            }
          };
        }
      }

      {
        (float opacity, float purity) fog = (0x3000 / (float)0xFFFF, 0x6000 / (float)0xFFFF);
        (float opacity, float purity) force = (0x5000 / (float)0xFFFF, 0x8000 / (float)0xFFFF);

        translucencyTable = new(9, Allocator.Persistent) {
          [249] = (fog.opacity, fog.purity, new Color32(255, 0, 0, 0)),
          [250] = (fog.opacity, fog.purity, new Color32(0, 255, 0, 0)),
          [251] = (fog.opacity, fog.purity, new Color32(0, 0, 255, 0)),
          [248] = (fog.opacity, fog.purity, new Color32(170, 170, 170, 0)),
          [252] = (fog.opacity, fog.purity, new Color32(240, 240, 240, 0)),
          [247] = (fog.opacity, fog.purity, new Color32(120, 120, 120, 0)),

          [255] = (force.opacity, force.purity, new Color32(255, 0, 0, 0)),
          [254] = (force.opacity, force.purity, new Color32(0, 255, 0, 0)),
          [253] = (force.opacity, force.purity, new Color32(0, 0, 255, 0)),
        };
      }

      defaultBitmapDesc = new() {
        Transparent = false,
        Size = new(64, 64),
        AnchorPoint = new(),
        AnchorRect = new()
      };
    }

    protected override void OnDestroy() {
      base.OnDestroy();

      bitmapMaterials.Dispose();
      cameraMaterials.Dispose();
      wordMaterials.Dispose();
      textMaterials.Dispose();
      freeTextMaterials.Dispose();

      randoms.Dispose();
    }

    protected override void OnUpdate() {
      if (bitmapSetLoaders.TryGetValue(noiseMaterialID, out var noiseBitmapSetLoader)) {
        if (noiseBitmapSetLoader.IsCompleted) {
          var noiseTexture = noiseBitmapSetLoader.Result.Texture;
          var noiseTextureData = noiseTexture.GetRawTextureData<byte>();

          var fillStaticTextureJob = new FillNoiseTexture() {
            ColorBase = Palette.GRAY_8_BASE,
            Stride = noiseTexture.format == TextureFormat.R8 ? 1 : 4,
            TextureData = noiseTextureData,
            Randoms = randoms
          };

          Dependency = fillStaticTextureJob.ScheduleBatch(noiseTextureData.Length, 32, Dependency);

          CompleteDependency();

          noiseTexture.Apply(false, false);
        }
      }
    }

    public Material ClutMaterialTemplate => clutMaterialTemplate;
    public Material DecalClutMaterialTemplate => clutDecalMaterialTemplate;

    public BatchMaterialID ColorMaterialID => colorMaterialID;
    public BatchMaterialID NoiseMaterialID => noiseMaterialID;

    public BatchMaterialID GetMaterial(ushort resId, ushort blockIndex, bool lightmapped, bool decal) {
      var resRef = (uint)((resId << 16) | blockIndex);

      if (bitmapMaterials.TryGetValue((resRef, lightmapped, decal), out var batchMaterialID))
        return batchMaterialID; // Res already loaded. Skip loading.

      Material material = new(decal ? clutDecalMaterialTemplate : clutMaterialTemplate);
      if (lightmapped) material.EnableKeyword(@"_LIGHTGRID");
      else material.DisableKeyword(@"_LIGHTGRID");

      batchMaterialID = entitiesGraphicsSystem.RegisterMaterial(material);

      if (bitmapMaterials.TryAdd((resRef, lightmapped, decal), batchMaterialID)) {
        LoadBitmapToMaterial(resRef, batchMaterialID);
        return batchMaterialID;
      }

      Debug.LogWarning($"GetMaterial failed for {resRef:X8}.");

      return BatchMaterialID.Null;
    }

    public BatchMaterialID GetTextureMaterial(ushort textureIndex) {
      return GetMaterial((ushort)(0x03E8 + textureIndex), 0, true, false); // Uses the 128x128 resource id
    }

    public BatchMaterialID GetCameraMaterial(int cameraIndex, bool lightmapped, bool decal) {
      if (cameraMaterials.TryGetValue((cameraIndex, lightmapped, decal), out var batchMaterialID))
        return batchMaterialID; // Res already loaded. Skip loading.

      Material material = new(decal ? decalMaterialTemplate : cameraMaterialTemplate);
      if (lightmapped) material.EnableKeyword(@"_LIGHTGRID");
      else material.DisableKeyword(@"_LIGHTGRID");

      batchMaterialID = entitiesGraphicsSystem.RegisterMaterial(material);

      if (cameraMaterials.TryAdd((cameraIndex, lightmapped, decal), batchMaterialID)) {
        var cameraTexture = cameraBitmapSets[cameraIndex].Texture;
        material.SetTexture(shaderTextureName, cameraTexture);
        return batchMaterialID;
      }

      Debug.LogWarning($"GetCameraMaterial failed for {cameraIndex}.");

      return BatchMaterialID.Null;
    }

    public RenderTexture GetCameraRenderTexture(int cameraIndex) {
      return cameraBitmapSets[cameraIndex].Texture;
    }

    public BatchMaterialID GetWordMaterial(ushort wordIndex, byte color, byte style) {
      if (wordMaterials.TryGetValue((wordIndex, color, style), out var batchMaterialID))
        return batchMaterialID; // Word already rendered. Skip rendering.

      Material material = new(clutDecalMaterialTemplate);
      material.EnableKeyword(@"_LIGHTGRID");
      material.EnableKeyword(ShaderKeywordStrings._ALPHATEST_ON);

      batchMaterialID = entitiesGraphicsSystem.RegisterMaterial(material);

      if (wordMaterials.TryAdd((wordIndex, color, style), batchMaterialID)) {
        RenderTextAsync(batchMaterialID, TextType.Word, wordIndex, color, style, false);
        return batchMaterialID;
      }

      Debug.LogWarning($"GetWordMaterial failed for {wordIndex}.");

      return BatchMaterialID.Null;
    }

    public BatchMaterialID GetTextMaterial(ushort textIndex, byte color, byte style, bool lightmapped, bool decal, bool scroll) {
      if (textMaterials.TryGetValue((textIndex, color, style, lightmapped, decal, scroll), out var batchMaterialID))
        return batchMaterialID; // Word already rendered. Skip rendering.

      Material material = new(decal ? decalMaterialTemplate : clutMaterialTemplate);
      if (lightmapped) material.EnableKeyword(@"_LIGHTGRID");
      else material.DisableKeyword(@"_LIGHTGRID");
      material.DisableKeyword(ShaderKeywordStrings._ALPHATEST_ON);

      batchMaterialID = entitiesGraphicsSystem.RegisterMaterial(material);

      if (textMaterials.TryAdd((textIndex, color, style, lightmapped, decal, scroll), batchMaterialID)) {
        RenderTextAsync(batchMaterialID, TextType.Screen, textIndex, color, style, scroll);
        return batchMaterialID;
      }

      Debug.LogWarning($"GetTextMaterial failed for {textIndex}.");

      return BatchMaterialID.Null;
    }

    public BatchMaterialID GetTextMaterial(string fullText, byte color, byte style, bool lightmapped, bool decal) {
      var hash = UnityEngine.Hash128.Compute(fullText);

      if (freeTextMaterials.TryGetValue((hash, color, style, lightmapped, decal), out var batchMaterialID))
        return batchMaterialID; // Word already rendered. Skip rendering.

      Material material = new(decal ? decalMaterialTemplate : clutMaterialTemplate);
      if (lightmapped) material.EnableKeyword(@"_LIGHTGRID");
      else material.DisableKeyword(@"_LIGHTGRID");
      material.DisableKeyword(ShaderKeywordStrings._ALPHATEST_ON);

      batchMaterialID = entitiesGraphicsSystem.RegisterMaterial(material);

      if (freeTextMaterials.TryAdd((hash, color, style, lightmapped, decal), batchMaterialID)) {
        RenderTextAsync(batchMaterialID, TextType.Screen, fullText, color, style);
        return batchMaterialID;
      }

      Debug.LogWarning($"GetTextMaterial failed for {fullText}.");

      return BatchMaterialID.Null;
    }

    public BatchMaterialID GetTranslucentMaterial(byte colorIndex) {
      if (translucentMaterials.TryGetValue(colorIndex, out var batchMaterialID))
        return batchMaterialID; // Res already loaded. Skip loading.

      Material material = new(translucencyMaterialTemplate);

      if (translucencyTable.TryGetValue(colorIndex, out var attributes)) {
        material.SetFloat(@"_Opacity", attributes.opacity);
        material.SetFloat(@"_Purity", attributes.purity);
        material.SetColor(@"_Color", attributes.color);
      } else {
        material.SetFloat(@"_Opacity", 1f);
        material.SetFloat(@"_Purity", 0f);
        material.SetColor(@"_Color", new Color32(128, 128, 128, 0)); // TODO clut lookup colorIndex
      }

      /*
      if (lightmapped) material.EnableKeyword(@"_LIGHTGRID");
      else material.DisableKeyword(@"_LIGHTGRID");
      */

      batchMaterialID = entitiesGraphicsSystem.RegisterMaterial(material);

      if (translucentMaterials.TryAdd(colorIndex, batchMaterialID)) {
        return batchMaterialID;
      }

      Debug.LogWarning($"GetTranslucentMaterial failed for {colorIndex}.");

      return BatchMaterialID.Null;
    }

    private async void LoadBitmapToMaterial(uint resRef, BatchMaterialID batchMaterialID) {
      if (!bitmapSetLoaders.TryGetValue(batchMaterialID, out var bitmapSetLoadOp)) {  // Check if BitmapSet already loaded.
        bitmapSetLoadOp = Res.Load<BitmapSet>(resRef);
        bitmapSetLoaders.TryAdd(batchMaterialID, bitmapSetLoadOp);
      }

      bitmapSetLoaders.TryAdd(batchMaterialID, bitmapSetLoadOp);

      var bitmapSet = await bitmapSetLoadOp;

      var material = entitiesGraphicsSystem.GetMaterial(batchMaterialID);
      material.SetTexture(shaderTextureName, bitmapSet.Texture);

      if (bitmapSet.Description.Transparent)
        material.EnableKeyword(ShaderKeywordStrings._ALPHATEST_ON);
      else
        material.DisableKeyword(ShaderKeywordStrings._ALPHATEST_ON);
    }

    private async void RenderTextAsync(BatchMaterialID batchMaterialID, TextType type, ushort wordIndex, byte color, byte style, bool scroll) {
      var (settings, colorIndex, fontRes) = GraphicUtils.GetTextProperties(type, color, style);
      var bitmapSet = CreateTexture(@"Rendered text", settings.Width, settings.Height, true);

      bitmapSetLoaders.TryAdd(batchMaterialID, new CompletedLoader<BitmapSet>(bitmapSet));

      unsafe {
        var rawData = bitmapSet.Texture.GetRawTextureData<byte>();
        UnsafeUtility.MemClear(rawData.GetUnsafePtr(), rawData.Length);
      }

      var material = entitiesGraphicsSystem.GetMaterial(batchMaterialID);
      material.SetTexture(shaderTextureName, bitmapSet.Texture);

      var fontSet = await Res.Load<FontSet>(fontRes);

      if (scroll) {
        byte scrollIndex = 0;
        var textPos = new int2(1, 1);

        while (textPos.y < settings.Height) {
          var fullText = await Res.Load<string>(settings.ResId, (ushort)(wordIndex + scrollIndex));

          var textSize = GraphicUtils.MeasureString(fontSet, fullText);

          GraphicUtils.DrawString(bitmapSet.Texture, fontSet, fullText, textPos, colorIndex);

          textPos.y += textSize.y + 1;
          ++scrollIndex;
        }
      } else {
        var fullText = await Res.Load<string>(settings.ResId, wordIndex);

        var textSize = GraphicUtils.MeasureString(fontSet, fullText);
        var textPos = max((new int2(settings.Width, settings.Height) - textSize) >> 1, new int2(1, 0));

        GraphicUtils.DrawString(bitmapSet.Texture, fontSet, fullText, textPos, colorIndex);
      }
    }

    private async void RenderTextAsync(BatchMaterialID batchMaterialID, TextType type, string fullText, byte color, byte style) {
      var (settings, colorIndex, fontRes) = GraphicUtils.GetTextProperties(type, color, style);
      var bitmapSet = CreateTexture(@"Rendered text", settings.Width, settings.Height, true);

      bitmapSetLoaders.TryAdd(batchMaterialID, new CompletedLoader<BitmapSet>(bitmapSet));

      unsafe {
        var rawData = bitmapSet.Texture.GetRawTextureData<byte>();
        UnsafeUtility.MemClear(rawData.GetUnsafePtr(), rawData.Length);
      }

      var material = entitiesGraphicsSystem.GetMaterial(batchMaterialID);
      material.SetTexture(shaderTextureName, bitmapSet.Texture);

      var fontSet = await Res.Load<FontSet>(fontRes);

      var textSize = GraphicUtils.MeasureString(fontSet, fullText);
      var textPos = max((new int2(settings.Width, settings.Height) - textSize) >> 1, new int2(1, 0));

      GraphicUtils.DrawString(bitmapSet.Texture, fontSet, fullText, textPos, colorIndex);
    }

    public async Awaitable<BitmapDesc> GetBitmapDesc(BatchMaterialID materialID) {
      if (bitmapSetLoaders.TryGetValue(materialID, out var bitmapSetLoader))
        return (await bitmapSetLoader).Description;

      var cameraIndex = cameraMaterials.GetValueArray(Allocator.Temp).IndexOf(materialID);
      if (cameraIndex != -1)
        return cameraBitmapSets[cameraIndex].Description;

      return defaultBitmapDesc;
    }

    public IResHandle<BitmapSet> GetBitmapLoader(BatchMaterialID materialID) {
      return bitmapSetLoaders[materialID];
    }

    public BatchMaterialID ParseTextureData(int textureData, bool lightmapped, bool decal, out TextureType type, out int scale) {
      const int DATA_MASK = 0xFFF;

      const int FIRST_CAMERA_TMAP = 0x78;

      const int NUM_AUTOMAP_MAGIC_COOKIES = 6;
      const int FIRST_AUTOMAP_MAGIC_COOKIE = 0x70;

      textureData &= DATA_MASK;

      byte index = (byte)(textureData & INDEX_MASK);
      type = (TextureType)((textureData & TYPE_MASK) >> TPOLY_INDEX_BITS);
      scale = (textureData & SCALE_MASK) >> (TPOLY_INDEX_BITS + TPOLY_TYPE_BITS);
      byte style = (textureData & STYLE_MASK) == STYLE_MASK ? (byte)2 : (byte)3;

      if (type == TextureType.Alt) {
        return GetMaterial((ushort)(SmallTextureIdBase + index), 0, lightmapped, decal);
      } else if (type == TextureType.Custom) {
        if (index >= FIRST_CAMERA_TMAP && index <= (FIRST_CAMERA_TMAP + NUM_HACK_CAMERAS)) {
          var cameraIndex = index - FIRST_CAMERA_TMAP;

          // if (hasCamera(cameraIndex))
          return GetCameraMaterial(cameraIndex, lightmapped, decal);
          // else
          // return noiseMaterialID;
        } else if (index == REGULAR_STATIC_MAGIC_COOKIE || index == SHODAN_STATIC_MAGIC_COOKIE) {
          return decal ? decalNoiseMaterialID : noiseMaterialID;
        } else if (index >= FIRST_AUTOMAP_MAGIC_COOKIE && index <= (FIRST_AUTOMAP_MAGIC_COOKIE + NUM_AUTOMAP_MAGIC_COOKIES)) {
          return GetMaterial(CustomTextureIdBase, 0, lightmapped, decal); // TODO FIXME PLACEHOLDER
          // ret automap bitmap
        }

        var defaultMaterial = GetMaterial((ushort)(CustomTextureIdBase + index), 0, lightmapped, decal);

        if (defaultMaterial == BatchMaterialID.Null)
          return decal ? decalNoiseMaterialID : noiseMaterialID;

        return defaultMaterial;
      } else if (type == TextureType.Text) {
        if (index == RANDOM_TEXT_MAGIC_COOKIE) {
          var seed = TimeUtils.SecondsToFastTicks(SystemAPI.Time.ElapsedTime) >> 7;
          var number = ((seed * 9277 + 7) % 14983) % 10;

          return GetTextMaterial($"{number}", 0 /* style >> 16 */, style, lightmapped, decal);
        } else {
          return GetTextMaterial(index, 0 /* style >> 16 */, style, lightmapped, decal, false);
        }
      } else if (type == TextureType.ScrollText) {
        return GetTextMaterial(index, 0 /* style >> 16 */, style, lightmapped, decal, true);
      }

      return BatchMaterialID.Null;
    }

    private BitmapSet CreateTexture(string name, int width, int height, bool transparent = false) {
      Texture2D texture;
      if (SystemInfo.SupportsTextureFormat(TextureFormat.R8)) {
        texture = new Texture2D(width, height, TextureFormat.R8, false, true);
      } else if (SystemInfo.SupportsTextureFormat(TextureFormat.RGBA32)) {
        texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
      } else {
        throw new Exception("No supported TextureFormat found.");
      }
      texture.name = name;
      texture.filterMode = FilterMode.Point;
      texture.wrapMode = TextureWrapMode.Clamp;

      BitmapSet bitmapSet = new() {
        Texture = texture,
        Description = new() {
          Transparent = transparent,
          Size = new(texture.width, texture.height),
          AnchorPoint = new(),
          AnchorRect = new()
        }
      };

      return bitmapSet;
    }

    private class MipMapLoader : LoaderBase<BitmapSet> {
      public MipMapLoader(ushort textureIndex) {
        Load(textureIndex);
      }

      private async void Load(ushort textureIndex) {
        var tex128x128op = Res.Load<BitmapSet>((ushort)(0x03E8 + textureIndex));
        var tex64x64op = Res.Load<BitmapSet>((ushort)(0x02C3 + textureIndex));
        var tex32x32op = Res.Load<BitmapSet>(0x004D, textureIndex);
        var tex16x16op = Res.Load<BitmapSet>(0x004C, textureIndex);

        // TODO FIXME disposing these might be a bad idea. Resources should be cached and reference counted.
        using var tex128x128 = await tex128x128op;

        Texture2D complete = new(128, 128, tex128x128.Texture.format, 4, true) {
          filterMode = tex128x128.Texture.filterMode,
          wrapMode = tex128x128.Texture.wrapMode
        };

        using var tex64x64 = await tex64x64op;
        using var tex32x32 = await tex32x32op;
        using var tex16x16 = await tex16x16op;

        if (SystemInfo.copyTextureSupport.HasFlag(CopyTextureSupport.Basic)) {
          Graphics.CopyTexture(tex128x128.Texture, 0, 0, complete, 0, 0);
          Graphics.CopyTexture(tex64x64.Texture, 0, 0, complete, 0, 1);
          Graphics.CopyTexture(tex32x32.Texture, 0, 0, complete, 0, 2);
          Graphics.CopyTexture(tex16x16.Texture, 0, 0, complete, 0, 3);
        } else {
          complete.SetPixelData(tex128x128.Texture.GetPixelData<byte>(0), 0);
          complete.SetPixelData(tex64x64.Texture.GetPixelData<byte>(0), 1);
          complete.SetPixelData(tex32x32.Texture.GetPixelData<byte>(0), 2);
          complete.SetPixelData(tex16x16.Texture.GetPixelData<byte>(0), 3);
        }
        complete.Apply(false, true);

        InvokeCompletionEvent(new BitmapSet {
          Texture = complete,
          Description = tex128x128.Description
        });
      }
    }

    [BurstCompile]
    struct FillNoiseTexture : IJobParallelForBatch {
      [ReadOnly] public byte ColorBase;
      [ReadOnly] public int Stride;

      [WriteOnly, NativeDisableParallelForRestriction] public NativeArray<byte> TextureData;
      [NativeDisableContainerSafetyRestriction] public NativeArray<Random> Randoms;

      public void Execute(int startIndex, int count) {
        var threadIndex = JobsUtility.ThreadIndex;

        var random = Randoms[threadIndex];

        int lastIndex = startIndex + count;
        for (int index = startIndex; index < lastIndex; ++index) {
          var rand = random.NextUInt();
          if ((rand & 0x300) == 0x300)
            TextureData[index * Stride] = (byte)(ColorBase + (rand & 0x07));
          else
            TextureData[index * Stride] = 0;
        }

        Randoms[threadIndex] = random;
      }
    }

    private class CameraBitmapSet : IDisposable {
      public RenderTexture Texture;
      public BitmapDesc Description;

      public void Dispose() {
        if (Texture != null) UnityEngine.Object.Destroy(Texture);
      }
    }
  }
}
