using System;
using System.Runtime.InteropServices;
using SS;
using Unity.Entities;   

namespace SS {
  [Serializable]
  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct ObjectInstance : IComponentData {
    [MarshalAs(UnmanagedType.U1)] public bool Active;
    public ObjectClass Class;
    public byte SubClass;
    public ushort Type;
    public ushort CrossReferenceTableIndex;
    public ushort Next;
    public ushort Prev;
    public Location Location;
    public Info Info;

    public ushort HeadUsed => CrossReferenceTableIndex;
    public ushort HeadFree => Next;

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Weapon : IComponentData {
      public Link Link;

      public byte AmmoType;
      public byte AmmoCount;

      public byte Charge => AmmoType;
      public byte Temperature => AmmoCount;
    }
    
    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Ammunition : IComponentData {
        public Link Link;
    }

    [Serializable]
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

    [Serializable]
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

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct DermalPatch : IComponentData {
      public Link Link;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Hardware : IComponentData {
      public Link Link;

      public byte Version;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SoftwareAndLog : IComponentData {
      public Link Link;

      public byte Version;
      public ushort Data;

      public int SoftwareSecurity => Data >> 12;
      public int SoftwareContents => Data & 0x0FFF;

      public bool IsEmail => (Data >> 8) == 0;
      public int EmailIndex => Data & 0xFF;

      public bool IsLog => (Data >> 8) == 1;
      public int LogIndex => (Data & 0xFF) / 16; // 16 == LOGS_PER_LEVEL

      public bool IsData => (Data >> 8) == 2;
      public int DataIndex => Data & 0xFF;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Decoration : IComponentData {
      public Link Link;

      public ushort Cosmetic;
      public uint Data1;
      public uint Data2;

      public uint SoftwareVersion => Cosmetic;
      public uint SoftwareSubclass => Data1;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Item : IComponentData {
      public Link Link;

      public ushort Cosmetic;
      public uint Data1;
      public uint Data2;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Interface : IComponentData {
      public Link Link;

      public ActionType ActionType; // ?? trap_type
      public byte DestroyCount; // ?? destroy_count
      public uint Comparator; // ?? comparator
      public uint ActionParam1;
      public uint ActionParam2;
      public uint ActionParam3;
      public uint ActionParam4;
      public ushort AccessLevel;
    }

    [Serializable]
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
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Animated : IComponentData {
      public Link Link;

      public byte StartFrame;
      public byte EndFrame;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Trigger : IComponentData {
      public Link Link;

      public ActionType ActionType; // ?? trap_type
      public byte DestroyCount; // ?? destroy_count
      public uint Comparator; // ?? comparator
      public uint ActionParam1;
      public uint ActionParam2;
      public uint ActionParam3;
      public uint ActionParam4;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Container : IComponentData {
      public Link Link;

      public Triple Contents1;
      public Triple Contents2;
      public byte SizeX;
      public byte SizeY;
      public byte SizeZ;

      public uint Data;

      public uint TopBottomTexture => (Data & 0xFF00) >> 8;
      public uint SideTexture => Data & 0xFF;
    }

    [Serializable]
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
      public byte Posture;
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

      // Doors
      AutoClose = ClassSpecific,
      AutoClose2 = ClassSpecific2
  }

  [Serializable]
  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct Location {
    /// <summary>8.8</summary>
    public ushort X;
    /// <summary>8.8</summary>
    public ushort Y;
    public byte Z;
    public byte Pitch;
    public byte Yaw;
    public byte Roll;


    public int TileX => X >> 8;
    public int TileY => Y >> 8;
    public int FineX => X & 0xFF;
    public int FineY => Y & 0xFF;
  }

  [Serializable]
  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct Info {
    public byte AIIndex;
    public byte Type;
    public ushort Hitpoints;
    public byte MakerDetails;
    public byte CurrentFrame;
    public byte TimeRemaining;
    public InstanceFlags Flags;
  }

  [Serializable]
  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct Link {
      public ushort ObjectIndex;
      public ushort Next;
      public ushort Prev;

      public ushort HeadUsed => ObjectIndex;
      public ushort HeadFree => Next;
  }
  
  public enum ActionType : byte {
    NoOp = 0x00,
    TeleportPlayer = 0x01,
    ChangePlayerVitality = 0x02,
    CloneOrMove = 0x03,
    SetVariable = 0x04,
    Cutscene = 0x05,
    Propagate = 0x06,
    Lighting = 0x07,
    Effect = 0x08,
    MovePlatform = 0x09,
    ChangeTerrain = 0x0A,
    PropagateRepeat = 0x0B,
    PropagateCycle = 0x0C,
    Destroy = 0x0D,
    PlotClock = 0x0E,
    EmailPlayer = 0x0F,
    ChangeContamination = 0x10,
    ChangeClassData = 0x11,
    ChangeFrameLoop = 0x12,
    ChangeInstance = 0x13,
    Texture = 0x14,
    Awaken = 0x15,
    Message = 0x16,
    Spawn = 0x17,
    ChangeType = 0x18
  }
}
