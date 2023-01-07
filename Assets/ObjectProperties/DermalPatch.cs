using System;
using System.Runtime.InteropServices;

namespace SS.ObjectProperties {
  [Serializable]
  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct DermalPatch {
    public const int NUM_STATS_DRUG = 7;
    public const int NUM_DRUG = NUM_STATS_DRUG;

    private byte Intensity;
    private byte Delay;
    private byte Duration;
    private int Effect;
    private int SideEffect;
    private int AfterEffect;
    short Flags;

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Stats {
      private short Efficteveness;
      private byte SoundEffect;
      private int Duration;
    }
  }
}
