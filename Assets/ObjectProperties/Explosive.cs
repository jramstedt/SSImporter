using System;
using System.Runtime.InteropServices;

namespace SS.ObjectProperties {
  [Serializable]
  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct Explosive {
    public const int NUM_DIRECT_GRENADE = 5;
    public const int NUM_TIMED_GRENADE = 3;

    public const int NUM_GRENADE = NUM_DIRECT_GRENADE + NUM_TIMED_GRENADE;

    public DamageInfo DamageInfo;

    public byte Touchiness;
    public byte Radius;
    public byte RadiusChange;
    public byte DamageChange;
    public byte AttackMass;

    public TypeFlags Flags;

    [Flags]
    public enum TypeFlags : ushort {
      Contact = 0x01,
      Motion = 0x02,
      Timing = 0x04,
      Mine = 0x05
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Direct {
      private byte Dummy;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Timed {
      public byte MinTime;
      public byte MaxTime;
      public byte Deviation;
    }
  }
}