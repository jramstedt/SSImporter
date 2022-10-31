using System.Collections.Generic;
using SS.ObjectProperties;
using SS.Resources;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Rendering;
using UnityEngine.ResourceManagement.AsyncOperations;
using Hash128 = UnityEngine.Hash128;

namespace SS.System {
  [CreateAfter(typeof(EntitiesGraphicsSystem))]
  [UpdateInGroup(typeof(InitializationSystemGroup))]
  public partial class MaterialProviderSystem : SystemBase {
    private const ushort CustomTextureIdBase = 2180;
    private const ushort SmallTextureIdBase = 321;

    private NativeParallelHashMap<Hash128, BatchMaterialID> lightmappedMaterials; // TODO FIXME instead of hash, create resource reference struct? Addressables toString?
    private NativeParallelHashMap<Hash128, BatchMaterialID> unlitMaterials; // TODO FIXME instead of hash, create resource reference struct? Addressables toString?
    private NativeParallelHashMap<BatchMaterialID, Hash128> materialIDToBitmapResource;
    private Dictionary<Hash128, AsyncOperationHandle<BitmapSet>> bitmapSetLoaders = new();

    private ComponentLookup<ObjectInstance> instanceLookup;
    private ComponentLookup<ObjectInstance.Decoration> decorationLookup;
    private EntitiesGraphicsSystem entitiesGraphicsSystem;
    private Resources.ObjectProperties objectProperties;

    private BatchMaterialID colorMaterialId;

    private Material lightmapMaterialTemplate;
    private Material unlitMaterialTemplate;

