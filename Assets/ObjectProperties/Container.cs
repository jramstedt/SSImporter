using System;
using System.Runtime.InteropServices;

namespace SS.ObjectProperties {
  [Serializable]
  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct Container {
    public const int NUM_ACTUAL_CONTAINER = 3;
    public const int NUM_WASTE_CONTAINER = 3;
    public const int NUM_LIQUID_CONTAINER = 4;
    public const int NUM_MUTANT_CORPSE_CONTAINER = 8;
    public const int NUM_ROBOT_CORPSE_CONTAINER = 13;
    public const int NUM_CYBORG_CORPSE_CONTAINER = 7;
    public const int NUM_OTHER_CORPSE_CONTAINER = 8;

    public const int NUM_CONTAINER = NUM_ACTUAL_CONTAINER + NUM_WASTE_CONTAINER + NUM_LIQUID_CONTAINER + NUM_MUTANT_CORPSE_CONTAINER + NUM_ROBOT_CORPSE_CONTAINER + NUM_CYBORG_CORPSE_CONTAINER + NUM_OTHER_CORPSE_CONTAINER;

    private short Contents;
    private byte NumContents;

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Actual {
      private byte Dummy;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Waste {
      private byte Dummy;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Liquid {
      private byte Dummy;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct MutantCorpse {
      private byte Dummy;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct RobotCorpse {
      private byte Dummy;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CyborgCorpse {
      private byte Dummy;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct OtherCorpse {
      private byte Dummy;
    }
  }
}