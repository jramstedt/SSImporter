using System;
using System.Runtime.InteropServices;
using Unity.Entities;

namespace SS.ObjectProperties {
  [Serializable]
  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct Hardware {
    public const int NUM_GOGGLE_HARDWARE = 5;
    public const int NUM_HARDWARE_HARDWARE = 10;

    public const int NUM_HARDWARE = NUM_GOGGLE_HARDWARE + NUM_HARDWARE_HARDWARE;

    private short Flags;

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Goggle {
      private byte Dummy;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Hard {
      private short TargetFlag;
    }
  }
}
