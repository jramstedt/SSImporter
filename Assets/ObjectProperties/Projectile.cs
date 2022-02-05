using System;
using System.Runtime.InteropServices;
using Unity.Entities;

namespace SS.ObjectProperties {
[Serializable]
  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct Projectile{
    [Flags]
    public enum FeatureFlag : byte {
        Light = 0x01,
        BounceWorld = 0x02,
        BounceEnemy = 0x04,
        Cyberspace = 0x08
    }

    public FeatureFlag Flags;

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Tracer{
      [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
      public short[] XCoords;

      [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
      public short[] YCoords;

      [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
      public byte[] ZCoords;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Slow{
      [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
      public byte[] Colors;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Camera{
      private byte Zero;
    }
  }
}