    protected override void OnCreate() {
      base.OnCreate();

      lightmappedMaterials = new(1024, Allocator.Persistent);
      unlitMaterials = new(1024, Allocator.Persistent);
      materialIDToBitmapResource = new(1024, Allocator.Persistent);

      instanceLookup = GetComponentLookup<ObjectInstance>(true);
      decorationLookup = GetComponentLookup<ObjectInstance.Decoration>(true);
      entitiesGraphicsSystem = World.GetOrCreateSystemManaged<EntitiesGraphicsSystem>();

      var clutTexture = Services.ColorLookupTableTexture.WaitForCompletion();
      var lightmap = Services.LightmapTexture.WaitForCompletion();
      objectProperties = Services.ObjectProperties.WaitForCompletion();

      lightmapMaterialTemplate = new Material(Shader.Find("Universal Render Pipeline/System Shock/Lightmap CLUT"));
      lightmapMaterialTemplate.SetTexture(Shader.PropertyToID(@"_LightGrid"), lightmap);
      lightmapMaterialTemplate.SetTexture(Shader.PropertyToID(@"_CLUT"), clutTexture);
      lightmapMaterialTemplate.DisableKeyword(@"_SPECGLOSSMAP");
      lightmapMaterialTemplate.DisableKeyword(@"_SPECULAR_COLOR");
      lightmapMaterialTemplate.DisableKeyword(@"_GLOSSINESS_FROM_BASE_ALPHA");
      lightmapMaterialTemplate.DisableKeyword(@"_ALPHAPREMULTIPLY_ON");
      lightmapMaterialTemplate.EnableKeyword(@"LINEAR");
      lightmapMaterialTemplate.SetFloat(@"_BlendOp", (float)UnityEngine.Rendering.BlendOp.Add);
      lightmapMaterialTemplate.SetFloat(@"_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
      lightmapMaterialTemplate.SetFloat(@"_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
      lightmapMaterialTemplate.enableInstancing = true;

      unlitMaterialTemplate = new Material(Shader.Find("Universal Render Pipeline/System Shock/CLUT"));
      unlitMaterialTemplate.SetTexture(Shader.PropertyToID(@"_CLUT"), clutTexture);
      unlitMaterialTemplate.DisableKeyword(@"_SPECGLOSSMAP");
      unlitMaterialTemplate.DisableKeyword(@"_SPECULAR_COLOR");
      unlitMaterialTemplate.DisableKeyword(@"_GLOSSINESS_FROM_BASE_ALPHA");
      unlitMaterialTemplate.DisableKeyword(@"_ALPHAPREMULTIPLY_ON");
      unlitMaterialTemplate.EnableKeyword(@"LINEAR");
      unlitMaterialTemplate.SetFloat(@"_BlendOp", (float)UnityEngine.Rendering.BlendOp.Add);
      unlitMaterialTemplate.SetFloat(@"_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
      unlitMaterialTemplate.SetFloat(@"_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
      unlitMaterialTemplate.enableInstancing = true;

      var colorMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit")); // TODO Create color material with nearest lookup
      colorMaterial.SetTexture(Shader.PropertyToID(@"_BaseMap"), clutTexture);
      colorMaterial.DisableKeyword(@"_SPECGLOSSMAP");
      colorMaterial.DisableKeyword(@"_SPECULAR_COLOR");
      colorMaterial.DisableKeyword(@"_GLOSSINESS_FROM_BASE_ALPHA");
      colorMaterial.DisableKeyword(@"_ALPHAPREMULTIPLY_ON");
      colorMaterial.EnableKeyword(@"LINEAR");
      colorMaterial.DisableKeyword(@"TRANSPARENCY_ON");
      colorMaterial.SetFloat(@"_BlendOp", (float)UnityEngine.Rendering.BlendOp.Add);
      colorMaterial.SetFloat(@"_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
      colorMaterial.SetFloat(@"_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
      colorMaterial.enableInstancing = true;

      colorMaterialId = entitiesGraphicsSystem.RegisterMaterial(colorMaterial);
    }

    protected override void OnUpdate() {

    }

    protected override void OnDestroy() {
      base.OnDestroy();

      lightmappedMaterials.Dispose();
      unlitMaterials.Dispose();
      materialIDToBitmapResource.Dispose();
    }

    public Material LightmapMaterial => lightmapMaterialTemplate;
    public Material UnlitMaterial => unlitMaterialTemplate;
    public BatchMaterialID ColorMaterialID => this.colorMaterialId;

    public BatchMaterialID GetMaterial (string resource, bool lightmapped) {
      var hash = Hash128.Compute(resource);

      var materials = lightmapped switch {
        true => lightmappedMaterials,
        false => unlitMaterials
      };

      if (materials.TryGetValue(hash, out var batchMaterialID))
        return batchMaterialID; // Res already loaded. Skip loading.

      Material material = lightmapped switch {
        true => new Material(this.lightmapMaterialTemplate),
        false => new Material(this.unlitMaterialTemplate)
      };

      batchMaterialID = this.entitiesGraphicsSystem.RegisterMaterial(material);

      if (materials.TryAdd(hash, batchMaterialID)) {
        materialIDToBitmapResource[batchMaterialID] = hash;

        if (!bitmapSetLoaders.TryGetValue(hash, out var loadOp)) {  // Check if BitmapSet already loaded.
          loadOp = Addressables.LoadAssetAsync<BitmapSet>(resource);
          bitmapSetLoaders.TryAdd(hash, loadOp);
        }

        loadOp.Completed += loadOp => {
          var bitmapSet = loadOp.Result;
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

      return BatchMaterialID.Null;
    }

    public AsyncOperationHandle<BitmapSet> GetBitmapSet(BatchMaterialID materialID) {
      return bitmapSetLoaders[materialIDToBitmapResource[materialID]];
    }
    
    public int CalculateTextureData (in Entity entity, in ObjectInstance instanceData, in Level level) {
      this.instanceLookup.Update(this); // FIXME
      this.decorationLookup.Update(this); // FIXME

      var baseProperties = objectProperties.BasePropertyData(instanceData);

      var textureData = 0;

      var isIndirectable = instanceData.Class == ObjectClass.Decoration &&
        (baseProperties.DrawType == DrawType.TexturedPolygon || baseProperties.DrawType == DrawType.TerrainPolygon);

      //(instanceData.Class == ObjectClass.Decoration && baseProperties.DrawType == DrawType.TexturedPolygon) ||
      //instanceData.Triple == 0x70208 || // SUPERSCREEN_TRIPLE
      //instanceData.Triple == 0x70209 || // BIGSCREEN_TRIPLE
      //instanceData.Triple == 0x70206; // SCREEN_TRIPLE

      if (isIndirectable) { // Must be ObjectClass.Decoration
        var decorationInstance = this.decorationLookup[entity];
        var data = decorationInstance.Data2;

        const int INDIRECTED_STUFF_INDICATOR_MASK = 0x1000;
        const int INDIRECTED_STUFF_DATA_MASK = 0xFFF;

        if (data != 0 /*|| animlist.ObjectIndex == objindex*/) {
          if ((data & INDIRECTED_STUFF_INDICATOR_MASK) != 0) {
            var dataEntity = level.ObjectInstances.Value[(int)data & INDIRECTED_STUFF_DATA_MASK];
            var databObjectInstance = this.instanceLookup[dataEntity];
            var dataDecorationInstance = this.decorationLookup[dataEntity];

            textureData = (int)dataDecorationInstance.Data2 + databObjectInstance.Info.CurrentFrame;
          } else {
            textureData = (int)decorationInstance.Data2 + instanceData.Info.CurrentFrame;
          }
        } else if (instanceData.SubClass == 1 /* BIGSTUFF_SUBCLASS_FURNISHING */) {
          const int SECRET_FURNITURE_DEFAULT_O3DREP = 0x80;
          textureData = SECRET_FURNITURE_DEFAULT_O3DREP;
        }
      } else if (baseProperties.DrawType != DrawType.TerrainPolygon || baseProperties.DrawType != DrawType.TexturedPolygon) {
        textureData = baseProperties.BitmapIndex;

        if (baseProperties.DrawType != DrawType.Voxel && instanceData.Class != ObjectClass.DoorAndGrating && instanceData.Info.CurrentFrame != byte.MaxValue) {
          textureData += instanceData.Info.CurrentFrame;
        }
      }

      return textureData;
    }

    public BatchMaterialID ParseTextureData (int textureData, bool lightmapped, out TextureType type, out int scale) {
      const int DATA_MASK = 0xFFF;

      const int INDEX_MASK = 0x007F;
      const int TYPE_MASK = 0x0180;
      const int SCALE_MASK = 0x0600;
      const int STYLE_MASK = 0x0800;

      const int RANDOM_TEXT_MAGIC_COOKIE = 0x7F;
      const int REGULAR_STATIC_MAGIC_COOKIE = 0x77;
      const int SHODAN_STATIC_MAGIC_COOKIE = 0x76;

      const int NUM_HACK_CAMERAS = 8;
      const int FIRST_CAMERA_TMAP = 0x78;

      const int NUM_AUTOMAP_MAGIC_COOKIES = 6;
      const int FIRST_AUTOMAP_MAGIC_COOKIE = 0x70;

      textureData &= DATA_MASK;

      var index = textureData & INDEX_MASK;
      type = (TextureType)((textureData & TYPE_MASK) >> 7);
      scale = (textureData & SCALE_MASK) >> 9;
      var style = (textureData & STYLE_MASK) == STYLE_MASK ? 2 : 3;

      if (type == TextureType.Alt) {
        return GetMaterial($"{SmallTextureIdBase + index}", lightmapped);
      } else if (type == TextureType.Custom) {
        if (index >= FIRST_CAMERA_TMAP && index <= (FIRST_CAMERA_TMAP + NUM_HACK_CAMERAS)) {
          var cameraIndex = index - FIRST_CAMERA_TMAP;
          return GetMaterial($"{CustomTextureIdBase}", lightmapped); // TODO FIXME PLACEHOLDER

          // if (hasCamera(cameraIndex))
          //  ret camera
          // else
          //  ret static
        } else if (index == REGULAR_STATIC_MAGIC_COOKIE || index == SHODAN_STATIC_MAGIC_COOKIE) {
          return GetMaterial($"{CustomTextureIdBase}", lightmapped); // TODO FIXME PLACEHOLDER
          // ret static
        } else if (index >= FIRST_AUTOMAP_MAGIC_COOKIE && index <= (FIRST_AUTOMAP_MAGIC_COOKIE + NUM_AUTOMAP_MAGIC_COOKIES)) {
          return GetMaterial($"{CustomTextureIdBase}", lightmapped); // TODO FIXME PLACEHOLDER
          // ret automap bitmap
        }

        // if (!HasRes(CustomTextureIdBase + index)) {
        //   ret static
        // } else {
          return GetMaterial($"{CustomTextureIdBase + index}", lightmapped); 
        // }

        
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
  }
}
