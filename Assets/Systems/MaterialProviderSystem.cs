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
using UnityEngine.AddressableAssets;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.ResourceManagement.AsyncOperations;
using static SS.TextureUtils;
using Hash128 = UnityEngine.Hash128;
using Random = Unity.Mathematics.Random;

namespace SS.System {
  [CreateAfter(typeof(EntitiesGraphicsSystem))]
  [UpdateInGroup(typeof(InitializationSystemGroup))]
  public partial class MaterialProviderSystem : SystemBase {
    private NativeParallelHashMap<Hash128, BatchMaterialID> lightmappedMaterials; // TODO FIXME instead of hash, create resource reference struct? Addressables toString?
    private NativeParallelHashMap<Hash128, BatchMaterialID> unlitMaterials; // TODO FIXME instead of hash, create resource reference struct? Addressables toString?

    private NativeParallelHashMap<Hash128, BatchMaterialID> decalLightmappedMaterials; // TODO FIXME instead of hash, create resource reference struct? Addressables toString?
    private NativeParallelHashMap<Hash128, BatchMaterialID> decalUnlitMaterials; // TODO FIXME instead of hash, create resource reference struct? Addressables toString?

    private NativeParallelHashMap<BatchMaterialID, Hash128> materialIDToBitmapResource;
    private readonly Dictionary<Hash128, AsyncOperationHandle<BitmapSet>> bitmapSetLoaders = new();

    private EntitiesGraphicsSystem entitiesGraphicsSystem;

    private NativeArray<Random> randoms;

    private BatchMaterialID colorMaterialID;
    private BatchMaterialID noiseMaterialID;

    private Material clutMaterialTemplate;
    private Material decalClutMaterialTemplate;

    private Material noiseMaterial;
    private AsyncOperationHandle<BitmapSet> noiseBitmapSet;

    private BatchMaterialID[] cameraMaterialsIDs;
    private AsyncOperationHandle<BitmapDesc>[] cameraSetLoaders;
    private RenderTexture[] cameraRenderTextures;

    private AsyncOperationHandle<Texture2D> clutTextureOp;

    protected override void OnCreate() {
      base.OnCreate();

      lightmappedMaterials = new(1024, Allocator.Persistent);
      unlitMaterials = new(1024, Allocator.Persistent);

      decalLightmappedMaterials = new(1024, Allocator.Persistent);
      decalUnlitMaterials = new(1024, Allocator.Persistent);

      materialIDToBitmapResource = new(1024, Allocator.Persistent);

      entitiesGraphicsSystem = World.GetOrCreateSystemManaged<EntitiesGraphicsSystem>();

      randoms = new NativeArray<Random>(JobsUtility.MaxJobThreadCount, Allocator.Persistent);
      for (int i = 0; i < randoms.Length; ++i)
        randoms[i] = Random.CreateFromIndex((uint)i);

      this.clutTextureOp = Services.ColorLookupTableTexture;

      clutMaterialTemplate = new Material(Shader.Find("Universal Render Pipeline/System Shock/CLUT"));
      clutMaterialTemplate.DisableKeyword(ShaderKeywordStrings._ALPHAPREMULTIPLY_ON);
      clutMaterialTemplate.DisableKeyword(ShaderKeywordStrings._SURFACE_TYPE_TRANSPARENT);
      clutMaterialTemplate.DisableKeyword(ShaderKeywordStrings._ALPHAMODULATE_ON);
      clutMaterialTemplate.SetFloat(@"_BlendOp", (float)BlendOp.Add);
      clutMaterialTemplate.SetFloat(@"_SrcBlend", (float)BlendMode.One);
      clutMaterialTemplate.SetFloat(@"_DstBlend", (float)BlendMode.Zero);
      clutMaterialTemplate.enableInstancing = true;

      decalClutMaterialTemplate = new Material(Shader.Find(@"Shader Graphs/URP CLUT Decal"));
      decalClutMaterialTemplate.enableInstancing = true;

      var colorMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit")); // TODO Create color material with nearest lookup
      colorMaterial.DisableKeyword(ShaderKeywordStrings._ALPHAPREMULTIPLY_ON);
      colorMaterial.DisableKeyword(ShaderKeywordStrings._SURFACE_TYPE_TRANSPARENT);
      colorMaterial.DisableKeyword(ShaderKeywordStrings._ALPHAMODULATE_ON);
      colorMaterial.DisableKeyword(@"TRANSPARENCY_ON");
      colorMaterial.SetFloat(@"_BlendOp", (float)BlendOp.Add);
      colorMaterial.SetFloat(@"_SrcBlend", (float)BlendMode.One);
      colorMaterial.SetFloat(@"_DstBlend", (float)BlendMode.Zero);
      colorMaterial.enableInstancing = true;

      colorMaterialID = entitiesGraphicsSystem.RegisterMaterial(colorMaterial);

      {
        this.noiseMaterial = new Material(clutMaterialTemplate);
        noiseMaterialID = entitiesGraphicsSystem.RegisterMaterial(noiseMaterial);

        var createOp = Addressables.ResourceManager.StartOperation(new CreateNoiseTexture(), default);
        createOp.Completed += op => {
          if (op.Status != AsyncOperationStatus.Succeeded)
            throw op.OperationException;

          var bitmapSet = op.Result;

          noiseMaterial.SetTexture(Shader.PropertyToID(@"_BaseMap"), bitmapSet.Texture);
          noiseMaterial.DisableKeyword(@"TRANSPARENCY_ON");
          noiseMaterial.DisableKeyword(@"_ALPHATEST_ON");
          noiseMaterial.DisableKeyword(@"LIGHTGRID");
        };

        this.noiseBitmapSet = createOp;
      }

      clutTextureOp.Completed += op => {
        if (op.Status != AsyncOperationStatus.Succeeded)
          throw op.OperationException;

        // TODO FIXME create correct shader

        colorMaterial.SetTexture(Shader.PropertyToID(@"_BaseMap"), clutTextureOp.Result);
      };

      {
        this.cameraSetLoaders = new AsyncOperationHandle<BitmapDesc>[NUM_HACK_CAMERAS];
        this.cameraMaterialsIDs = new BatchMaterialID[NUM_HACK_CAMERAS];
        this.cameraRenderTextures = new RenderTexture[NUM_HACK_CAMERAS];
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
          var cameraMaterial = new Material(colorMaterial);
          this.cameraMaterialsIDs[i] = entitiesGraphicsSystem.RegisterMaterial(cameraMaterial);

          var cameraTexture = new RenderTexture(new RenderTextureDescriptor(128, 128, format, 16));
          cameraMaterial.SetTexture(Shader.PropertyToID(@"_BaseMap"), cameraTexture);

          cameraTexture.name = @"Camera";
          cameraTexture.filterMode = FilterMode.Point;
          cameraTexture.wrapMode = TextureWrapMode.Repeat;

          this.cameraRenderTextures[i] = cameraTexture;

          this.cameraSetLoaders[i] = Addressables.ResourceManager.CreateCompletedOperation(new BitmapDesc() {
            Transparent = false,
            Size = new(32, 32),
            AnchorPoint = new(),
            AnchorRect = new()
          }, null);
        }
      }
    }

