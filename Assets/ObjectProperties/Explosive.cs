using System;
using System.Runtime.InteropServices;
using Unity.Entities;

namespace SS.ObjectProperties {
  [Serializable]
  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct Explosive{
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
    public struct Grenade{
      private byte Dummy;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Bomb{
      public byte MinTime;
      public byte MaxTime;
      public byte Deviation;
    }
  }
}