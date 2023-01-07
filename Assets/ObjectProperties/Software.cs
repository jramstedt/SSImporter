using System;
using System.Runtime.InteropServices;

namespace SS.ObjectProperties {
  [Serializable]
  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct Software {
    public const int NUM_OFFENSE_SOFTWARE = 7;
    public const int NUM_DEFENSE_SOFTWARE = 3;
    public const int NUM_ONESHOT_SOFTWARE = 4;
    public const int NUM_MISC_SOFTWARE = 5;
    public const int NUM_DATA_SOFTWARE = 3;

    public const int NUM_COMBAT_SOFTS = NUM_OFFENSE_SOFTWARE;
    public const int NUM_DEFENSE_SOFTS = NUM_DEFENSE_SOFTWARE;
    public const int NUM_MISC_SOFTS = NUM_ONESHOT_SOFTWARE + NUM_MISC_SOFTWARE;
    public const int NUM_SOFTWARE = NUM_OFFENSE_SOFTWARE + NUM_DEFENSE_SOFTWARE + NUM_ONESHOT_SOFTWARE + NUM_MISC_SOFTWARE + NUM_DATA_SOFTWARE;

    private short Flags;

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Offense {
      private byte Dummy;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Defense {
      private byte Dummy;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct OneShot {
      private byte Dummy;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Misc {
      private byte Dummy;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Data {
      private byte Dummy;
    }
  }

}