﻿using UnityEngine;
using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using System;
using UnityEngine.ResourceManagement.ResourceLocations;
using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using System.Threading.Tasks;
using Unity.Entities;
using Unity.Collections;
using Unity.Transforms;
using SS.System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Assertions;

namespace SS.Resources {
  public static class SaveLoader {
    private const ushort SaveGameResourceIdBase = 4000;
    private const ushort NumResourceIdsPerLevel = 100;

    private static ushort ResourceIdFromLevel (byte level) => (ushort)(SaveGameResourceIdBase + (level * NumResourceIdsPerLevel));

    public static async Task<World> LoadMap(byte mapId, string dataPath, string saveGameFile) {
      var loadOp = Addressables.ResourceManager.ProvideResource<ResourceFile>(new ResourceLocationBase(@"savegame", $"{dataPath}\\{saveGameFile}", typeof(ResourceFileProvider).FullName, typeof(ResourceFile)));
      var paletteOp = Addressables.LoadAssetAsync<Palette>(0x02BC);
      var shadetableOp = Addressables.LoadAssetAsync<ShadeTableData>(new ResourceLocationBase(@"SHADTABL.DAT", dataPath + @"\SHADTABL.DAT", typeof(RawDataProvider).FullName, typeof(ShadeTableData)));
      var texturePropertiesOp = Addressables.LoadAssetAsync<TexturePropertiesData>(new ResourceLocationBase(@"TEXTPROP.DAT", dataPath + @"\TEXTPROP.DAT", typeof(RawDataProvider).FullName, typeof(TexturePropertiesData)));

      var saveData = await loadOp.Task;
      var palette = await paletteOp.Task; // TODO should be global.
      var shadetable = await shadetableOp.Task; // TODO should be global.
      var allTextureProperties = await texturePropertiesOp.Task;

      ushort resourceId = ResourceIdFromLevel(mapId);
      var levelInfo = saveData.GetResourceData<LevelInfo>((ushort)(0x0004 + resourceId));
      var tileMap = ReadMapElements(saveData.GetResourceData((ushort)(0x0005 + resourceId), 0), levelInfo);
      var schedules = saveData.GetResourceData<ScheduleEvent>((ushort)(0x0006 + resourceId));
      var textureMap = saveData.GetResourceData<TextureMap>((ushort)(0x0007 + resourceId));

      var objectInstances = saveData.GetResourceDataArray<ObjectInstance>((ushort)(0x008 + resourceId));
      // 0x0009 Cross reference table

      var weaponInstances = saveData.GetResourceDataArray<ObjectInstance.Weapon>((ushort)(0x000A + resourceId));
      var ammunitionInstances = saveData.GetResourceDataArray<ObjectInstance.Ammunition>((ushort)(0x000B + resourceId));
      var projectileInstances = saveData.GetResourceDataArray<ObjectInstance.Projectile>((ushort)(0x000C + resourceId));
      var explosiveInstances = saveData.GetResourceDataArray<ObjectInstance.Explosive>((ushort)(0x000D + resourceId));
      var dermalInstances = saveData.GetResourceDataArray<ObjectInstance.DermalPatch>((ushort)(0x000E + resourceId));
      var hardwareInstances = saveData.GetResourceDataArray<ObjectInstance.Hardware>((ushort)(0x000F + resourceId));
      var softwareLogInstances = saveData.GetResourceDataArray<ObjectInstance.SoftwareAndLog>((ushort)(0x0010 + resourceId));
      var decorationInstances = saveData.GetResourceDataArray<ObjectInstance.Decoration>((ushort)(0x0011 + resourceId));
      var itemInstances = saveData.GetResourceDataArray<ObjectInstance.Item>((ushort)(0x0012 + resourceId));
      var interfaceInstances = saveData.GetResourceDataArray<ObjectInstance.Interface>((ushort)(0x0013 + resourceId));
      var doorGratingInstances = saveData.GetResourceDataArray<ObjectInstance.DoorAndGrating>((ushort)(0x0014 + resourceId));
      var animatedInstances = saveData.GetResourceDataArray<ObjectInstance.Animated>((ushort)(0x0015 + resourceId));
      var triggerInstances = saveData.GetResourceDataArray<ObjectInstance.Trigger>((ushort)(0x0016 + resourceId));
      var containerInstances = saveData.GetResourceDataArray<ObjectInstance.Container>((ushort)(0x0017 + resourceId));
      var enemyInstances = saveData.GetResourceDataArray<ObjectInstance.Enemy>((ushort)(0x0018 + resourceId));

      var weaponTemplates = saveData.GetResourceDataArray<ObjectInstance.Weapon>((ushort)(0x0019 + resourceId));
      var ammunitionTemplates = saveData.GetResourceDataArray<ObjectInstance.Ammunition>((ushort)(0x001A + resourceId));
      var projectileTemplates = saveData.GetResourceDataArray<ObjectInstance.Projectile>((ushort)(0x001B + resourceId));
      var explosiveTemplates = saveData.GetResourceDataArray<ObjectInstance.Explosive>((ushort)(0x0001C + resourceId));
      var dermalTemplates = saveData.GetResourceDataArray<ObjectInstance.DermalPatch>((ushort)(0x001D + resourceId));
      var hardwareTemplates = saveData.GetResourceDataArray<ObjectInstance.Hardware>((ushort)(0x001E + resourceId));
      var softwareLogTemplates = saveData.GetResourceDataArray<ObjectInstance.SoftwareAndLog>((ushort)(0x001F + resourceId));
      var decorationTemplates = saveData.GetResourceDataArray<ObjectInstance.Decoration>((ushort)(0x0020 + resourceId));
      var itemTemplates = saveData.GetResourceDataArray<ObjectInstance.Item>((ushort)(0x0021 + resourceId));
      var interfaceTemplates = saveData.GetResourceDataArray<ObjectInstance.Interface>((ushort)(0x0022 + resourceId));
      var doorGratingTemplates = saveData.GetResourceDataArray<ObjectInstance.DoorAndGrating>((ushort)(0x0023 + resourceId));
      var animatedTemplates = saveData.GetResourceDataArray<ObjectInstance.Animated>((ushort)(0x0024 + resourceId));
      var triggerTemplates = saveData.GetResourceDataArray<ObjectInstance.Trigger>((ushort)(0x0025 + resourceId));
      var containerTemplates = saveData.GetResourceDataArray<ObjectInstance.Container>((ushort)(0x0026 + resourceId));
      var enemyTemplates = saveData.GetResourceDataArray<ObjectInstance.Enemy>((ushort)(0x0027 + resourceId));

      var textureAnimation = saveData.GetResourceDataArray<TextureAnimationData>((ushort)(0x002A + resourceId));

      // 0x002B surveillance sources
      // 0x002C surveillance surrogates
      // 0x002D level variables
      // 0x002E map notes
      // 0x002F map notes pointers
      // 0x0030 Player physic state, unused
      var pathfindingPaths = saveData.GetResourceDataArray<Path>((ushort)(0x0031 + resourceId));
      var pathfindingPathsUsed = saveData.GetResourceData<ushort>((ushort)(0x0032 + resourceId));

      var animationLoops = saveData.GetResourceDataArray<AnimationLoop>((ushort)(0x0033 + resourceId));
      var animationCounter = saveData.GetResourceData<ushort>((ushort)(0x0034 + resourceId));

      var heightSemaphores = saveData.GetResourceData<HeightSemaphore>((ushort)(0x0034 + resourceId));

      // Initialize systems
      var defaultSystems = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default);

