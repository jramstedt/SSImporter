using SS.Resources;
using System;
using System.Runtime.InteropServices;

namespace SS {
  static class ObjectConstants {
    public const int NUM_OBJECTS = 872;
    public const int NUM_OBJECTS_GUN = 16;
    public const int NUM_OBJECTS_AMMO = 32;
    public const int NUM_OBJECTS_PHYSICS = 32;
    public const int NUM_OBJECTS_GRENADE = 32;
    public const int NUM_OBJECTS_DRUG = 32;
    public const int NUM_OBJECTS_HARDWARE = 8;
    public const int NUM_OBJECTS_SOFTWARE = 16;
    public const int NUM_OBJECTS_BIGSTUFF = 176;
    public const int NUM_OBJECTS_SMALLSTUFF = 128;
    public const int NUM_OBJECTS_FIXTURE = 64;
    public const int NUM_OBJECTS_DOOR = 64;
    public const int NUM_OBJECTS_ANIMATING = 32;
    public const int NUM_OBJECTS_TRAP = 160;
    public const int NUM_OBJECTS_CONTAINER = 64;
    public const int NUM_OBJECTS_CRITTER = 64;
  }

  public enum ObjectClass : byte {
    Weapon,         // CLASS_GUN
    Ammunition,     // CLASS_AMMO
    Projectile,     // CLASS_PHYSICS
    Explosive,      // CLASS_GRENADE
    DermalPatch,    // CLASS_DRUG
    Hardware,       // CLASS_HARDWARE
    SoftwareAndLog, // CLASS_SOFTWARE
    Decoration,     // CLASS_BIGSTUFF
    Item,           // CLASS_SMALLSTUFF
    Interface,      // CLASS_FIXTURE
    DoorAndGrating, // CLASS_DOOR
    Animated,       // CLASS_ANIMATING
    Trigger,        // CLASS_TRAP
    Container,      // CLASS_CONTAINER
    Enemy,          // CLASS_CRITTER

    NumClasses,
    ClassFirst = Weapon
  }

  [Serializable]
  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct Triple : IEquatable<Triple> {
    private byte Padding;
    public ObjectClass Class;
    public byte SubClass;
    public byte Type;


    public readonly bool Equals(Triple other) => Class == other.Class && SubClass == other.SubClass && Type == other.Type;

    public override readonly bool Equals(object obj) => obj is Triple other && Class == other.Class && SubClass == other.SubClass && Type == other.Type;

    public override readonly int GetHashCode() => HashCode.Combine(Class, SubClass, Type);

    public override readonly string ToString() => $"{Class}:{SubClass}:{Type}";

    public static bool operator ==(Triple left, Triple right) => left.Class == right.Class && left.SubClass == right.SubClass && left.Type == right.Type;
    public static bool operator !=(Triple left, Triple right) => left.Class != right.Class || left.SubClass != right.SubClass || left.Type != right.Type;

    public static implicit operator Triple(ObjectInstance instance) => new() { Class = instance.Class, SubClass = instance.SubClass, Type = instance.Info.Type };
    public static implicit operator Triple(int triple) => new() { Class = (ObjectClass)(triple >> 16 & 0xFF), SubClass = (byte)(triple >> 8 & 0xFF), Type = (byte)(triple & 0xFF) };
    public static implicit operator int(Triple triple) => (ushort)(((byte)triple.Class) << 16 | triple.SubClass << 8 | triple.Type);
  }
}