using System;
using System.Runtime.InteropServices;

namespace SS.ObjectProperties {
  [Serializable]
  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct Base {
    public int Mass;
    public short Hitpoints;
    public byte Armour;
    public DrawType DrawType; // ?? render_type
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
    // ?? render_type
  }

  [Flags]
  public enum Flags : ushort {

  }
}
