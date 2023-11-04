using SS.Resources;
using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static SS.TextureUtils;
using Random = Unity.Mathematics.Random;

namespace SS.System {
  [CreateAfter(typeof(EntitiesGraphicsSystem))]
  [UpdateInGroup(typeof(InitializationSystemGroup))]
  public partial class MaterialProviderSystem : SystemBase {
    public static readonly int shaderTextureName = Shader.PropertyToID(@"_Texture");

    private NativeParallelHashMap<(uint resRef, bool lightmapped, bool decal), BatchMaterialID> bitmapMaterials;
    private NativeParallelHashMap<(int cameraIndex, bool lightmapped, bool decal), BatchMaterialID> cameraMaterials;
    private NativeParallelHashMap<ushort, BatchMaterialID> textureMaterials;

    private NativeParallelHashMap<BatchMaterialID, uint> materialIDToBitmapResourceRef;
    private readonly Dictionary<uint, IResHandle<BitmapSet>> bitmapSetLoaders = new();

    private EntitiesGraphicsSystem entitiesGraphicsSystem;

    private NativeArray<Random> randoms;

    private BatchMaterialID colorMaterialID;
    private BatchMaterialID noiseMaterialID;

    private Material clutMaterialTemplate;
    private Material clutDecalMaterialTemplate;
    private Material clutColorMaterialTemplate;

    private Material decalMaterialTemplate;
    private Material cameraMaterialTemplate;

    private Material noiseMaterial;
    private BitmapSet noiseBitmapSet;

    private BitmapDesc[] cameraBitmapDescs;
    private RenderTexture[] cameraRenderTextures;

    protected override void OnCreate() {
      base.OnCreate();

      bitmapMaterials = new(1024, Allocator.Persistent);
      cameraMaterials = new(128, Allocator.Persistent);
      textureMaterials = new(128, Allocator.Persistent);

      materialIDToBitmapResourceRef = new(1024, Allocator.Persistent);

      entitiesGraphicsSystem = World.GetOrCreateSystemManaged<EntitiesGraphicsSystem>();

      randoms = new NativeArray<Random>(JobsUtility.ThreadIndexCount, Allocator.Persistent);
      for (int i = 0; i < randoms.Length; ++i)
        randoms[i] = Random.CreateFromIndex((uint)i);

      clutMaterialTemplate = new Material(Shader.Find("Shader Graphs/URP CLUT")) {
        enableInstancing = true
      };

      clutDecalMaterialTemplate = new Material(Shader.Find(@"Shader Graphs/URP CLUT Decal")) {
        enableInstancing = true
      };

      clutColorMaterialTemplate = new Material(Shader.Find("Universal Render Pipeline/System Shock/CLUT Color"));
      clutColorMaterialTemplate.DisableKeyword(ShaderKeywordStrings._ALPHAPREMULTIPLY_ON);
      clutColorMaterialTemplate.DisableKeyword(ShaderKeywordStrings._SURFACE_TYPE_TRANSPARENT);
      clutColorMaterialTemplate.DisableKeyword(ShaderKeywordStrings._ALPHAMODULATE_ON);
      clutColorMaterialTemplate.EnableKeyword(@"_LIGHTGRID");
      clutColorMaterialTemplate.SetFloat(@"_BlendOp", (float)BlendOp.Add);
      clutColorMaterialTemplate.SetFloat(@"_SrcBlend", (float)BlendMode.One);
      clutColorMaterialTemplate.SetFloat(@"_DstBlend", (float)BlendMode.Zero);
      clutColorMaterialTemplate.enableInstancing = true;

      decalMaterialTemplate = new Material(Shader.Find(@"Shader Graphs/URP Decal")) {
        enableInstancing = true
      };

      cameraMaterialTemplate = new Material(Shader.Find("Universal Render Pipeline/Unlit")); // TODO Create color material with nearest lookup
      cameraMaterialTemplate.DisableKeyword(ShaderKeywordStrings._ALPHAPREMULTIPLY_ON);
      cameraMaterialTemplate.DisableKeyword(ShaderKeywordStrings._SURFACE_TYPE_TRANSPARENT);
      cameraMaterialTemplate.DisableKeyword(ShaderKeywordStrings._ALPHAMODULATE_ON);
      cameraMaterialTemplate.SetFloat(@"_BlendOp", (float)BlendOp.Add);
      cameraMaterialTemplate.SetFloat(@"_SrcBlend", (float)BlendMode.One);
      cameraMaterialTemplate.SetFloat(@"_DstBlend", (float)BlendMode.Zero);
      cameraMaterialTemplate.enableInstancing = true;

      colorMaterialID = entitiesGraphicsSystem.RegisterMaterial(clutColorMaterialTemplate);

      {
        noiseBitmapSet = CreateNoiseTexture();
        noiseMaterial = new Material(clutMaterialTemplate);
        noiseMaterial.SetTexture(Shader.PropertyToID(@"_BaseMap"), noiseBitmapSet.Texture);
        noiseMaterial.DisableKeyword(ShaderKeywordStrings._ALPHATEST_ON);
        noiseMaterial.DisableKeyword(@"_LIGHTGRID");
        noiseMaterial.renderQueue = (int)RenderQueue.Geometry;

        noiseMaterialID = entitiesGraphicsSystem.RegisterMaterial(noiseMaterial);
      }

      {
        cameraBitmapDescs = new BitmapDesc[NUM_HACK_CAMERAS];
        cameraRenderTextures = new RenderTexture[NUM_HACK_CAMERAS];
        /*
          RenderTextureFormat format;
          if (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.R8))
            format = RenderTextureFormat.R8;
          else if (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGB32))
            format = RenderTextureFormat.ARGB32;
          else
            throw new Exception("No supported TextureFormat found.");
        */
        var format = RenderTextureFormat.ARGB32;

        for (var i = 0; i < NUM_HACK_CAMERAS; ++i) {
          cameraRenderTextures[i] = new RenderTexture(new RenderTextureDescriptor(128, 128, format, 16)) {
            name = @"Camera",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Repeat
          };

          cameraBitmapDescs[i] = new BitmapDesc() {
            Transparent = false,
            Size = new(32, 32),
            AnchorPoint = new(),
            AnchorRect = new()
          };
        }
      }
    }

