using System;
using System.Runtime.InteropServices;

namespace SS.ObjectProperties {
  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct Enemy {
    public const int NUM_MUTANT_CRITTER = 9;
    public const int NUM_ROBOT_CRITTER = 12;
    public const int NUM_CYBORG_CRITTER = 7;
    public const int NUM_CYBER_CRITTER = 7;
    public const int NUM_ROBOBABE_CRITTER = 2;

    public const int NUM_CRITTER = NUM_MUTANT_CRITTER + NUM_ROBOT_CRITTER + NUM_CYBORG_CRITTER + NUM_CYBER_CRITTER + NUM_ROBOBABE_CRITTER;

    public byte Intelligence;
    public WeaponInfo MainAttack;
    public WeaponInfo AlternativeAttack;
    public byte Perception;
    public DefenceFlags Defence;
    public byte ProjectileOffset;
    public int Flags;
    [MarshalAs(UnmanagedType.U1)] public bool Mirror;

    public EightOf<byte> Frames;
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
    public struct Mutant {
      private byte Dummy;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Robot {
      public byte BackupWeapon;
      public byte MetalThickness;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Cyborg {
      public short ShieldEnergy;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Cyberspace {
      public ThreeOf<byte> Colors;
      public ThreeOf<byte> AlternativeColors;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Boss {
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