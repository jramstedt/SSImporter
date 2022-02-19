using UnityEngine;
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
      var textureMap = saveData.GetResourceData<TextureMap>((ushort)(0x0007 + resourceId));

      /*
      var instanceDatas = new object[][] {
          saveData.GetResourceDataArray<ObjectInstance.Weapon>((ushort)(0x000A + resourceId)),
          saveData.GetResourceDataArray<ObjectInstance.Ammunition>((ushort)(0x000B + resourceId)),
          saveData.GetResourceDataArray<ObjectInstance.Projectile>((ushort)(0x000C + resourceId)),
          saveData.GetResourceDataArray<ObjectInstance.Explosive>((ushort)(0x000D + resourceId)),
          saveData.GetResourceDataArray<ObjectInstance.DermalPatch>((ushort)(0x000E + resourceId)),
          saveData.GetResourceDataArray<ObjectInstance.Hardware>((ushort)(0x000F + resourceId)),
          saveData.GetResourceDataArray<ObjectInstance.SoftwareAndLog>((ushort)(0x0010 + resourceId)),
          saveData.GetResourceDataArray<ObjectInstance.Decoration>((ushort)(0x0011 + resourceId)),
          saveData.GetResourceDataArray<ObjectInstance.Item>((ushort)(0x0012 + resourceId)),
          saveData.GetResourceDataArray<ObjectInstance.Interface>((ushort)(0x0013 + resourceId)),
          saveData.GetResourceDataArray<ObjectInstance.DoorAndGrating>((ushort)(0x0014 + resourceId)),
          saveData.GetResourceDataArray<ObjectInstance.Animated>((ushort)(0x0015 + resourceId)),
          saveData.GetResourceDataArray<ObjectInstance.Trigger>((ushort)(0x0016 + resourceId)),
          saveData.GetResourceDataArray<ObjectInstance.Container>((ushort)(0x0017 + resourceId)),
          saveData.GetResourceDataArray<ObjectInstance.Enemy>((ushort)(0x0018 + resourceId))
      };

      var classDataTemplates = new object[][] {
          saveData.GetResourceDataArray<ObjectInstance.Weapon>((ushort)(0x0019 + resourceId)),
          saveData.GetResourceDataArray<ObjectInstance.Ammunition>((ushort)(0x001A + resourceId)),
          saveData.GetResourceDataArray<ObjectInstance.Projectile>((ushort)(0x001B + resourceId)),
          saveData.GetResourceDataArray<ObjectInstance.Explosive>((ushort)(0x0001C + resourceId)),
          saveData.GetResourceDataArray<ObjectInstance.DermalPatch>((ushort)(0x001D + resourceId)),
          saveData.GetResourceDataArray<ObjectInstance.Hardware>((ushort)(0x001E + resourceId)),
          saveData.GetResourceDataArray<ObjectInstance.SoftwareAndLog>((ushort)(0x001F + resourceId)),
          saveData.GetResourceDataArray<ObjectInstance.Decoration>((ushort)(0x0020 + resourceId)),
          saveData.GetResourceDataArray<ObjectInstance.Item>((ushort)(0x0021 + resourceId)),
          saveData.GetResourceDataArray<ObjectInstance.Interface>((ushort)(0x0022 + resourceId)),
          saveData.GetResourceDataArray<ObjectInstance.DoorAndGrating>((ushort)(0x0023 + resourceId)),
          saveData.GetResourceDataArray<ObjectInstance.Animated>((ushort)(0x0024 + resourceId)),
          saveData.GetResourceDataArray<ObjectInstance.Trigger>((ushort)(0x0025 + resourceId)),
          saveData.GetResourceDataArray<ObjectInstance.Container>((ushort)(0x0026 + resourceId)),
          saveData.GetResourceDataArray<ObjectInstance.Enemy>((ushort)(0x0027 + resourceId))
      };
    */
      var textureAnimation = saveData.GetResourceDataArray<TextureAnimationData>((ushort)(0x002A + resourceId));

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

      // Create Entities
      for (int i = 0; i < textureAnimation.Length; ++i) {
        var entity = textureAnimationEntities[i];
        entityManager.AddComponentData(entity, textureAnimation[i]);
      }
      
      var mapElementArchetype = entityManager.CreateArchetype(typeof(TileLocation), typeof(LocalToWorld), typeof(MapElement));
      var mapElementEntities = entityManager.CreateEntity(mapElementArchetype, levelInfo.Width * levelInfo.Height, Allocator.Temp);

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

      var levelInfoArchetype = entityManager.CreateArchetype(typeof(LevelInfo), typeof(Map));
      var levelInfoEntity = entityManager.CreateEntity(levelInfoArchetype);
      mapSystem.SetSingleton(levelInfo);
      BuildBlob(mapSystem, mapId, in levelInfo, ref mapElementEntities);

      //DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, defaultSystems);
      //ScriptBehaviourUpdateOrder.AddWorldToCurrentPlayerLoop(world);

      return world;
    }

    private static unsafe void BuildBlob (MapElementBuilderSystem mapSystem, byte mapId, in LevelInfo levelInfo, ref NativeArray<Entity> entities) {
      using (BlobBuilder blobBuilder = new BlobBuilder(Allocator.Temp)) {
        ref var tileArrayAsset = ref blobBuilder.ConstructRoot<BlobArray<Entity>>();
        var tileArray = blobBuilder.Allocate(ref tileArrayAsset, levelInfo.Width * levelInfo.Height);
        UnsafeUtility.MemCpy(tileArray.GetUnsafePtr(), entities.GetUnsafeReadOnlyPtr(), entities.Length * UnsafeUtility.SizeOf<Entity>());
        var assetReference = blobBuilder.CreateBlobAssetReference<BlobArray<Entity>>(Allocator.Persistent);

        mapSystem.SetSingleton(new Map { Id = mapId, TileMap = assetReference });
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

      clut.Apply(false, true);
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

  public struct Map : IComponentData {
      public byte Id;
      public BlobAssetReference<BlobArray<Entity>> TileMap;
  }

  public struct TileLocation : IComponentData {
    public byte X;
    public byte Y;
  }

  public struct ViewPart : IComponentData { }
  public struct ViewPartRebuildTag : IComponentData { }
  public struct LightmapRebuildTag : IComponentData { }

  /**
   * FullMap
   */
  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public unsafe struct LevelInfo : IComponentData {
    public enum LevelType : byte {
      Normal,
      Cyberspace
    }

    public int Width;
    public int Height;
    public int XShift; // def 6
    public int YShift; // def 6
    public int ZShift; // def 3
    private uint InternalPointer;
    public LevelType Type;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
    private fixed byte Unused[12]; // x_scale, y_scale, z_scale

    public SchedulerInfo SchedulerInfo;

    public bool IsCyberspace => Type == LevelType.Cyberspace;

    public int HeightDivisor => 1 << ZShift;

    public override string ToString() => $"Width = {Width}, Height = {Height}, XShift = {XShift}, YShift = {YShift}, ZShift = {ZShift}, Type = {Type}";
  }

  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct SchedulerInfo {
    public uint Size; // Must be 0x40 (64)
    public uint Count;
    public uint ElementSize; // Must be 0x08
    public byte Grow;
    private readonly uint InternalPointer;
    private readonly uint InternalPointer2;
  }

  public enum TileType : byte {
    Solid,
    Open,
    OpenDiagonalSE,
    OpenDiagonalSW,
    OpenDiagonalNW,
    OpenDiagonalNE,
    SlopeSN,
    SlopeWE,
    SlopeNS,
    SlopeEW,
    ValleySE_NW,
    ValleySW_NE,
    ValleyNW_SE,
    ValleyNE_SW,
    RidgeNW_SE,
    RidgeNE_SW,
    RidgeSE_NW,
    RidgeSW_NE
  };

  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct MapElement : IComponentData {
    [Flags]
    public enum FlagMask : uint { // ok
      // Flag 1
      Offset = 0x0000001F,
      Flip = 0x00000060, // FlipMask
      Family = 0x00000080,

      // Flag 2
      UseAdjacentWallTexture = 0x00000100,
      DeconstructedMusic = 0x00000200,
      SlopeControl = 0x00000C00, // SlopeControl
      Peril = 0x00001000,
      Music = 0x0000E000,

      // Flag 3
      ShadeFloor = 0x000F0000,
      Rend4 = 0x00F00000, // unused

      // Flag 4
      ShadeCeiling = 0x0F000000,
      Rend3 = 0x70000000, // unused

      TileVisited = 0x80000000,

      // Cyberspace
      CyberspacePullFloorStrong = 0x01000000,
      CyberspacePull = ShadeFloor,
      CyberspaceGOL = Flip,
    };

    public enum SlopeControl : uint {
      Match = 0x00000000,       // 0
      Mirror = 0x00000400,      // 1
      FloorOnly = 0x00000800,   // 2
      CeilingOnly = 0x00000C00  // 3
    };

    [Flags]
    public enum TextureInfoMask : ushort { // ok
      WallTexture = 0x003F,
      CeilingTexture = 0x07C0,
      FloorTexture = 0xF800,

      CyberspaceFloor = 0x00FF,
      CyberspaceCeiling = 0xFF00,
    }

    [Flags]
    public enum InfoMask : byte { // ok
      Height = 0x1F,
      Orientation = 0x60,
      Hazard = 0x80
    }

    [Flags]
    public enum FlipMask : byte { // ok
      Parity = 0x01,
      Alternate = 0x02
    }

    public enum Orientation : byte {
      North = 0x00,
      East = 0x20,
      South = 0x40,
      West = 0x60
    }

    public TileType TileType;
    public InfoMask FloorInfo;      // bit 0-4 Height from down to top, 5-6 orientation, 7 biohazard
    public InfoMask CeilingInfo;    // bit 0-4 Height from top to down, 5-6 orientation, 7 radiation hazard
    public byte SlopeSteepnessFactor;
    public ushort IndexFirstObject;
    public TextureInfoMask TextureInfo;
    public FlagMask Flags;
    private uint RuntimeTemps;

    public const int MAX_HEIGHT = 32;

    public int FloorHeight => (int)(FloorInfo & InfoMask.Height);
    public Orientation FloorOrientation => (Orientation)(FloorInfo & InfoMask.Orientation);
    public int FloorRotation => ((byte)FloorOrientation >> 5) & 0x03;
    public bool FloorHazard => (FloorInfo & InfoMask.Hazard) == InfoMask.Hazard;
    public byte FloorTexture => (byte)((ushort)(TextureInfo & TextureInfoMask.FloorTexture) >> 11);

    public int CeilingHeight => MAX_HEIGHT - (int)(CeilingInfo & InfoMask.Height);
    public Orientation CeilingOrientation => (Orientation)(CeilingInfo & InfoMask.Orientation);
    public int CeilingRotation => ((byte)CeilingOrientation >> 5) & 0x03;

    public bool CeilingHazard => (CeilingInfo & InfoMask.Hazard) == InfoMask.Hazard;
    public byte CeilingTexture => (byte)((ushort)(TextureInfo & TextureInfoMask.CeilingTexture) >> 6);

    public byte WallTexture => (byte)(TextureInfo & TextureInfoMask.WallTexture);

    // Flag 1
    public byte TextureOffset => (byte)(Flags & FlagMask.Offset);
    public bool TextureParity => ((FlipMask)((byte)(Flags & FlagMask.Flip) >> 5) & FlipMask.Parity) == FlipMask.Parity;
    public bool TextureAlternate => ((FlipMask)((byte)(Flags & FlagMask.Flip) >> 5) & FlipMask.Alternate) == FlipMask.Alternate;

    // Flag 2
    public bool UseAdjacentTexture => (Flags & FlagMask.UseAdjacentWallTexture) == FlagMask.UseAdjacentWallTexture;
    public bool IsCeilingMirrored => (SlopeControl)(Flags & FlagMask.SlopeControl) == SlopeControl.Mirror;
    public bool IsCeilingOnly => (SlopeControl)(Flags & FlagMask.SlopeControl) == SlopeControl.CeilingOnly;
    public bool IsFloorOnly => (SlopeControl)(Flags & FlagMask.SlopeControl) == SlopeControl.FloorOnly;

    // Flag 3
    public byte ShadeFloor => (byte)((uint)(Flags & FlagMask.ShadeFloor) >> 16);

    // Flag 4
    public byte ShadeCeiling => (byte)((uint)(Flags & FlagMask.ShadeCeiling) >> 24);

    public int FloorCornerHeight (int cornerIndex) => !IsCeilingOnly && MapUtils.slopeAffectsCorner[(byte)TileType, cornerIndex] ? FloorHeight + SlopeSteepnessFactor : FloorHeight;
    public int CeilingCornerHeight (int cornerIndex) => !IsFloorOnly && MapUtils.slopeAffectsCorner[(byte)TileType, cornerIndex] == IsCeilingMirrored ? CeilingHeight - SlopeSteepnessFactor : CeilingHeight;
  }


  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct TextureMap {
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 54)]
    public readonly ushort[] blockIndex;
  }

  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct TextureProperties {
    [Flags]
    public enum StartfieldControlMask : byte {
        Stars = 0x01,
        StarsOnEmpty = 0x02
    }

    public byte Family;
    public byte Texture;
    /// <summary>Unused</summary>
    public short Resilience;
    /// <summary>LOD Bias</summary>
    public short DistanceModifier;
    public byte FrictionClimb;
    public byte FrictionWalk;
    public byte StarfieldControl;
    public byte AnimationGroup;

    /// <summary>Offset from texture to start of the group.</summary>
    public byte GroupPosition;

    public ushort BaseTextureId (int textureId) => (ushort)(textureId - GroupPosition);
  }
}
