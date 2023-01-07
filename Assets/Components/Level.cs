using System;
using System.Runtime.InteropServices;
using Unity.Entities;
using Unity.Mathematics;
using static Unity.Mathematics.math;

namespace SS.Resources {
  public struct Level : IComponentData {
    public byte Id;
    public TextureMap TextureMap;
    public BlobAssetReference<BlobArray<Entity>> TileMap;
    public BlobAssetReference<BlobArray<Entity>> ObjectInstances; // TODO needs to be mutable
    public BlobAssetReference<BlobArray<Entity>> SurveillanceCameras;
  }

  public struct TileLocation : IComponentData {
    public byte X;
    public byte Y;
  }

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

    private fixed byte Unused[12]; // x_scale, y_scale, z_scale

    public SchedulerInfo SchedulerInfo;

    public bool IsCyberspace => Type == LevelType.Cyberspace;

    public int HeightDivisor => 1 << ZShift;
    public int HeightFactor => MapElement.MAX_HEIGHT >> ZShift;

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
    public enum Flag1Mask : byte {
      // Flag 1
      Offset = 0x1F,
      FlipParity = 0x20,
      FlipAlternate = 0x40,
      Family = 0x80,

      // Cyberspace
      CyberspaceGOL = FlipParity | FlipAlternate,
    }

    [Flags]
    public enum Flag2Mask : byte {
      // Flag 2
      UseAdjacentWallTexture = 0x01,
      DeconstructedMusic = 0x02,
      SlopeMirror = 0x04,
      SlopeFloorOnly = 0x08,
      SlopeCeilingOnly = 0x0C,
      Peril = 0x10,
      Music = 0xE0,
    }

    [Flags]
    public enum Flag3Mask : byte {
      // Flag 3
      ShadeFloor = 0x0F,
      Rend4 = 0xF0, // unused,

      // Cyberspace
      CyberspacePull = ShadeFloor,
    }

    [Flags]
    public enum Flag4Mask : byte {
      // Flag 4
      ShadeCeiling = 0x0F,
      Rend3 = 0x70, // unused

      TileVisited = 0x80,

      // Cyberspace
      CyberspacePullFloorStrong = 0x01,
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

    public Flag1Mask Flag1;
    public Flag2Mask Flag2;
    public Flag3Mask Flag3;
    public Flag4Mask Flag4;

    public byte SubClip;
    public byte ClearSolid;
    public byte FlickQclip;
    public byte Templight;

    public const int MAX_HEIGHT = 32;
    public const int PHYSICS_RADIUS_UNIT = 96;

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
    public byte TextureOffset => (byte)(Flag1 & Flag1Mask.Offset);
    public bool TextureParity => (Flag1 & Flag1Mask.FlipParity) == Flag1Mask.FlipParity;
    public bool TextureAlternate => (Flag1 & Flag1Mask.FlipAlternate) == Flag1Mask.FlipAlternate;

    // Flag 2
    public bool UseAdjacentTexture => (Flag2 & Flag2Mask.UseAdjacentWallTexture) == Flag2Mask.UseAdjacentWallTexture;
    public bool IsCeilingMirrored => (Flag2 & Flag2Mask.SlopeCeilingOnly) == Flag2Mask.SlopeMirror;
    public bool IsFloorOnly => (Flag2 & Flag2Mask.SlopeCeilingOnly) == Flag2Mask.SlopeFloorOnly;
    public bool IsCeilingOnly => (Flag2 & Flag2Mask.SlopeCeilingOnly) == Flag2Mask.SlopeCeilingOnly;

    // Flag 3
    public byte ShadeFloor => (byte)(Flag3 & Flag3Mask.ShadeFloor);
    public byte ShadeFloorModifier {
      get => (byte)(Templight & 0x0F);
      set => Templight = (byte)((Templight & 0xF0) | value);
    }

    // Flag 4
    public byte ShadeCeiling => (byte)(Flag4 & Flag4Mask.ShadeCeiling);
    public byte ShadeCeilingModifier {
      get => (byte)((Templight & 0xF0) >> 4);
      set => Templight = (byte)((Templight & 0x0F) | (value << 4));
    }

    public int FloorCornerHeight(int cornerIndex) => !IsCeilingOnly && slopeAffectsCorner[(byte)TileType][cornerIndex] ? FloorHeight + SlopeSteepnessFactor : FloorHeight;
    public int CeilingCornerHeight(int cornerIndex) => !IsFloorOnly && slopeAffectsCorner[(byte)TileType][cornerIndex] == IsCeilingMirrored ? CeilingHeight - SlopeSteepnessFactor : CeilingHeight;

    public static readonly bool4[] slopeAffectsCorner = new bool4[] {
      bool4( false, false, false, false ),
      bool4( false, false, false, false ),

      bool4( false, false, false, false ),
      bool4( false, false, false, false ),
      bool4( false, false, false, false ),
      bool4( false, false, false, false ),

      bool4( false,  true,  true, false ),
      bool4( false, false,  true,  true ),
      bool4(  true, false, false,  true ),
      bool4(  true,  true, false, false ),

      bool4(  true,  true,  true, false ),
      bool4( false,  true,  true,  true ),
      bool4(  true, false,  true,  true ),
      bool4(  true,  true, false,  true ),

      bool4( false, false, false,  true ),
      bool4(  true, false, false, false ),
      bool4( false,  true, false, false ),
      bool4( false, false,  true, false )
    };
  }


  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public unsafe struct TextureMap {
    public const byte NUM_LOADED_TEXTURES = 54;

    public fixed ushort blockIndex[NUM_LOADED_TEXTURES];
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

    public ushort BaseTextureId(int textureId) => (ushort)(textureId - GroupPosition);
  }

  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct Path {
    public const byte NUM_PATH_STEPS = 64;

    public LGPoint Source;
    public LGPoint Destination;
    public byte DestinationZ;
    public byte StartZ;
    public byte TotalSteps;
    public byte CurrentSteps;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = NUM_PATH_STEPS / 4)]
    public byte[] Moves;
  }

  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct HeightSemaphore {
    public byte X;
    public byte Y;
    private byte Data;
    public byte InUse;

    public bool IsFloor => (Data & 0x01) == 0x01; // TODO Setter
    public byte Key => (byte)(Data >> 1); // TODO Setter
  }
}