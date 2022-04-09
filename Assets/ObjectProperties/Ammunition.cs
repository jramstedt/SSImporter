using System;
using System.Runtime.InteropServices;
using Unity.Entities;

namespace SS.ObjectProperties {
  [Serializable]
  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct Ammunition {
    public const int NUM_PISTOL_AMMO = 2;
    public const int NUM_NEEDLE_AMMO = 2;
    public const int NUM_MAGNUM_AMMO = 3;
    public const int NUM_RIFLE_AMMO = 2;
    public const int NUM_FLECHETTE_AMMO = 2;
    public const int NUM_AUTO_AMMO = 2;
    public const int NUM_PROJ_AMMO = 2;

    public const int NUM_AMMO = NUM_PISTOL_AMMO + NUM_NEEDLE_AMMO + NUM_MAGNUM_AMMO + NUM_RIFLE_AMMO + NUM_FLECHETTE_AMMO + NUM_AUTO_AMMO + NUM_PROJ_AMMO;

    public DamageInfo DamageInfo;

    public byte CartridgeSize;
    public byte BulletMass;
    private ushort BulletSpeed;
    public byte Range;
    private byte RecoilForce;

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Pistol {
      private byte Dummy;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Needle {
      private byte Dummy;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Magnum {
      private byte Dummy;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Rifle {
      private byte Dummy;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Flechette {
      private byte Dummy;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Auto {
      private byte Dummy;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Projectile {
      private byte Dummy;
    }
  }
}
