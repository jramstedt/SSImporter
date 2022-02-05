using System;
using System.Runtime.InteropServices;

namespace SS {
  public enum ObjectClass : byte {
    Weapon,
    Ammunition,
    Projectile,
    Explosive,
    DermalPatch,
    Hardware,
    SoftwareAndLog,
    Decoration,
    Item,
    Interface,
    DoorAndGrating,
    Animated,
    Trigger,
    Container,
    Enemy,

    NumClasses,
    ClassFirst = Weapon
  }

  [Serializable]
  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct Triple {
    private byte Padding;
    public ObjectClass Class;
    public byte SubClass;
    public byte Type;

    public static implicit operator Triple(int triple) => new Triple { Class = (ObjectClass)(triple >> 16 & 0xFF), SubClass = (byte)(triple >> 8 & 0xFF), Type = (byte)(triple & 0xFF) };
    public static implicit operator int(Triple triple) => (ushort)(((byte)triple.Class) << 16 | triple.SubClass << 8 | triple.Type);
  }
}