    protected override void OnUpdate() {
      if (noiseBitmapSet.IsDone) {
        var noiseTexture = noiseBitmapSet.Result.Texture;
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

    protected override void OnDestroy() {
      base.OnDestroy();

      lightmappedMaterials.Dispose();
      unlitMaterials.Dispose();

      decalLightmappedMaterials.Dispose();
      decalUnlitMaterials.Dispose();

      materialIDToBitmapResource.Dispose();

      randoms.Dispose();
    }

    public Material ClutMaterialTemplate => clutMaterialTemplate;
    public BatchMaterialID ColorMaterialID => colorMaterialID;
    public BatchMaterialID NoiseMaterialID => noiseMaterialID;

    public BatchMaterialID GetMaterial(string resource, bool lightmapped, bool decal) {
      var hash = Hash128.Compute(resource);

      var materials = (lightmapped, decal) switch {
        (true, false) => lightmappedMaterials,
        (false, false) => unlitMaterials,
        (true, true) => decalLightmappedMaterials,
        (false, true) => decalUnlitMaterials
      };

      if (materials.TryGetValue(hash, out var batchMaterialID))
        return batchMaterialID; // Res already loaded. Skip loading.

      if (decal) {
        Material material = new Material(decalClutMaterialTemplate);
        if (lightmapped) material.EnableKeyword(@"LIGHTGRID");
        else material.DisableKeyword(@"LIGHTGRID");

        batchMaterialID = entitiesGraphicsSystem.RegisterMaterial(material);

        if (materials.TryAdd(hash, batchMaterialID)) {
          materialIDToBitmapResource[batchMaterialID] = hash;

          if (!bitmapSetLoaders.TryGetValue(hash, out var bitmapSetLoadOp)) {  // Check if BitmapSet already loaded.
            bitmapSetLoadOp = Addressables.LoadAssetAsync<BitmapSet>(resource);
            bitmapSetLoaders.TryAdd(hash, bitmapSetLoadOp);
          }

          bitmapSetLoadOp.Completed += loadOp => {
            /* TODO FIXME
            if (loadOp.Status != AsyncOperationStatus.Succeeded) {
              material.CopyPropertiesFromMaterial(noiseMaterial);
              return;
            }
            */

            var bitmapSet = bitmapSetLoadOp.Result;
            material.SetTexture(Shader.PropertyToID(@"Base_Map"), bitmapSet.Texture);

            if (bitmapSet.Description.Transparent)
              material.EnableKeyword(@"TRANSPARENCY_ON");
            else
              material.DisableKeyword(@"TRANSPARENCY_ON");
          };

          return batchMaterialID;
        }
      } else {
        Material material = new Material(clutMaterialTemplate);
        if (lightmapped) material.EnableKeyword(@"LIGHTGRID");
        else material.DisableKeyword(@"LIGHTGRID");

        batchMaterialID = entitiesGraphicsSystem.RegisterMaterial(material);

        if (materials.TryAdd(hash, batchMaterialID)) {
          materialIDToBitmapResource[batchMaterialID] = hash;

          if (!bitmapSetLoaders.TryGetValue(hash, out var bitmapSetLoadOp)) {  // Check if BitmapSet already loaded.
            bitmapSetLoadOp = Addressables.LoadAssetAsync<BitmapSet>(resource);
            bitmapSetLoaders.TryAdd(hash, bitmapSetLoadOp);
          }

          bitmapSetLoadOp.Completed += loadOp => {
            if (loadOp.Status != AsyncOperationStatus.Succeeded) {
              material.CopyPropertiesFromMaterial(noiseMaterial);
              return;
            }

            var bitmapSet = bitmapSetLoadOp.Result;
            material.SetTexture(Shader.PropertyToID(@"_BaseMap"), bitmapSet.Texture);

            if (bitmapSet.Description.Transparent) {
              material.EnableKeyword(@"TRANSPARENCY_ON");
              material.EnableKeyword(@"_ALPHATEST_ON");
              material.renderQueue = 2450;
            } else {
              material.DisableKeyword(@"TRANSPARENCY_ON");
              material.DisableKeyword(@"_ALPHATEST_ON");
            }
          };

          return batchMaterialID;
        }
      }

      Debug.LogWarning($"GetMaterial failed for {resource}.");

      return BatchMaterialID.Null;
    }

    public BatchMaterialID GetCameraMaterial(int cameraIndex) {
      return cameraMaterialsIDs[cameraIndex];
    }

    public RenderTexture GetCameraRenderTexture(int cameraIndex) {
      return cameraRenderTextures[cameraIndex];
    }

    public AsyncOperationHandle<BitmapDesc> GetBitmapDesc(BatchMaterialID materialID) {
      var cameraIndex = Array.IndexOf(cameraMaterialsIDs, materialID);

      if (materialID == noiseMaterialID)
        return Addressables.ResourceManager.CreateChainOperation(noiseBitmapSet, op => Addressables.ResourceManager.CreateCompletedOperation(op.Result.Description, null));
      if (cameraIndex != -1)
        return cameraSetLoaders[cameraIndex];
      else
        return Addressables.ResourceManager.CreateChainOperation(bitmapSetLoaders[materialIDToBitmapResource[materialID]], op => Addressables.ResourceManager.CreateCompletedOperation(op.Result.Description, null));
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
        return GetMaterial($"{SmallTextureIdBase + index}", lightmapped, decal);
      } else if (type == TextureType.Custom) {
        if (index >= FIRST_CAMERA_TMAP && index <= (FIRST_CAMERA_TMAP + NUM_HACK_CAMERAS)) {
          var cameraIndex = index - FIRST_CAMERA_TMAP;

          // if (hasCamera(cameraIndex))
          return GetCameraMaterial(cameraIndex);
          // else
          // return noiseMaterialID;
        } else if (index == REGULAR_STATIC_MAGIC_COOKIE || index == SHODAN_STATIC_MAGIC_COOKIE) {
          return noiseMaterialID;
        } else if (index >= FIRST_AUTOMAP_MAGIC_COOKIE && index <= (FIRST_AUTOMAP_MAGIC_COOKIE + NUM_AUTOMAP_MAGIC_COOKIES)) {
          return GetMaterial($"{CustomTextureIdBase}", lightmapped, decal); // TODO FIXME PLACEHOLDER
          // ret automap bitmap
        }

        var defaultMaterial = GetMaterial($"{CustomTextureIdBase + index}", lightmapped, decal);

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

    private class CreateNoiseTexture : AsyncOperationBase<BitmapSet> {
      protected override void Execute() {
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

        Complete(noiseBitmapSet, true, null);
      }
    }

    [BurstCompile]
    struct FillNoiseTexture : IJobParallelForBatch {
      [NativeSetThreadIndex] internal readonly int threadIndex;

      [ReadOnly] public byte ColorBase;
      [ReadOnly] public int Stride;

      [WriteOnly, NativeDisableParallelForRestriction] public NativeArray<byte> TextureData;
      [NativeDisableContainerSafetyRestriction] public NativeArray<Random> Randoms;

      public void Execute(int startIndex, int count) {
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
