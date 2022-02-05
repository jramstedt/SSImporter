using System;
using System.Runtime.InteropServices;
using Unity.Entities;

namespace SS.ObjectProperties {
  [Serializable]
  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct DermalPatch{
    private byte Intensity;
    private byte Delay;
    private byte Duration;
    private int Effect;
    private int SideEffect;
    private int AfterEffect;
    short Flags;

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct All{
      private short Efficteveness;
      private byte SoundEffect;
      private int Duration;
    }
  }
}