    protected override void OnDestroy() {
      base.OnDestroy();

      bitmapMaterials.Dispose();
      cameraMaterials.Dispose();
      textureMaterials.Dispose();

      materialIDToBitmapResourceRef.Dispose();

      randoms.Dispose();
    }

    protected override void OnUpdate() {
      if (noiseBitmapSet?.Texture != null) {
        var noiseTexture = noiseBitmapSet.Texture;
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
        materialIDToBitmapResourceRef[batchMaterialID] = resRef;

        LoadBitmapToMaterial(resId, blockIndex, material);

        return batchMaterialID;
      }

      Debug.LogWarning($"GetMaterial failed for {resRef:X8}.");

      return BatchMaterialID.Null;
    }

    public BatchMaterialID GetTextureMaterial(ushort textureIndex) {
      if (textureMaterials.TryGetValue(textureIndex, out var batchMaterialID))
        return batchMaterialID; // Res already loaded. Skip loading.

      Material material = new(clutMaterialTemplate);
      material.EnableKeyword(@"_LIGHTGRID");

      batchMaterialID = entitiesGraphicsSystem.RegisterMaterial(material);

      if (textureMaterials.TryAdd(textureIndex, batchMaterialID)) {
        materialIDToBitmapResourceRef[batchMaterialID] = (uint)((0x03E8 + textureIndex) << 16); // Uses the 128x128 resource id

        LoadTextureToMaterial(textureIndex, material);

        return batchMaterialID;
      }

      Debug.LogWarning($"GetTextureMaterial failed for {textureIndex}.");

      return BatchMaterialID.Null;
    }

    private async void LoadTextureToMaterial(ushort textureIndex, Material material) {
      var resRef = (uint)((0x03E8 + textureIndex) << 16); // Uses the 128x128 resource id

      if (!bitmapSetLoaders.TryGetValue(resRef, out var bitmapSetLoadOp)) {  // Check if BitmapSet already loaded.
        bitmapSetLoadOp = new MipMapLoader(textureIndex);
        bitmapSetLoaders.TryAdd(resRef, bitmapSetLoadOp);
      }

      var bitmapSet = await bitmapSetLoadOp;

      material.SetTexture(shaderTextureName, bitmapSet.Texture);

      if (bitmapSet.Description.Transparent)
        material.EnableKeyword(ShaderKeywordStrings._ALPHATEST_ON);
      else
        material.DisableKeyword(ShaderKeywordStrings._ALPHATEST_ON);
    }

    private async void LoadBitmapToMaterial(ushort resId, ushort blockIndex, Material material) {
      var resRef = (uint)((resId << 16) | blockIndex);

      if (!bitmapSetLoaders.TryGetValue(resRef, out var bitmapSetLoadOp)) {  // Check if BitmapSet already loaded.
        bitmapSetLoadOp = Res.Load<BitmapSet>(resId, blockIndex);
        bitmapSetLoaders.TryAdd(resRef, bitmapSetLoadOp);
      }

      var bitmapSet = await bitmapSetLoadOp;

      material.SetTexture(shaderTextureName, bitmapSet.Texture);

      if (bitmapSet.Description.Transparent)
        material.EnableKeyword(ShaderKeywordStrings._ALPHATEST_ON);
      else
        material.DisableKeyword(ShaderKeywordStrings._ALPHATEST_ON);
    }

