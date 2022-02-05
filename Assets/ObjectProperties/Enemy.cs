using System;
using System.Runtime.InteropServices;
using Unity.Entities;

namespace SS.ObjectProperties {
  [Serializable]
  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct Enemy{
    public byte Intelligence;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
    public WeaponInfo[] Attacks;
    public byte Perception;
    public DefenceFlags Defence;
    public byte ProjectileOffset;
    public int Flags;
    [MarshalAs(UnmanagedType.U1)]
    public bool Mirror;
    
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
    public byte[] Frames;
    public byte AnimSpeed;

    public byte AttackSound;
    public byte NearSound;
    public byte HurtSound;
    public byte DeathSound;
    public byte NoticeSound;

    public Triple Corpse;

    public byte Views;
    public byte AltPercentage;
    public byte DisruptPercentage;
    public LootTemplate TreasureType;
    public InjuryEffectType HitEffect;
    public byte FireFrame;

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Mutant{
      private byte Dummy;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Robot{
      public byte BackupWeapon;
      public byte MetalThickness;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Cyborg{
      public byte ShieldEnergy;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Cyberspace{
      [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
      public byte[] Colors;
      [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
      public byte[] AlternativeColors;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Boss{
      private byte Dummy;
    }
    
    [Flags]
    public enum DefenceFlags : byte {
      IgnoreGravity = 0x01,
      Robot = 0x04, // set for repair-, serv-, exec-, maint- bots
    }

    public enum LootTemplate : byte {
      Nothing,
      Mutants,
      CyborgDrone,
      CyborgAssasin,
      CyborgWarrior,
      Flier,
      Sec1Bot,
      ExecBot,
      Enforcer,
      Sec2Bot,
      EliteMutantborg,
      Misc,
      MLStandard,
      RepairMaintenanceBot,
      ServiceBot
    }

    public enum InjuryEffectType : byte {
      Flesh,
      Plant,
      Metal
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct WeaponInfo {
      public int DamageType;
      public short DamageModifier;
      public byte OffenceValue;
      public byte Penetration;
      public byte AttackMass;
      public short AttackVelocity;
      public byte Accuracy;
      public byte AttRange;
      public int Speed;
      public int SlowProjectile;
    }

    [Flags]
    public enum BaseFlags : byte {
      NoMove = 0x01, // incapable of movement
      NoDoor = 0x02 // unable to open doors
    }
  }
}