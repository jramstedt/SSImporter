using UnityEngine;
using System.Collections;
using UnityEngine.ResourceManagement;
using UnityEngine.SceneManagement;
using System.IO;
using System.Runtime.InteropServices;
using System;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using System.Collections.Generic;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.Util;
using UnityEngine.AddressableAssets;
using System.Threading.Tasks;
using Unity.Rendering;
using Unity.Entities;
using Unity.Collections;
using Unity.Transforms;
using SS.System;

namespace SS.Resources {
  public static class SaveLoader {
    private const ushort SaveGameResourceIdBase = 4000;
    private const ushort NumResourceIdsPerLevel = 100;

    private static ushort ResourceIdFromLevel (byte level) => (ushort)(SaveGameResourceIdBase + (level * NumResourceIdsPerLevel));

    public static async Task<World> LoadMap(byte mapId, string saveGame) {
      var loadOp = Addressables.ResourceManager.ProvideResource<ResourceFile>(new ResourceLocationBase(@"savegame", saveGame, typeof(ResourceFileProvider).FullName, typeof(ResourceFile)));
      var saveData = await loadOp.Task;

      if (loadOp.Status != AsyncOperationStatus.Succeeded)
        throw loadOp.OperationException;

      //foreach (var kvp in saveData.ResourceEntries)
      //  Debug.Log($"{kvp.Value.info.Id} {kvp.Value.info.Flags}");

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

      var map = new Map { Id = mapId };

      var defaultSystems = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default);

      var world = new World($"map{mapId:D}");
      var mapSystem = world.CreateSystem<MapElementBuilderSystem>();
      DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, defaultSystems);
      ScriptBehaviourUpdateOrder.AddWorldToCurrentPlayerLoop(world);

      var entityManager = world.EntityManager;

      var levelInfoArchetype = entityManager.CreateArchetype(typeof(LevelInfo), typeof(Map));
      var levelInfoEntity = entityManager.CreateEntity(levelInfoArchetype);
      mapSystem.SetSingleton(levelInfo);
      mapSystem.SetSingleton(map);
      
      var mapElemenArchetype = entityManager.CreateArchetype(typeof(TileLocation), typeof(MapElement), typeof(NeedsRebuildTag));
      var entityArray = entityManager.CreateEntity(mapElemenArchetype, levelInfo.Width * levelInfo.Height, Allocator.Temp);

      for (int x = 0; x < levelInfo.Width; ++x) {
        for (int y = 0; y < levelInfo.Height; ++y) {
          var rowIndex = y * levelInfo.Width;

          var entity = entityArray[rowIndex + x];
          entityManager.AddComponentData(entity, new TileLocation { X = (byte)x, Y = (byte)y });
          entityManager.AddComponentData(entity, default(LocalToWorld));
          entityManager.AddComponentData(entity, tileMap[x, y]);
          entityManager.AddComponentData(entity, default(NeedsRebuildTag));
/*
          var buffer = entityManager.AddBuffer<TileNeighbour>(entity);

          if (x > 0)
            buffer.Add(new TileNeighbour { Entity = entityArray[rowIndex + x - 1] });
          if (x < levelInfo.Width - 1)
            buffer.Add(new TileNeighbour { Entity = entityArray[rowIndex + x + 1] });
          if (y > 0)
            buffer.Add(new TileNeighbour { Entity = entityArray[rowIndex + x - levelInfo.Width] });
          if (y < levelInfo.Height - 1)
            buffer.Add(new TileNeighbour { Entity = entityArray[rowIndex + x + levelInfo.Width] });
*/
        }
      }

      return world;
    }

    public static MapElement[,] ReadMapElements(byte[] rawData, LevelInfo levelInfo) {
      using (MemoryStream ms = new MemoryStream(rawData)) {
          BinaryReader msbr = new BinaryReader(ms);

          MapElement[,] mapElements = new MapElement[levelInfo.Width, levelInfo.Height];

          for (uint y = 0; y < levelInfo.Height; ++y)
              for (uint x = 0; x < levelInfo.Width; ++x)
                  mapElements[x, y] = msbr.Read<MapElement>();

          return mapElements;
      }
    }
  }

  public struct Map : IComponentData {
      public byte Id;
  }

  public struct TileLocation : IComponentData {
    public byte X;
    public byte Y;
  }

  public struct Floor : IComponentData {

  }

/*
  [InternalBufferCapacity(4)]
  public unsafe struct TileNeighbour : IBufferElementData {
    public Entity Entity;
  }
*/
  public struct NeedsRebuildTag : IComponentData { }

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
      Mirrored = 0x00000C00, // MirrorControl
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

    public enum MirrorControl : uint {
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
    public ushort FloorTexture => (ushort)((ushort)(TextureInfo & TextureInfoMask.FloorTexture) >> 11);

    public int CeilingHeight => MAX_HEIGHT - (int)(CeilingInfo & InfoMask.Height);
    public Orientation CeilingOrientation => (Orientation)(CeilingInfo & InfoMask.Orientation);
    public int CeilingRotation => ((byte)CeilingOrientation >> 5) & 0x03;

    public bool CeilingHazard => (CeilingInfo & InfoMask.Hazard) == InfoMask.Hazard;
    public ushort CeilingTexture => (ushort)((ushort)(TextureInfo & TextureInfoMask.CeilingTexture) >> 6);

    public ushort WallTexture => (ushort)(TextureInfo & TextureInfoMask.WallTexture);

    // Flag 1
    public byte TextureOffset => (byte)(Flags & FlagMask.Offset);
    public bool TextureParity => ((FlipMask)((byte)(Flags & FlagMask.Flip) >> 5) & FlipMask.Parity) == FlipMask.Parity;
    public bool TextureAlternate => ((FlipMask)((byte)(Flags & FlagMask.Flip) >> 5) & FlipMask.Alternate) == FlipMask.Alternate;

    // Flag 2
    public bool UseAdjacentTexture => (Flags & FlagMask.UseAdjacentWallTexture) == FlagMask.UseAdjacentWallTexture;
    public bool IsCeilingMirrored => ((MirrorControl)Flags & MirrorControl.Mirror) == MirrorControl.Mirror;
    public bool IsCeilingOnly => ((MirrorControl)Flags & MirrorControl.CeilingOnly) == MirrorControl.CeilingOnly;
    public bool IsFloorOnly => ((MirrorControl)Flags & MirrorControl.FloorOnly) == MirrorControl.FloorOnly;

    // Flag 3
    public int ShadeFloor => (byte)(Flags & FlagMask.ShadeFloor) >> 16;

    // Flag 4
    public int ShadeCeiling => (byte)(Flags & FlagMask.ShadeCeiling) >> 24;

    public int FloorCornerHeight (int cornerIndex) => !IsCeilingOnly && MapUtils.slopeAffectsCorner[(byte)TileType, cornerIndex] ? FloorHeight + SlopeSteepnessFactor : FloorHeight;
    public int CeilingCornerHeight (int cornerIndex) => !IsFloorOnly && MapUtils.slopeAffectsCorner[(byte)TileType, cornerIndex] == IsCeilingMirrored ? CeilingHeight - SlopeSteepnessFactor : CeilingHeight;
  }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct TextureMap {
  [MarshalAs(UnmanagedType.ByValArray, SizeConst = 54)]
  public readonly ushort[] textureIndex;
}