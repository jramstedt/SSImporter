using System;
using System.Runtime.InteropServices;
using Unity.Entities;

namespace SS.ObjectProperties {
  [Serializable]
  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct Projectile {
    public const int NUM_TRACER_PHYSICS = 6;
    public const int NUM_SLOW_PHYSICS = 16;
    public const int NUM_CAMERA_PHYSICS = 2;

    public const int NUM_PHYSICS = NUM_TRACER_PHYSICS + NUM_SLOW_PHYSICS + NUM_CAMERA_PHYSICS;

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
    public unsafe struct Tracer {
      public fixed short XCoords[4];
      public fixed short YCoords[4];
      public fixed byte ZCoords[4];
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct Slow {
      public fixed byte Colors[6];
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Camera {
      private byte Zero;
    }
  }
}