    public BatchMaterialID GetCameraMaterial(int cameraIndex, bool lightmapped, bool decal) {
      if (cameraMaterials.TryGetValue((cameraIndex, lightmapped, decal), out var batchMaterialID))
        return batchMaterialID; // Res already loaded. Skip loading.

      Material material = new(decal ? decalMaterialTemplate : cameraMaterialTemplate);
      if (lightmapped) material.EnableKeyword(@"_LIGHTGRID");
      else material.DisableKeyword(@"_LIGHTGRID");

      batchMaterialID = entitiesGraphicsSystem.RegisterMaterial(material);

      if (cameraMaterials.TryAdd((cameraIndex, lightmapped, decal), batchMaterialID)) {
        var cameraTexture = cameraRenderTextures[cameraIndex];
        material.SetTexture(shaderTextureName, cameraTexture);
        return batchMaterialID;
      }

      Debug.LogWarning($"GetCameraMaterial failed for {cameraIndex}.");

      return BatchMaterialID.Null;
    }

    public RenderTexture GetCameraRenderTexture(int cameraIndex) {
      return cameraRenderTextures[cameraIndex];
    }

    public async Awaitable<BitmapDesc> GetBitmapDesc(BatchMaterialID materialID) {
      var cameraIndex = cameraMaterials.GetValueArray(Allocator.Temp).IndexOf(materialID);

      if (materialID == noiseMaterialID)
        return noiseBitmapSet.Description;
      if (cameraIndex != -1)
        return cameraBitmapDescs[cameraIndex];
      else
        return (await bitmapSetLoaders[materialIDToBitmapResourceRef[materialID]]).Description;
    }

    public IResHandle<BitmapSet> GetBitmapLoader(BatchMaterialID materialID) {
      return bitmapSetLoaders[materialIDToBitmapResourceRef[materialID]];
    }

    public BatchMaterialID ParseTextureData(int textureData, bool lightmapped, bool decal, out TextureType type, out int scale) {
      const int DATA_MASK = 0xFFF;

      const int FIRST_CAMERA_TMAP = 0x78;

      const int NUM_AUTOMAP_MAGIC_COOKIES = 6;
      const int FIRST_AUTOMAP_MAGIC_COOKIE = 0x70;

      textureData &= DATA_MASK;

      var index = textureData & INDEX_MASK;
      type = (TextureType)((textureData & TYPE_MASK) >> TPOLY_INDEX_BITS);
      scale = (textureData & SCALE_MASK) >> (TPOLY_INDEX_BITS + TPOLY_TYPE_BITS);
      var style = (textureData & STYLE_MASK) == STYLE_MASK ? 2 : 3;

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
          return noiseMaterialID;
        } else if (index >= FIRST_AUTOMAP_MAGIC_COOKIE && index <= (FIRST_AUTOMAP_MAGIC_COOKIE + NUM_AUTOMAP_MAGIC_COOKIES)) {
          return GetMaterial(CustomTextureIdBase, 0, lightmapped, decal); // TODO FIXME PLACEHOLDER
          // ret automap bitmap
        }

        var defaultMaterial = GetMaterial((ushort)(CustomTextureIdBase + index), 0, lightmapped, decal);

        if (defaultMaterial == BatchMaterialID.Null)
          return noiseMaterialID;

        return defaultMaterial;
      } else if (type == TextureType.Text) {
        if (index == RANDOM_TEXT_MAGIC_COOKIE) {
          // TODO randomize text
          // TODO DRAW TEXT CANVAS
        } else {
          // TODO DRAW TEXT CANVAS
        }
      } else if (type == TextureType.ScrollText) {
        // TODO DRAW TEXT CANVAS
      }

      return BatchMaterialID.Null;
    }

    private BitmapSet CreateNoiseTexture () {
      Texture2D noiseTexture;
      if (SystemInfo.SupportsTextureFormat(TextureFormat.R8)) {
        noiseTexture = new Texture2D(32, 32, TextureFormat.R8, false, true);
      } else if (SystemInfo.SupportsTextureFormat(TextureFormat.RGBA32)) {
        noiseTexture = new Texture2D(32, 32, TextureFormat.RGBA32, false, true);
      } else {
        throw new Exception("No supported TextureFormat found.");
      }
      noiseTexture.name = @"Noise";
      noiseTexture.filterMode = FilterMode.Point;
      noiseTexture.wrapMode = TextureWrapMode.Repeat;

      BitmapSet noiseBitmapSet = new() {
        Texture = noiseTexture,
        Description = new() {
          Transparent = false,
          Size = new(noiseTexture.width, noiseTexture.height),
          AnchorPoint = new(),
          AnchorRect = new()
        }
      };

      return noiseBitmapSet;
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
  }
}
