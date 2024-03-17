using System;
using System.Runtime.InteropServices;
using Unity.Entities;

namespace SS.Resources {
  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct ObjectInstance : IComponentData {
    [MarshalAs(UnmanagedType.U1)] public bool Active;
    public ObjectClass Class;
    public byte SubClass;
    /// <summary>Index to object specific data.</summary>
    public ushort SpecIndex;
    public ushort CrossReferenceTableIndex;
    public ushort Next;
    public ushort Prev;
    public Location Location;
    public Info Info;

    public readonly ushort HeadUsed => CrossReferenceTableIndex;
    public readonly ushort HeadFree => Next;
    public readonly int Triple => ((byte)Class) << 16 | SubClass << 8 | Info.Type;

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Weapon : IComponentData {
      public Link Link;

      public byte AmmoType;
      public byte AmmoCount;

      public readonly byte Charge => AmmoType;
      public readonly byte Temperature => AmmoCount;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Ammunition : IComponentData {
      public Link Link;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Projectile : IComponentData {
      public Link Link;

      public ushort OwnerObjectIndex;
      public Triple Bullet;
      public int duration;
      public Location p1;
      public Location p2;
      public Location p3;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Explosive : IComponentData {
      public Link Link;

      public byte UniqueId;
      public byte WallsHit;
      public ExplosiveFlags Flags;
      public ushort Timestamp;

      [Flags]
      public enum ExplosiveFlags : ushort {
        Active = 0x01,
        Dud = 0x02,
        MineStill = 0x04
      }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct DermalPatch : IComponentData {
      public Link Link;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Hardware : IComponentData {
      public Link Link;

      public byte Version;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SoftwareAndLog : IComponentData {
      public Link Link;

      public byte Version;
      public ushort Data;

      public readonly int SoftwareSecurity => Data >> 12;
      public readonly int SoftwareContents => Data & 0x0FFF;

      public readonly bool IsEmail => (Data >> 8) == 0;
      public readonly int EmailIndex => Data & 0xFF;

      public readonly bool IsLog => (Data >> 8) == 1;
      public readonly int LogIndex => (Data & 0xFF) / 16; // 16 == LOGS_PER_LEVEL

      public readonly bool IsData => (Data >> 8) == 2;
      public readonly int DataIndex => Data & 0xFF;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Decoration : IComponentData {
      public Link Link;

      public ushort Cosmetic;
      public uint Data1;
      public uint Data2;

      public readonly uint SoftwareVersion => Cosmetic;
      public readonly uint SoftwareSubclass => Data1;

      #region Words
      public readonly byte WordStyle => (byte)(Data1 & 0x0F);
      public readonly byte WordScale => (byte)((Data1 & 0xF0) >> 4);
      public readonly byte WordColor => (byte)((Data1 & 0xFF0000) >> 16);
      public readonly ushort WordIndex => Cosmetic;
      #endregion

      #region Mesh data
      public readonly byte SizeX => (byte)(Data1 & 0x0F);
      public readonly byte SizeY => (byte)((Data1 & 0xF0) >> 4);
      public readonly byte SizeZ => (byte)((Data1 & 0xFF00) >> 8);
      public readonly byte SideTexture => (byte)((Data1 & 0xFF0000) >> 16);
      public readonly byte TopBottomTexture => (byte)((Data1 & 0xFF000000) >> 24);
      public readonly byte Color => (byte)(Data2 & 0xFF);
      #endregion
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Item : IComponentData {
      public Link Link;

      public ushort Cosmetic;
      public uint Data1;
      public uint Data2;

      #region Mesh data
      public readonly byte SizeX => (byte)(Data1 & 0x0F);
      public readonly byte SizeY => (byte)((Data1 & 0xF0) >> 4);
      public readonly byte SizeZ => (byte)((Data1 & 0xFF00) >> 8);
      public readonly byte SideTexture => (byte)((Data1 & 0xFF0000) >> 16);
      public readonly byte TopBottomTexture => (byte)((Data1 & 0xFF000000) >> 24);
      public readonly byte Color => (byte)(Data2 & 0xFF);
      #endregion
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Interface : IComponentData {
      public Link Link;

      public ActionType ActionType; // ?? trap_type
      public byte DestroyCount;
      public uint Comparator; // ?? comparator
      public uint ActionParam1;
      public uint ActionParam2;
      public uint ActionParam3;
      public uint ActionParam4;
      public ushort AccessLevel;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct DoorAndGrating : IComponentData {
      public Link Link;

      public ushort Lock;
      public byte LockMessage;
      public byte Color;
      public byte AccessLevel;
      /// <summary>0xFF never close</summary>
      public byte AutocloseTime;
      public ushort OtherHalf;

      public const int DOOR_OPEN_FRAME = 3;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Animated : IComponentData {
      public Link Link;

      public byte StartFrame;
      public byte EndFrame;
      public ushort Owner;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Trigger : IComponentData {
      public Link Link;

      public ActionType ActionType;
      public byte DestroyCount;
      public uint Comparator;
      public uint ActionParam1;
      public uint ActionParam2;
      public uint ActionParam3;
      public uint ActionParam4;

      public readonly float RepulsorBottom => ActionParam2 / 65536f;
      public readonly float RepulsorTop => ActionParam3 / 65536f;
      public readonly Direction RepulsorDirection => (Direction)((ActionParam4 + 1) & 0x7);
      public readonly bool RepulsorIsFast => (ActionParam4 & 0x8) != 0;

      public enum Direction : byte {
        Null = 0,
        Up,
        Down,
        North,
        South,
        East,
        West
      }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Container : IComponentData {
      public Link Link;

      public Triple Contents1;
      public Triple Contents2;
      public byte SizeX;
      public byte SizeY;
      public byte SizeZ;

      public uint Data;

      public readonly byte SideTexture => (byte)(Data & 0xFF);
      public readonly byte TopBottomTexture => (byte)((Data & 0xFF00) >> 8);
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Enemy : IComponentData {
      public Link Link;

      /// <summary>16.16</summary>
      public int DestinationHeading;
      /// <summary>16.16</summary>
      public int DestinationSpeed;
      /// <summary>16.16</summary>
      public int Urgency;
      public short WaitFrames;
      public ushort Flags;
      public uint AttackCount;
      public byte AiMode;
      public byte Mood;
      public byte Orders;
      public byte ViewPosture;
      public byte X;
      public byte Y;
      public byte DestinationX;
      public byte DestinationY;
      public byte PathfindingX;
      public byte PathfindingY;
      public byte PahtfindingId;
      public byte PathTries;
      public ushort Loot1;
      public ushort Loot2;
      public int Sidestep;

      public readonly PostureType Posture => (PostureType)(ViewPosture & 0xF); // TODO Setter
      public readonly int View => ViewPosture >> 8; // TODO Setter

      public enum PostureType : byte {
        Standing,
        Moving,
        Attacking,
        AttackRest,
        Knockback,
        Death,
        Disrupt,
        Attacking2
      }
    }
  }

  [Flags]
  public enum InstanceFlags : byte {
    Nothing = 0x00,
    Hud = 0x01,
    BlockRendering = 0x02,
    Unlit = 0x04,
    Indestructible = 0x08,
    Useful = 0x10,
    HasPlayerInteracted = 0x20,
    ClassSpecific2 = 0x40,
    ClassSpecific = 0x80,

    // Containers & Corpses
    NoLootYet = ClassSpecific2,

    // Enemies
    Loner = ClassSpecific,
    WantCloser = ClassSpecific2,
    NoMove = Hud,
    NoDoor = BlockRendering,

    // Doors
    AutoClose = ClassSpecific,
    AutoClose2 = ClassSpecific2,

    // Decoration & Items
    DataIsObjIdsToUse = Hud
  }

  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct Location {
    /// <summary>8.8</summary>
    public ushort X;
    /// <summary>8.8</summary>
    public ushort Y;
    public byte Z;
    public byte Pitch; // FIXME Heading, Y axis
    public byte Yaw; // FIXME Pitch, X axis
    public byte Roll; // FIXME Bank, Z axis

    public readonly int TileX => X >> 8;
    public readonly int TileY => Y >> 8;
    public readonly int FineX => X & 0xFF;
    public readonly int FineY => Y & 0xFF;
  }

  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct Info {
    public byte PhysicsHandle;
    public byte Type;
    public ushort Hitpoints;
    public byte MakerDetails;
    public sbyte CurrentFrame;
    public byte TimeRemaining;
    public InstanceFlags Flags;
  }

  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct Link {
    public ushort ObjectIndex;
    public ushort Next;
    public ushort Prev;

    public readonly ushort HeadUsed => ObjectIndex;
    public readonly ushort HeadFree => Next;
  }

  public enum ActionType : byte {
    NoOp = 0x00,
    TeleportPlayer = 0x01,
    Damage = 0x02,
    Create = 0x03,
    SetQuestVariable = 0x04,
    Cutscene = 0x05,
    Propagate = 0x06,
    Lighting = 0x07,
    SoundEffect = 0x08,
    ChangeTileHeight = 0x09,
    ChangeTerrain = 0x0A,
    Scheduler = 0x0B,
    PropagateAlternating = 0x0C,
    Destroy = 0x0D,
    PlotClock = 0x0E,
    EmailPlayer = 0x0F,
    ChangeContamination = 0x10,
    ChangeClassData = 0x11,
    ChangeAnimation = 0x12,
    Hacks = 0x13, // Check Hacks enum 
    Texture = 0x14,
    AI = 0x15,
    Message = 0x16,
    Spawn = 0x17,
    ChangeType = 0x18
  }

  public enum Hacks : uint {
    NoOp = 0x00,
    RepulsorToggle = 0x01,
    ReactorDigit,
    ReactorKeypad,
    FixtureFrame,
    Door,
    GameOver,
    TurnObject,
    Armageddon,
    ShodanConquer,
    Comparator,
    PlotWare,
    AreaSpew,
    Diego,
    Panel,
    EarthDestroyed,
    MultiTransmog
  }
}
