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
    public byte PhysicsX;
    public byte PhysicsY; // unused ubyte physics_y
    public byte PhysicsZ;
    public int Resistances; // ?? int   resistances
    public byte Defence;
    public byte Toughness;
    public FlagMasks Flags;
    public ushort MfdId; // ?? mfd_id
    public ushort Bitmap;
    public byte DestroyEffect; // ?? destroy_effect

    public readonly byte Radius => PhysicsX;

    public readonly bool IsTrash => (Flags & FlagMasks.InventoryGeneral) == FlagMasks.InventoryGeneral;
    public readonly bool IsPhysicsPreserved => (Flags & FlagMasks.PreservePhysics) == FlagMasks.PreservePhysics;
    public readonly UseModes UseMode => (UseModes)((ushort)(Flags & FlagMasks.InventoryUsable) >> 2);
    public readonly bool IsNoCursor => (Flags & FlagMasks.NoCursor) == FlagMasks.NoCursor;
    public readonly bool IsBlockRendering => (Flags & FlagMasks.BlockRendering) == FlagMasks.BlockRendering;
    public readonly LightTypes LightType => (LightTypes)((ushort)(Flags & FlagMasks.LightType) >> 6);
    public readonly TerrainTypes TerrainType => (TerrainTypes)((ushort)(Flags & FlagMasks.Terrain) >> 8);
    public readonly bool IsDoubleSize => (Flags & FlagMasks.DoubleSize) == FlagMasks.DoubleSize;
    public readonly bool IsTerrainDamage => (Flags & FlagMasks.TerrainDamage) == FlagMasks.TerrainDamage;
    public readonly bool IsUseless => (Flags & FlagMasks.Useless) == FlagMasks.Useless;

    public readonly byte ClassFlags => (byte)((ushort)(Flags & FlagMasks.ClassFlags) >> 12);

    public readonly ushort BitmapIndex => (ushort)((Bitmap & 0x3FF) + ((Bitmap & 0x8000) >> 5));
    public readonly byte BitmapFrameCount => (byte)((Bitmap & 0x7000) >> 12);
    public readonly bool BitmapRepeat => (Bitmap & 0x0800) == 0x0800;
    public readonly bool BitmapAnim => (Bitmap & 0x0400) == 0x0400;

    [Flags]
    public enum FlagMasks : ushort {
      InventoryGeneral = 0x0001,
      PreservePhysics = 0x0002,
      InventoryUsable = 0x000C,
      NoCursor = 0x0010,
      BlockRendering = 0x0020,
      LightType = 0x00C0,
      Terrain = 0x0300,
      DoubleSize = 0x0400,
      TerrainDamage = 0x0800,
      ClassFlags = 0x7000,
      Useless = 0x8000
    }

    public enum UseModes : byte {
      Pickup,
      Use
    }

    public enum LightTypes : byte {
      Normal,         // simple
      Complicated,
      Never,
      InstanceLookup  // look from instance data
    }

    public enum TerrainTypes : byte {
      Ignore,
      Wall,
      Complex
    }
  }

  public enum DrawType : byte {
    Unknown,                // FAUBJ_UNKNOWN
    TexturedPolygon,        // FAUBJ_TEXTPOLY
    Bitmap,                 // FAUBJ_BITMAP
    TerrainPolygon,         // FAUBJ_TPOLY
    DirectionalEnemySprite, // FAUBJ_CRIT
    AnimatedPolygon,        // FAUBJ_ANIMPOLY
    Voxel,                  // FAUBJ_VOX
    NoObj,                  // FAUBJ_NOOBJ
    FlatTexture,            // FAUBJ_TEXBITMAP
    FlatPolygon,            // FAUBJ_FLATPOLY
    DirectionalSprite,      // FAUBJ_MULTIVIEW
    Special,                // FAUBJ_SPECIAL
    TranslucentPolygon      // FAUBJ_TL_POLY
  }
}
