using System;
using System.Runtime.InteropServices;

namespace SS.ObjectProperties {
  [Serializable]
  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct Base {
    public const int NUM_OBJECT = Weapon.NUM_GUN + Ammunition.NUM_AMMO + Projectile.NUM_PHYSICS + Explosive.NUM_GRENADE + DermalPatch.NUM_DRUG + Hardware.NUM_HARDWARE + Software.NUM_SOFTWARE + Decoration.NUM_BIGSTUFF + Item.NUM_SMALLSTUFF + Fixture.NUM_FIXTURE + DoorAndGrating.NUM_DOOR + Animating.NUM_ANIMATING + Trap.NUM_TRAP + Container.NUM_CONTAINER + Enemy.NUM_CRITTER;

    public int Mass;
    public short Hitpoints;
    public byte Armour;
    public DrawType DrawType;
    public byte PhysicsModel;  // ?? physics_model
    public byte Hardness; // ?? hardness
    public byte Pep; // ?? ubyte pep; 
    public byte PhysicsX; // ?? ubyte physics_xr;
    public byte PhysicsY; // ?? ubyte physics_y;
    public byte PhysicsZ; // ?? ubyte physics_z;
    public int Resistances; // ?? int   resistances
    public byte Defence;
    public byte Toughness;
    public Flags Flags; // ?? flags
    public ushort MfdId; // ?? mfd_id
    public ushort Bitmap; // ?? bitmap_3d
    public byte DestroyEffect; // ?? destroy_effect

    public byte ClassFlags => (byte)(((int)Flags & 0x7000) >> 12);
  }

  public enum DrawType : byte {
    Unknown,
    TexturedPolygon,
    Bitmap,
    TextPolygon,
    DirectionalEnemySprite,
    AnimatedPolygon,
    Voxel,
    NoObj,
    FlatTexture,
    FlatPolygon,
    DirectionalSprite,
    Special,
    TranslucentPolygon
  }

  [Flags]
  public enum Flags : ushort {

  }
}
