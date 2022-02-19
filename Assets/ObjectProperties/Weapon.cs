using System;
using System.Runtime.InteropServices;
using Unity.Entities;

namespace SS.ObjectProperties {
  [Serializable]
  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct Weapon {
    public byte FiringRate;
    public byte AmmoInfo;

    public byte AmmoSubClass => (byte)((AmmoInfo >> 4) & 0x0F);
    public byte AmmoType => (byte)(AmmoInfo & 0x0F);

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SemiAutomatic {
      private byte Dummy;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct FullAutomatic {
      private byte Dummy;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Projectile {
      public DamageInfo DamageInfo;

      private byte Speed;
      public Triple ProjectileType;
      public byte AttackMass;
      public ushort AttackSpeed;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Melee {
      public DamageInfo DamageInfo;

      public byte Energy;
      public byte AttackMass;
      public byte AttackRange;
      public ushort AttackSpeed;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Beam {
      public DamageInfo DamageInfo;
      
      public byte MaxCharge;
      public byte AttackMass;
      public byte AttackRange;
      public ushort AttackSpeed;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct EnergyProjectile {
      public DamageInfo DamageInfo;

      public byte MaxCharge;
      public byte AttackMass;
      public ushort AttackSpeed;

      public byte Speed;
      public Triple ProjectileType;
      
      public byte Flags;
    }
  }
  
  [Flags]
  public enum DamageType : byte {
    None = 0x00,

    Explosion = 0x01,
    Energy = 0x02,
    Magnetic = 0x04,
    Radiation = 0x08,

    Gas = 0x10,
    Tranquil = 0x20,
    Needle = 0x40,
    Sleep = 0x80,

    CyberExplosion = Explosion,
    CyberProjecile = Energy,
    CyberDrill = Magnetic
  }

  [Serializable]
  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct DamageInfo {
    public ushort Damage;
    public byte Offence;

    public DamageType DamageType;
    public byte SpecialDamage;
    private ushort Unknown;

    public byte ArmorPenetration;

    public int PrimaryDamage => (SpecialDamage >> 8) & 0x0F;
    public int SuperDamage => (SpecialDamage >> 12) & 0x0F;
  }
}