      var world = World.DefaultGameObjectInjectionWorld;
      //var world = new World($"map{mapId:D}");
      var entityManager = world.EntityManager;

      var lightmapSystem = world.GetOrCreateSystem<LightmapBuilderSystem>();
      lightmapSystem.lightmap = CreateLightmap(in levelInfo);
      
      var clutTexture = CreateColorLookupTable(palette, shadetable);

      var textures = new BitmapSet[textureMap.blockIndex.Length];
      var textureProperties = new TextureProperties[textureMap.blockIndex.Length];
      var materials = new Dictionary<ushort, Material>(textureMap.blockIndex.Length);
      for (ushort i = 0; i < textureMap.blockIndex.Length; ++i) {
        if (materials.ContainsKey(i)) continue;

        var textureIndex = textureMap.blockIndex[i];

        textureProperties[i] = allTextureProperties[textureIndex];

        var bitmapSet = await CreateMipmapTexture(textureIndex); // TODO instead of await, run parallel
        textures[i] = bitmapSet;

        var material = new Material(Shader.Find("Universal Render Pipeline/System Shock/Lightmap CLUT"));
        material.SetTexture(Shader.PropertyToID(@"_BaseMap"), bitmapSet.Texture);
        material.SetTexture(Shader.PropertyToID(@"_CLUT"), clutTexture);
        material.SetTexture(Shader.PropertyToID(@"_LightGrid"), lightmapSystem.lightmap);
        material.DisableKeyword(@"_SPECGLOSSMAP");
        material.DisableKeyword(@"_SPECULAR_COLOR");
        material.DisableKeyword(@"_GLOSSINESS_FROM_BASE_ALPHA");
        material.DisableKeyword(@"_ALPHAPREMULTIPLY_ON");

        material.EnableKeyword(@"LINEAR");
        if (bitmapSet.Transparent) material.EnableKeyword(@"TRANSPARENCY_ON");
        else material.DisableKeyword(@"TRANSPARENCY_ON");

        material.SetFloat(@"_BlendOp", (float)UnityEngine.Rendering.BlendOp.Add);
        material.SetFloat(@"_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
        material.SetFloat(@"_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
        material.enableInstancing = true;
        materials.Add(i, material);
      }

      var mapSystem = world.GetOrCreateSystem<MapElementBuilderSystem>();
      mapSystem.mapMaterial = materials;

      var textureAnimationArchetype = entityManager.CreateArchetype(typeof(TextureAnimationData));
      var textureAnimationEntities = entityManager.CreateEntity(textureAnimationArchetype, textureAnimation.Length, Allocator.Persistent);
      var animateTexturesSystem = world.GetOrCreateSystem<AnimateTexturesSystem>();
      animateTexturesSystem.textures = textures;
      animateTexturesSystem.textureProperties = textureProperties;
      animateTexturesSystem.mapMaterial = materials;
      animateTexturesSystem.textureAnimationEntities = textureAnimationEntities;

      var paletteEffectSystem = world.GetOrCreateSystem<PaletteEffectSystem>();
      paletteEffectSystem.clut = clutTexture;
      paletteEffectSystem.shadeTable = shadetable;
      paletteEffectSystem.palette = palette.ToNativeArray();

      // Create Entities
      var objectInstanceArchetype = entityManager.CreateArchetype(typeof(ObjectInstance));
      var objectInstanceEntities = entityManager.CreateEntity(objectInstanceArchetype, ObjectConstants.NUM_OBJECTS, Allocator.Persistent);
      for (int i = 0; i < ObjectConstants.NUM_OBJECTS; ++i) {
        var instanceData = objectInstances[i];
        var instanceClass = instanceData.Class;
        
        entityManager.AddComponentData(objectInstanceEntities[i], objectInstances[i]);

        if (!instanceData.Active || instanceData.SpecIndex == 0) continue;

        _ = instanceClass switch {
          ObjectClass.Weapon => entityManager.AddComponentData(objectInstanceEntities[i], weaponInstances[instanceData.SpecIndex]),
          ObjectClass.Ammunition => entityManager.AddComponentData(objectInstanceEntities[i], ammunitionInstances[instanceData.SpecIndex]),
          ObjectClass.Projectile => entityManager.AddComponentData(objectInstanceEntities[i], projectileInstances[instanceData.SpecIndex]),
          ObjectClass.Explosive => entityManager.AddComponentData(objectInstanceEntities[i], explosiveInstances[instanceData.SpecIndex]),
          ObjectClass.DermalPatch => entityManager.AddComponentData(objectInstanceEntities[i], dermalInstances[instanceData.SpecIndex]),
          ObjectClass.Hardware => entityManager.AddComponentData(objectInstanceEntities[i], hardwareInstances[instanceData.SpecIndex]),
          ObjectClass.SoftwareAndLog => entityManager.AddComponentData(objectInstanceEntities[i], softwareLogInstances[instanceData.SpecIndex]),
          ObjectClass.Decoration => entityManager.AddComponentData(objectInstanceEntities[i], decorationInstances[instanceData.SpecIndex]),
          ObjectClass.Item => entityManager.AddComponentData(objectInstanceEntities[i], itemInstances[instanceData.SpecIndex]),
          ObjectClass.Interface => entityManager.AddComponentData(objectInstanceEntities[i], interfaceInstances[instanceData.SpecIndex]),
          ObjectClass.DoorAndGrating => entityManager.AddComponentData(objectInstanceEntities[i], doorGratingInstances[instanceData.SpecIndex]),
          ObjectClass.Animated => entityManager.AddComponentData(objectInstanceEntities[i], animatedInstances[instanceData.SpecIndex]),
          ObjectClass.Trigger => entityManager.AddComponentData(objectInstanceEntities[i], triggerInstances[instanceData.SpecIndex]),
          ObjectClass.Container => entityManager.AddComponentData(objectInstanceEntities[i], containerInstances[instanceData.SpecIndex]),
          ObjectClass.Enemy => entityManager.AddComponentData(objectInstanceEntities[i], enemyInstances[instanceData.SpecIndex]),
          _ => false,
        };
      }

      var paletteEffectArchetype = entityManager.CreateArchetype(typeof(PaletteEffect));
      using (var paletteEffects = entityManager.CreateEntity(paletteEffectArchetype, 6, Allocator.Temp)) {
        entityManager.AddComponentData<PaletteEffect>(paletteEffects[0], new PaletteEffect { First = 0x03, Last = 0x07, FrameTime = 68, TimeRemaining = 0 });
        entityManager.AddComponentData<PaletteEffect>(paletteEffects[1], new PaletteEffect { First = 0x0B, Last = 0x0F, FrameTime = 40, TimeRemaining = 0 });
        entityManager.AddComponentData<PaletteEffect>(paletteEffects[2], new PaletteEffect { First = 0x10, Last = 0x14, FrameTime = 20, TimeRemaining = 0 });
        entityManager.AddComponentData<PaletteEffect>(paletteEffects[3], new PaletteEffect { First = 0x15, Last = 0x17, FrameTime = 108, TimeRemaining = 0 });
        entityManager.AddComponentData<PaletteEffect>(paletteEffects[4], new PaletteEffect { First = 0x18, Last = 0x1A, FrameTime = 84, TimeRemaining = 0 });
        entityManager.AddComponentData<PaletteEffect>(paletteEffects[5], new PaletteEffect { First = 0x1B, Last = 0x1F, FrameTime = 64, TimeRemaining = 0 });
      }

      for (int i = 0; i < textureAnimation.Length; ++i)
        entityManager.AddComponentData(textureAnimationEntities[i], textureAnimation[i]);
      
      var levelInfoArchetype = entityManager.CreateArchetype(typeof(LevelInfo), typeof(Level));
      var levelInfoEntity = entityManager.CreateEntity(levelInfoArchetype);
      mapSystem.SetSingleton(levelInfo);

      var map = new Level {
        Id = mapId,
        ObjectInstances = BuildBlob(objectInstanceEntities)
      };

      var mapElementArchetype = entityManager.CreateArchetype(typeof(TileLocation), typeof(LocalToWorld), typeof(MapElement));
      using (var mapElementEntities = entityManager.CreateEntity(mapElementArchetype, levelInfo.Width * levelInfo.Height, Allocator.Temp)) {
        for (int x = 0; x < levelInfo.Width; ++x) {
          for (int y = 0; y < levelInfo.Height; ++y) {
            var rowIndex = y * levelInfo.Width;

            var entity = mapElementEntities[rowIndex + x];
            entityManager.AddComponentData(entity, new TileLocation { X = (byte)x, Y = (byte)y });
            entityManager.AddComponentData(entity, default(LocalToWorld));
            entityManager.AddComponentData(entity, tileMap[x, y]);
            entityManager.AddComponentData(entity, default(ViewPartRebuildTag));
            entityManager.AddComponentData(entity, default(LightmapRebuildTag));
          }
        }

        map.TileMap = BuildBlob(mapElementEntities);
      }

      mapSystem.SetSingleton(map);

      //DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, defaultSystems);
      //ScriptBehaviourUpdateOrder.AddWorldToCurrentPlayerLoop(world);

      return world;
    }

    private static unsafe BlobAssetReference<BlobArray<Entity>> BuildBlob (in NativeArray<Entity> entities) {
      using (BlobBuilder blobBuilder = new BlobBuilder(Allocator.Temp)) {
        ref var tileArrayAsset = ref blobBuilder.ConstructRoot<BlobArray<Entity>>();
        var tileArray = blobBuilder.Allocate(ref tileArrayAsset, entities.Length);
        UnsafeUtility.MemCpy(tileArray.GetUnsafePtr(), entities.GetUnsafeReadOnlyPtr(), entities.Length * UnsafeUtility.SizeOf<Entity>());
        return blobBuilder.CreateBlobAssetReference<BlobArray<Entity>>(Allocator.Persistent);
      }
    }

    private static MapElement[,] ReadMapElements(byte[] rawData, in LevelInfo levelInfo) {
      using (MemoryStream ms = new MemoryStream(rawData)) {
          BinaryReader msbr = new BinaryReader(ms);

          MapElement[,] mapElements = new MapElement[levelInfo.Width, levelInfo.Height];

          for (uint y = 0; y < levelInfo.Height; ++y)
              for (uint x = 0; x < levelInfo.Width; ++x)
                  mapElements[x, y] = msbr.Read<MapElement>();

          return mapElements;
      }
    }

    private static Texture2D CreateColorLookupTable(Palette palette, ShadeTableData shadeTable) {
      Texture2D clut = new Texture2D(256, 16, TextureFormat.RGBA32, false, false);
      clut.filterMode = FilterMode.Point;
      clut.wrapMode = TextureWrapMode.Clamp;

      var textureData = clut.GetRawTextureData<Color32>();

      for (int i = 0; i < textureData.Length; ++i)
        textureData[i] = palette[shadeTable[i]];

      clut.Apply(false, false);
      return clut;
    }

    private static Texture2D CreateLightmap(in LevelInfo levelInfo) {
      Texture2D lightmap;
      if (SystemInfo.SupportsTextureFormat(TextureFormat.RG16)) {
        lightmap = new Texture2D(levelInfo.Width, levelInfo.Height, TextureFormat.RG16, false, true);
      } else if (SystemInfo.SupportsTextureFormat(TextureFormat.RGBA32)) {
        lightmap = new Texture2D(levelInfo.Width, levelInfo.Height, TextureFormat.RGBA32, false, true);
      } else {
        throw new Exception("No supported TextureFormat found.");
      }

      lightmap.name = @"Lightmap";
      return lightmap;
    }

    private static async Task<BitmapSet> CreateMipmapTexture(ushort textureIndex) {
      var tex128x128 = Addressables.LoadAssetAsync<BitmapSet>($"{0x03E8 + textureIndex}");
      var tex64x64 = Addressables.LoadAssetAsync<BitmapSet>($"{0x02C3 + textureIndex}");
      var tex32x32 = Addressables.LoadAssetAsync<BitmapSet>($"{0x004D}:{textureIndex}");
      var tex16x16 = Addressables.LoadAssetAsync<BitmapSet>($"{0x004C}:{textureIndex}");

      await Task.WhenAll(tex128x128.Task, tex64x64.Task, tex32x32.Task, tex16x16.Task);

      Texture2D complete = new Texture2D(128, 128, tex128x128.Result.Texture.format, 4, true);
      complete.filterMode = tex128x128.Result.Texture.filterMode;
      complete.wrapMode = tex128x128.Result.Texture.wrapMode;

      if (SystemInfo.copyTextureSupport.HasFlag(UnityEngine.Rendering.CopyTextureSupport.Basic)) {
        Graphics.CopyTexture(tex128x128.Result.Texture, 0, 0, complete, 0, 0);
        Graphics.CopyTexture(tex64x64.Result.Texture, 0, 0, complete, 0, 1);
        Graphics.CopyTexture(tex32x32.Result.Texture, 0, 0, complete, 0, 2);
        Graphics.CopyTexture(tex16x16.Result.Texture, 0, 0, complete, 0, 3);
      } else {
        complete.SetPixelData(tex128x128.Result.Texture.GetPixelData<byte>(0), 0);
        complete.SetPixelData(tex64x64.Result.Texture.GetPixelData<byte>(0), 1);
        complete.SetPixelData(tex32x32.Result.Texture.GetPixelData<byte>(0), 2);
        complete.SetPixelData(tex16x16.Result.Texture.GetPixelData<byte>(0), 3);
      }
      complete.Apply(false, true);

      var result = new BitmapSet {
        Texture = complete,
        Transparent = tex128x128.Result.Transparent,
        AnchorPoint = tex128x128.Result.AnchorPoint,
        AnchorRect = tex128x128.Result.AnchorRect,
        Palette = tex128x128.Result.Palette
      };

      Addressables.Release(tex128x128);
      Addressables.Release(tex64x64);
      Addressables.Release(tex32x32);
      Addressables.Release(tex16x16);

      return result;
    }
  }

  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct ShadeTableData {
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256 * 16)]
    private readonly byte[] paletteIndex;

    public byte this[int index] {
      get => paletteIndex[index];
      set => paletteIndex[index] = value;
    }
  }

  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct TexturePropertiesData {
    public const int Version = 9;

    private readonly int version;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 396)] // 400 / Marshal.SizeOf<TextureProperties>()
    private readonly TextureProperties[] textureProperties;

    public TextureProperties this[int index] {
      get => textureProperties[index];
      set => textureProperties[index] = value;
    }
  }

  public struct Level : IComponentData {
      public byte Id;
      public BlobAssetReference<BlobArray<Entity>> TileMap;
      public BlobAssetReference<BlobArray<Entity>> ObjectInstances;
  }
}
