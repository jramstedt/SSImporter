using SS.Resources;
using System.Collections.Generic;
using SS.ObjectProperties;
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
using Object = UnityEngine.Object;
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
    private NativeParallelHashMap<(uint4 textIndex, byte color, byte style, bool lightmapped, bool decal), BatchMaterialID> freeTextMaterials;
    private NativeParallelHashMap<byte, BatchMaterialID> translucentMaterials;

    private readonly Dictionary<BatchMaterialID, IResHandle<TextureSet>> textureSetLoaders = new();

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

    private NativeArray<CameraTextureSet> cameraTextureSets;
    private NativeArray<byte> cameraSourceCount;
    private NativeArray<ushort> doorFrames;
    private NativeArray<ushort> enemyDirectionBase;
    private NativeArray<ushort> enemyPostureFrames;
    
    private Bitmap defaultBitmapDesc;
    
    private Resources.ObjectProperties objectProperties;

    protected override async void OnCreate() {
      base.OnCreate();

      RequireForUpdate<AsyncLoadTag>();
      
      cameraTextureSets = new NativeArray<CameraTextureSet>(NUM_HACK_CAMERAS, Allocator.Persistent);
      cameraSourceCount = new NativeArray<byte>(NUM_HACK_CAMERAS, Allocator.Persistent);
      doorFrames = new NativeArray<ushort>(DoorAndGrating.NUM_DOOR, Allocator.Persistent);
      enemyDirectionBase = new NativeArray<ushort>(Enemy.NUM_CRITTER, Allocator.Persistent);
      enemyPostureFrames = new NativeArray<ushort>(Enemy.NUM_CRITTER * Enemy.NUM_CRITTER_POSTURES * 8, Allocator.Persistent);
      
      EntityManager.AddComponentData(SystemHandle, new MaterialProviderSystemData {
        CameraTextureSets = cameraTextureSets,
        CameraSourceCount = cameraSourceCount,
        DoorFrames = doorFrames.AsReadOnly(),
        EnemyPostureFrames = enemyPostureFrames.AsReadOnly()
      });

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
      clutColorMaterialTemplate = new Material(Shader.Find(@"Shader Graphs/URP CLUT Color"));
      clutColorMaterialTemplate.EnableKeyword("_LIGHTGRID");
      decalMaterialTemplate = new Material(Shader.Find(@"Shader Graphs/URP Decal"));
      cameraMaterialTemplate = new Material(Shader.Find("Shader Graphs/URP Camera"));

      colorMaterialID = entitiesGraphicsSystem.RegisterMaterial(clutColorMaterialTemplate);

      {
        var noiseBitmapSet = CreateTexture(@"Noise", 32, 32);
        Material noiseMaterial = new(clutMaterialTemplate);
        noiseMaterial.SetTexture(shaderTextureName, noiseBitmapSet.Texture);
        noiseMaterial.DisableKeyword(ShaderKeywordStrings._ALPHATEST_ON);
        noiseMaterial.DisableKeyword(@"_LIGHTGRID");

        noiseMaterialID = entitiesGraphicsSystem.RegisterMaterial(noiseMaterial);
        textureSetLoaders.Add(noiseMaterialID, new CompletedLoader<TextureSet>(noiseBitmapSet));

        noiseMaterial = new(clutDecalMaterialTemplate);
        noiseMaterial.SetTexture(shaderTextureName, noiseBitmapSet.Texture);
        noiseMaterial.DisableKeyword(ShaderKeywordStrings._ALPHATEST_ON);
        noiseMaterial.DisableKeyword(@"_LIGHTGRID");

        decalNoiseMaterialID = entitiesGraphicsSystem.RegisterMaterial(noiseMaterial);
        textureSetLoaders.Add(decalNoiseMaterialID, new CompletedLoader<TextureSet>(noiseBitmapSet));
      }

      {
        for (var i = 0; i < NUM_HACK_CAMERAS; ++i) {
          cameraTextureSets[i] = new () {
            Texture = new RenderTexture(new RenderTextureDescriptor(128, 128, RenderTextureFormat.ARGB32, 16)) {
              name = @"Camera",
              filterMode = FilterMode.Point,
              wrapMode = TextureWrapMode.Repeat
            },
            Description = new () {
              Transparent = false,
              Size = new(32, 32),
              AnchorPoint = new(16, 16),
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
      };
      
      objectProperties = await Services.ObjectProperties;

      #region Cache animation constants
      {
        ushort enemyDirectionCount = 0;
        for (var enemyIndex = 0; enemyIndex < Enemy.NUM_CRITTER; ++enemyIndex) {
          enemyDirectionBase[enemyIndex] = enemyDirectionCount;
          var directions = objectProperties.ObjectDatasBlobAsset.Value.EnemyProps[enemyIndex].Views;
          
          // Debug.Log($"Enemy property cache ei:{enemyIndex} dc:{directions}");
          
          for (var posture = 0; posture < Enemy.NUM_CRITTER_POSTURES; ++posture) {
            var (resBase, directional) = (ObjectInstance.Enemy.PostureType)posture switch {
              ObjectInstance.Enemy.PostureType.Standing => (0x631, true), // RES_bmCritterStanding_0
              ObjectInstance.Enemy.PostureType.Moving => (0x758, true), // RES_bmCritterMovement_0
              ObjectInstance.Enemy.PostureType.Attacking => (0x578, false), // RES_bmCritterAttack_0
              ObjectInstance.Enemy.PostureType.AttackRest => (0x59d, false), // RES_bmCritterAttackRest_0
              ObjectInstance.Enemy.PostureType.Knockback => (0x5e7, false), // RES_bmCritterKnockback_0
              ObjectInstance.Enemy.PostureType.Death => (0x5c2, false), // RES_bmCritterDeath_0
              ObjectInstance.Enemy.PostureType.Disrupt => (0x60c, false), // RES_bmCritterDisrupt_0
              ObjectInstance.Enemy.PostureType.Attacking2 => (0x841, false), // RES_bmCritterAttack2_0
              _ => (0x631, true) // Default
            };

            if (directional) {
              for (var direction = 0; direction < directions; ++direction) {
                enemyPostureFrames[(enemyIndex * posture * 8) + direction] = await Res.GetResourceBlockCount((ushort)(resBase + enemyDirectionCount + direction));
                // Debug.Log($"Enemy property cache d:{direction} frames:{enemyPostureFrames[(enemyIndex * posture * 8) + direction]}");
              }
            } else {
              enemyPostureFrames[enemyIndex * posture * 8] = await Res.GetResourceBlockCount((ushort)(resBase + enemyIndex));
              // Debug.Log($"Enemy property cache frames:{enemyPostureFrames[(enemyIndex * posture * 8) + 0]}");
            }
          }
          
          enemyDirectionCount += directions;
        }
      }

      {
        for (var doorIndex = 0; doorIndex < DoorAndGrating.NUM_DOOR; ++doorIndex)
          doorFrames[doorIndex] = await Res.GetResourceBlockCount((ushort)(DoorResourceIdBase + doorIndex));
      }
      #endregion
      
      EntityManager.AddComponent<AsyncLoadTag>(SystemHandle);
    }

    protected override void OnDestroy() {
      base.OnDestroy();

      bitmapMaterials.Dispose();
      cameraMaterials.Dispose();
      wordMaterials.Dispose();
      textMaterials.Dispose();
      freeTextMaterials.Dispose();

      randoms.Dispose();

      foreach (var cameraTextureSet in cameraTextureSets) {
        if (cameraTextureSet.Texture.IsValid())
          Object.Destroy(cameraTextureSet.Texture);
      }

      foreach (var textureSetLoader in textureSetLoaders.Values) {
        if (textureSetLoader.IsCompleted)
          textureSetLoader.Result.Dispose();
      }

      translucencyTable.Dispose();
      
      enemyDirectionBase.Dispose();
      enemyPostureFrames.Dispose();
      doorFrames.Dispose();
      
      cameraTextureSets.Dispose();
      cameraSourceCount.Dispose();
    }

    protected override void OnUpdate() {
      if (textureSetLoaders.TryGetValue(noiseMaterialID, out var noiseBitmapSetLoader)) {
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

    public BatchMaterialID GetMaterial(ushort resId, ushort blockIndex, bool lightmapped, bool decal, bool smoothScale) {
      var resRef = (uint)((resId << 16) | blockIndex);

      if (bitmapMaterials.TryGetValue((resRef, lightmapped, decal), out var batchMaterialID))
        return batchMaterialID; // Res already loaded. Skip loading.

      Material material = new(decal ? clutDecalMaterialTemplate : clutMaterialTemplate);
      if (lightmapped) material.EnableKeyword(@"_LIGHTGRID");
      else material.DisableKeyword(@"_LIGHTGRID");

      batchMaterialID = entitiesGraphicsSystem.RegisterMaterial(material);

      if (bitmapMaterials.TryAdd((resRef, lightmapped, decal), batchMaterialID)) {
        LoadBitmapToMaterial(resRef, batchMaterialID, smoothScale, smoothScale || decal ? TextureWrapMode.Clamp : TextureWrapMode.Repeat);
        return batchMaterialID;
      }

      Debug.LogWarning($"GetMaterial failed for {resRef:X8}.");

      return BatchMaterialID.Null;
    }

    public BatchMaterialID GetTextureMaterial(ushort textureIndex) {
      return GetMaterial((ushort)(Texture128ResourceIdBase + textureIndex), 0, true, false, false); // Uses the 128x128 resource id
    }

    public BatchMaterialID GetMeshTextureMaterial(ushort textureIndex) {
      return GetMaterial((ushort)(CustomTextureIdBase + textureIndex), 0, true, false, false);
    }

    public BatchMaterialID GetCameraMaterial(int cameraIndex, bool lightmapped, bool decal) {
      if (cameraMaterials.TryGetValue((cameraIndex, lightmapped, decal), out var batchMaterialID))
        return batchMaterialID; // Res already loaded. Skip loading.

      Material material = new(decal ? decalMaterialTemplate : cameraMaterialTemplate);
      if (lightmapped) material.EnableKeyword(@"_LIGHTGRID");
      else material.DisableKeyword(@"_LIGHTGRID");

      batchMaterialID = entitiesGraphicsSystem.RegisterMaterial(material);

      if (cameraMaterials.TryAdd((cameraIndex, lightmapped, decal), batchMaterialID)) {
        var cameraTexture = cameraTextureSets[cameraIndex].Texture;
        material.SetTexture(shaderTextureName, cameraTexture);
        return batchMaterialID;
      }

      Debug.LogWarning($"GetCameraMaterial failed for {cameraIndex}.");

      return BatchMaterialID.Null;
    }

    public RenderTexture GetCameraRenderTexture(int cameraIndex) {
      return cameraTextureSets[cameraIndex].Texture;
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

    public BatchMaterialID GetTextMaterial(ref NativeText.ReadOnly fullText, byte color, byte style, bool lightmapped, bool decal) {
      uint4 hash;
      unsafe { 
        hash = xxHash3.Hash128(fullText.GetUnsafePtr(), fullText.Length);
      }

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

      if (translucentMaterials.TryAdd(colorIndex, batchMaterialID))
        return batchMaterialID;

      Debug.LogWarning($"GetTranslucentMaterial failed for {colorIndex}.");

      return BatchMaterialID.Null;
    }

    private async void LoadBitmapToMaterial(uint resRef, BatchMaterialID batchMaterialID, bool smoothScale, TextureWrapMode wrapMode) {
      if (!textureSetLoaders.TryGetValue(batchMaterialID, out var textureSetLoadOp)) { // Check if BitmapSet already loaded.
        var bitmapSetLoader = Res.Load<BitmapSet>(resRef);
        textureSetLoadOp = new PickResultLoader<TextureSet, BitmapSet>(bitmapSetLoader, smoothScale ? CreateDoubledTexture : CreateTexture);
        bitmapSetLoader.Result.Dispose(); // TODO Res ref count?
        textureSetLoaders.TryAdd(batchMaterialID, textureSetLoadOp);
      }

      var bitmapSet = await textureSetLoadOp;
      bitmapSet.Texture.wrapMode = wrapMode;
      
      var material = entitiesGraphicsSystem.GetMaterial(batchMaterialID);
      material.SetTexture(shaderTextureName, bitmapSet.Texture);

      if (bitmapSet.Description.Transparent)
        material.EnableKeyword(ShaderKeywordStrings._ALPHATEST_ON);
      else
        material.DisableKeyword(ShaderKeywordStrings._ALPHATEST_ON);
    }

    private async void RenderTextAsync(BatchMaterialID batchMaterialID, TextType type, ushort wordIndex, byte color, byte style, bool scroll) {
      var (settings, colorIndex, fontRes) = GraphicUtils.GetTextProperties(type, color, style);

      if (!textureSetLoaders.TryGetValue(batchMaterialID, out var textureSetLoadOp)) {
        textureSetLoadOp = new CompletedLoader<TextureSet>(CreateTexture(@"Rendered text", settings.Width, settings.Height, true));
        textureSetLoaders.TryAdd(batchMaterialID, textureSetLoadOp);
      }

      var bitmapSet = await textureSetLoadOp;

      unsafe {
        var rawData = bitmapSet.Texture.GetRawTextureData<byte>();
        UnsafeUtility.MemClear(rawData.GetUnsafePtr(), rawData.Length);
      }

      var material = entitiesGraphicsSystem.GetMaterial(batchMaterialID);
      material.SetTexture(shaderTextureName, bitmapSet.Texture);

      var fontSet = await Res.Load<FontSet>(fontRes);

      var textureData = bitmapSet.Texture.GetRawTextureData<byte>();
      
      if (scroll) {
        byte scrollIndex = 0;
        var textPos = new int2(1, 1);

        while (textPos.y < settings.Height) {
          var fullText = await Res.Load<NativeText>(settings.ResId, (ushort)(wordIndex + scrollIndex));

          GraphicUtils.MeasureString(fontSet, fullText.AsReadOnly(), out var textSize);

          GraphicUtils.DrawString(ref textureData, bitmapSet.Texture.format, new int2(bitmapSet.Texture.width, bitmapSet.Texture.height), fontSet, fullText.AsReadOnly(), textPos, colorIndex);

          textPos.y += textSize.y + 1;
          ++scrollIndex;
        }
      } else {
        var fullText = await Res.Load<NativeText>(settings.ResId, wordIndex);

        GraphicUtils.MeasureString(fontSet, fullText.AsReadOnly(), out var textSize);
        var textPos = max((new int2(settings.Width, settings.Height) - textSize) >> 1, new int2(1, 0));

        GraphicUtils.DrawString(ref textureData, bitmapSet.Texture.format, new int2(bitmapSet.Texture.width, bitmapSet.Texture.height), fontSet, fullText.AsReadOnly(), textPos, colorIndex);
      }
      
      bitmapSet.Texture.Apply(false, false);
    }

    private async void RenderTextAsync(BatchMaterialID batchMaterialID, TextType type, NativeText.ReadOnly fullText, byte color, byte style) {
      var (settings, colorIndex, fontRes) = GraphicUtils.GetTextProperties(type, color, style);

      if (!textureSetLoaders.TryGetValue(batchMaterialID, out var textureSetLoadOp)) {
        textureSetLoadOp = new CompletedLoader<TextureSet>(CreateTexture(@"Rendered text", settings.Width, settings.Height, true));
        textureSetLoaders.TryAdd(batchMaterialID, textureSetLoadOp);
      }

      var bitmapSet = await textureSetLoadOp;

      unsafe {
        var rawData = bitmapSet.Texture.GetRawTextureData<byte>();
        UnsafeUtility.MemClear(rawData.GetUnsafePtr(), rawData.Length);
      }

      var material = entitiesGraphicsSystem.GetMaterial(batchMaterialID);
      material.SetTexture(shaderTextureName, bitmapSet.Texture);

      var fontSet = await Res.Load<FontSet>(fontRes);

      GraphicUtils.MeasureString(fontSet, fullText, out var textSize);
      var textPos = max((new int2(settings.Width, settings.Height) - textSize) >> 1, new int2(1, 0));

      var textureData = bitmapSet.Texture.GetRawTextureData<byte>();
      GraphicUtils.DrawString(ref textureData, bitmapSet.Texture.format, new int2(bitmapSet.Texture.width, bitmapSet.Texture.height), fontSet, fullText, textPos, colorIndex);
      bitmapSet.Texture.Apply(false, false);
    }

    public async Awaitable<Bitmap> GetBitmapDesc(BatchMaterialID materialID) {
      if (textureSetLoaders.TryGetValue(materialID, out var textureSetLoader))
        return (await textureSetLoader).Description;

      var cameraIndex = cameraMaterials.GetValueArray(Allocator.Temp).IndexOf(materialID);
      return cameraIndex != -1 ? cameraTextureSets[cameraIndex].Description : defaultBitmapDesc;
    }

    public IResHandle<TextureSet> GetTextureLoader(BatchMaterialID materialID) {
      return textureSetLoaders[materialID];
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
        return GetMaterial((ushort)(SmallTextureIdBase + index), 0, lightmapped, decal, false);
      } else if (type == TextureType.Custom) {
        if (index >= FIRST_CAMERA_TMAP && index <= (FIRST_CAMERA_TMAP + NUM_HACK_CAMERAS)) {
          var cameraIndex = index - FIRST_CAMERA_TMAP;

          if (cameraSourceCount[cameraIndex] > 0)
            return GetCameraMaterial(cameraIndex, lightmapped, decal);
          else
            return decal ? decalNoiseMaterialID : noiseMaterialID;
        } else if (index == REGULAR_STATIC_MAGIC_COOKIE || index == SHODAN_STATIC_MAGIC_COOKIE) {
          return decal ? decalNoiseMaterialID : noiseMaterialID;
        } else if (index >= FIRST_AUTOMAP_MAGIC_COOKIE && index <= (FIRST_AUTOMAP_MAGIC_COOKIE + NUM_AUTOMAP_MAGIC_COOKIES)) {
          return decal ? decalNoiseMaterialID : noiseMaterialID;
          // ret automap bitmap
        }

        var defaultMaterial = GetMaterial((ushort)(CustomTextureIdBase + index), 0, lightmapped, decal, false);

        if (defaultMaterial == BatchMaterialID.Null)
          return decal ? decalNoiseMaterialID : noiseMaterialID;

        return defaultMaterial;
      } else if (type == TextureType.Text) {
        if (index == RANDOM_TEXT_MAGIC_COOKIE) {
          var seed = TimeUtils.SecondsToFastTicks(SystemAPI.Time.ElapsedTime) >> 7;
          var number = ((seed * 9277 + 7) % 14983) % 10;

          var text = new NativeText($"{number}", Allocator.Temp).AsReadOnly();
          return GetTextMaterial(ref text, 0 /* style >> 16 */, style, lightmapped, decal);
        } else {
          return GetTextMaterial(index, 0 /* style >> 16 */, style, lightmapped, decal, false);
        }
      } else if (type == TextureType.ScrollText) {
        return GetTextMaterial(index, 0 /* style >> 16 */, style, lightmapped, decal, true);
      }

      return BatchMaterialID.Null;
    }
    
    public ushort GetEnemyDirectionBase(Triple triple) {
      var classIndex = objectProperties.ClassPropertyIndex(triple);
      return enemyDirectionBase[classIndex];
    }
    
    
    public BatchMaterialID GetResource (
      in Entity entity,
      in ObjectInstance instanceData,
      in Level level,
      in ComponentLookup<ObjectInstance> instanceLookup,
      in ComponentLookup<ObjectInstance.Decoration> decorationLookup,
      in ComponentLookup<ObjectInstance.DoorAndGrating> doorLookup,
      in ComponentLookup<ObjectInstance.Enemy> enemyLookup,
      in bool decal,
      out ushort refWidthOverride
    ) {
      var baseProperties = objectProperties.BasePropertyData(instanceData);

      refWidthOverride = 0;

      if (baseProperties.DrawType == DrawType.TerrainPolygon) {
        const int DESTROYED_SCREEN_ANIM_BASE = 0x1B;

        if (instanceData.Class == ObjectClass.Decoration) {
          var decorationData = decorationLookup.GetRefRO(entity).ValueRO; // TODO FIXME this is also called in CalculateTextureData
          var isAnimating = IsAnimated(decorationData.Link.ObjectIndex, level.Animations.AsReadOnly());
          var textureData = CalculateTextureData(entity, baseProperties, instanceData, level, instanceLookup, decorationLookup, isAnimating);

          if (instanceData.Triple == 0x70207) { // TMAP_TRIPLE
            refWidthOverride = 128;
            return GetMaterial((ushort)(0x03E8 + level.TextureMap[textureData]), 0, true, decal, false);
          } else if (instanceData.Triple == 0x70208) { // SUPERSCREEN_TRIPLE
            var lightmapped = decorationData.Data2 == DESTROYED_SCREEN_ANIM_BASE + 3; // screen is full bright if not destroyed
            refWidthOverride = 128; // 1 << 7
            return ParseTextureData(textureData, lightmapped, decal, out var textureType, out var scale);
          } else if (instanceData.Triple == 0x70209) { // BIGSCREEN_TRIPLE
            var lightmapped = decorationData.Data2 == DESTROYED_SCREEN_ANIM_BASE + 3; // screen is full bright if not destroyed
            refWidthOverride = 64; // 1 << 6
            return ParseTextureData(textureData, lightmapped, decal, out var textureType, out var scale);
          } else if (instanceData.Triple == 0x70206) { // SCREEN_TRIPLE
            var lightmapped = decorationData.Data2 == DESTROYED_SCREEN_ANIM_BASE + 3; // screen is full bright if not destroyed
            refWidthOverride = 32; // 1 << 5
            return ParseTextureData(textureData, lightmapped, decal, out var textureType, out var scale);
          } else {
            var materialID = ParseTextureData(textureData, true, decal, out var textureType, out var scale);
            refWidthOverride = (ushort)(1 << scale);
            return materialID;
          }
        }
      } else if (baseProperties.DrawType == DrawType.FlatTexture) {
        if (instanceData.Class == ObjectClass.Decoration) {
          if (instanceData.Triple == 0x70203) { // WORDS_TRIPLE
            const byte MEDIAN_WORD_SCALE = 4;

            var decorationData = decorationLookup.GetRefRO(entity).ValueRO;
            byte size = decorationData.WordScale;

            var colorIndex = decorationData.WordColor;
            var style = decorationData.WordStyle;
            var wordIndex = decorationData.WordIndex;

            if (size != 0)
              size -= MEDIAN_WORD_SCALE;

            refWidthOverride = (ushort)(size == 0 ? 128 : (1 << (7 + size))); // 1 << 7 = 128 is default word size

            return GetWordMaterial(wordIndex, colorIndex, style);
          } else if (instanceData.Triple == 0x70201) { // ICON_TRIPLE
            return GetMaterial(IconResourceIdBase, (ushort)instanceData.Info.CurrentFrame, true, decal, false);
          } else if (instanceData.Triple == 0x70202) { // GRAF_TRIPLE
            return GetMaterial(GraffitiResourceIdBase, (ushort)instanceData.Info.CurrentFrame, true, decal, false);
          } else if (instanceData.Triple == 0x7020a) { // REPULSWALL_TRIPLE
            return GetMaterial(RepulsorResourceIdBase, (ushort)instanceData.Info.CurrentFrame, true, decal, false);
          }
        } else if (instanceData.Class == ObjectClass.DoorAndGrating) {
          // Debug.Log($"{DoorResourceIdBase} {objectProperties.ClassPropertyIndex(instanceData)} : {instanceData.Info.CurrentFrame}");
          return GetMaterial((ushort)(DoorResourceIdBase + objectProperties.ClassPropertyIndex(instanceData)), (ushort)instanceData.Info.CurrentFrame, true, decal, false);
        }
      } else if (baseProperties.DrawType == DrawType.TranslucentPolygon) {
        byte colorIndex = 0;

        // TODO ObjectClass.Item

        if (instanceData.Class == ObjectClass.Decoration) {
          var decorationData = decorationLookup.GetRefRO(entity).ValueRO;
          colorIndex = (byte)decorationData.Data2;
        } else if (instanceData.Class == ObjectClass.DoorAndGrating) {
          var doorData = doorLookup.GetRefRO(entity).ValueRO;
          colorIndex = doorData.Color;
        }

        if (colorIndex == 0) colorIndex = 0xFF;

        return GetTranslucentMaterial(colorIndex);
      } else if (baseProperties.DrawType is DrawType.DirectionalEnemySprite or DrawType.DirectionalSprite) {
        // ref_from_critter_data
        
        // Obj pos to eye pos delta
        // View Dir
        
        var direction = ObjectInstance.Enemy.ViewDirection.Front; // TODO
        var transparent = false;

        if (instanceData.Class == ObjectClass.Enemy) {
          var enemyData = enemyLookup.GetRefRO(entity).ValueRO;

          // TODO DIEGO_TRIPLE DIEGO_DEATH_BATTLE_LEVEL etc.
          
          var posture = enemyData.Posture;
          if (instanceData.Triple == 0xe0400 /* ROBOBABE_TRIPLE */) { // SHODAN
            posture = ObjectInstance.Enemy.PostureType.Standing;
            direction = 0;
          } else if (instanceData.Triple == 0xe0007 /* INVISO_CRIT_TRIPLE */) {
            transparent = true; // TODO
          }

          var classIndex = objectProperties.ClassPropertyIndex(instanceData);

          var (resBase, directional) = posture switch {
            ObjectInstance.Enemy.PostureType.Standing => (0x631, true), // RES_bmCritterStanding_0
            ObjectInstance.Enemy.PostureType.Moving => (0x758, true), // RES_bmCritterMovement_0
            ObjectInstance.Enemy.PostureType.Attacking => (0x578, false), // RES_bmCritterAttack_0
            ObjectInstance.Enemy.PostureType.AttackRest => (0x59d, false), // RES_bmCritterAttackRest_0
            ObjectInstance.Enemy.PostureType.Knockback => (0x5e7, false), // RES_bmCritterKnockback_0
            ObjectInstance.Enemy.PostureType.Death => (0x5c2, false), // RES_bmCritterDeath_0
            ObjectInstance.Enemy.PostureType.Disrupt => (0x60c, false), // RES_bmCritterDisrupt_0
            ObjectInstance.Enemy.PostureType.Attacking2 => (0x841, false), // RES_bmCritterAttack2_0
            _ => (0x631, true) // Default
          };

          if (!directional)
            return GetMaterial((ushort)(resBase + classIndex), (ushort)instanceData.Info.CurrentFrame, true, false, true);
          
          //ushort frames = enemyPostureFrames[(classIndex * (int)posture * 8) + (int)direction];
          var directionBase = GetEnemyDirectionBase(instanceData);
          //var frame = (ushort)(instanceData.Info.CurrentFrame % frames);
          var frame = (ushort)instanceData.Info.CurrentFrame;
            
          return GetMaterial((ushort)(resBase + directionBase + (int)direction), frame, true, false, true);
        }
        
        var textureData = CalculateTextureData(entity, baseProperties, instanceData, level, instanceLookup, decorationLookup, false);
        return ParseTextureData(textureData + (int)direction, true, decal, out var textureType, out var scale);
      }

      return BatchMaterialID.Null;
    }

    /*
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
    */

    [BurstCompile]
    private struct FillNoiseTexture : IJobParallelForBatch {
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

    public struct CameraTextureSet {
      public UnityObjectRef<RenderTexture> Texture;
      public Bitmap Description;
    }
    
    public struct MaterialProviderSystemData : IComponentData {
      public NativeArray<CameraTextureSet> CameraTextureSets;
      public NativeArray<byte> CameraSourceCount;
      public NativeArray<ushort>.ReadOnly DoorFrames;
      public NativeArray<ushort>.ReadOnly EnemyPostureFrames;
    }
    
    private struct AsyncLoadTag : IComponentData { }
  }
}
