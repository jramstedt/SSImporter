using System;
using System.Runtime.InteropServices;

namespace SS.ObjectProperties {
  [Serializable]
  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct Animating {
    public const int NUM_OBJECT_ANIMATING = 9;
    public const int NUM_TRANSITORY_ANIMATING = 11;
    public const int NUM_EXPLOSION_ANIMATING = 14;

    public const int NUM_ANIMATING = NUM_OBJECT_ANIMATING + NUM_TRANSITORY_ANIMATING + NUM_EXPLOSION_ANIMATING;

    public const int DEFAULT_ANIMATION_SPEED = 32;
    
    public byte FrameTime;
    public EffectFlags Flags;

    public bool HasEffectLight => (Flags & EffectFlags.Light) == EffectFlags.Light;
    
    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Object {
      private byte Dummy;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Transitory {
      private byte Dummy;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Explosion {
      public byte FrameExplode;
    }

    [Flags]
    public enum EffectFlags : byte {
      Light = 0x01
    }
  }
}