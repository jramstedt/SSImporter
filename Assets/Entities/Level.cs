using System;
using System.Runtime.InteropServices;
using SS.Resources;
using SS.System;
using Unity.Entities;
using Unity.Mathematics;
using static Unity.Mathematics.math;

namespace SS {
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

    public int FloorCornerHeight (int cornerIndex) => !IsCeilingOnly && slopeAffectsCorner[(byte)TileType][cornerIndex] ? FloorHeight + SlopeSteepnessFactor : FloorHeight;
    public int CeilingCornerHeight (int cornerIndex) => !IsFloorOnly && slopeAffectsCorner[(byte)TileType][cornerIndex] == IsCeilingMirrored ? CeilingHeight - SlopeSteepnessFactor : CeilingHeight;

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

  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct Path {
    public const byte NUM_PATH_STEPS = 64;

    public LGPoint Source;
    public LGPoint Destination;
    public byte DestinationZ;
    public byte StartZ;
    public byte TotalSteps;
    public byte CurrentSteps;
    
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = NUM_PATH_STEPS/4)]
    public byte[] Moves;
  }

  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct AnimationLoop {
    public enum AnimationFlags : byte {
        Repeat = 0x01,
        Reverse,
        Cycle
    }

    public enum AnimationCallbackType : ushort {
      Remove = 0x01,
      Repeat,
      Cycle
    }

    public enum Callback : uint {
      DiegoTeleport = 0x01,
      DestroyScreen,
      Unshodanize,
      Unmulti,
      Multi,
      Animate
    }

    public ushort ObjectIndex;
    public AnimationFlags Flags;
    public AnimationCallbackType CallbackType;
    public Callback CallbackIndex;
    public uint UserDataPointer;
    public ushort Speed;
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
