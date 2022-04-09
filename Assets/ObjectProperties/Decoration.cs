using System;
using System.Runtime.InteropServices;
using Unity.Entities;

namespace SS.ObjectProperties {
  [Serializable]
  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct Decoration {
    public const int NUM_ELECTRONIC_BIGSTUFF = 9;
    public const int NUM_FURNISHING_BIGSTUFF = 10;
    public const int NUM_ONTHEWALL_BIGSTUFF = 11;
    public const int NUM_LIGHT_BIGSTUFF = 4;
    public const int NUM_LABGEAR_BIGSTUFF = 9;
    public const int NUM_TECHNO_BIGSTUFF = 8;
    public const int NUM_DECOR_BIGSTUFF = 16;
    public const int NUM_TERRAIN_BIGSTUFF = 10;

    public const int NUM_BIGSTUFF = NUM_ELECTRONIC_BIGSTUFF + NUM_FURNISHING_BIGSTUFF + NUM_ONTHEWALL_BIGSTUFF + NUM_LIGHT_BIGSTUFF + NUM_LABGEAR_BIGSTUFF + NUM_TECHNO_BIGSTUFF + NUM_DECOR_BIGSTUFF + NUM_TERRAIN_BIGSTUFF;

    private int Data;

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Electronic{
      private byte Zero;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Furniture{
      private byte Zero;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct OnTheWall{
      private byte Zero;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Light{
      private byte Zero;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct LabGear{
      private byte Zero;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Techno{
      private byte Zero;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Decor{
      private byte Zero;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Terrain{
      private byte Zero;
    }

    [Flags]
    public enum BaseFlags : byte {
      ObjectUse = 0x01, // denote data1 is 1-2 ObjIDs to be "used"
    }
  }
}
