using System;
using System.Runtime.InteropServices;
using Unity.Entities;

namespace SS.ObjectProperties {
  [Serializable]
  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct Item {
    public const int NUM_USELESS_SMALLSTUFF = 8;
    public const int NUM_BROKEN_SMALLSTUFF = 10;
    public const int NUM_CORPSELIKE_SMALLSTUFF = 15;
    public const int NUM_GEAR_SMALLSTUFF = 6;
    public const int NUM_CARDS_SMALLSTUFF = 12;
    public const int NUM_CYBER_SMALLSTUFF = 12;
    public const int NUM_ONTHEWALL_SMALLSTUFF = 9;
    public const int NUM_PLOT_SMALLSTUFF = 8;

    public const int NUM_SMALLSTUFF = NUM_USELESS_SMALLSTUFF + NUM_BROKEN_SMALLSTUFF + NUM_CORPSELIKE_SMALLSTUFF + NUM_GEAR_SMALLSTUFF + NUM_CARDS_SMALLSTUFF + NUM_CYBER_SMALLSTUFF + NUM_ONTHEWALL_SMALLSTUFF + NUM_PLOT_SMALLSTUFF;

    private short UsesFlags;

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Useless {
      private byte Zero;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Broken {
      private byte Zero;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Corpse {
      private byte Zero;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Gear {
      private byte Zero;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Cards {
      private byte Zero;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct Cyberspace {
      public SixOf<byte> Colors;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct OnTheWall {
      private byte Zero;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Plot {
      private ushort Target;
    }

    [Flags]
    public enum BaseFlags : byte {
      ObjectUse = 0x01, // denote data1 is 1-2 ObjIDs to be "used"
    }
  }
}
