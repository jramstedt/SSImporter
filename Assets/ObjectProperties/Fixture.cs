using System;
using System.Runtime.InteropServices;

namespace SS.ObjectProperties {
  [Serializable]
  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct Fixture {
    public const int NUM_CONTROL_FIXTURE = 9;
    public const int NUM_RECEPTACLE_FIXTURE = 7;
    public const int NUM_TERMINAL_FIXTURE = 3;
    public const int NUM_PANEL_FIXTURE = 11;
    public const int NUM_VENDING_FIXTURE = 2;
    public const int NUM_CYBER_FIXTURE = 3;
    public const int NUM_FIXTURE = NUM_CONTROL_FIXTURE + NUM_RECEPTACLE_FIXTURE + NUM_TERMINAL_FIXTURE + NUM_PANEL_FIXTURE + NUM_VENDING_FIXTURE + NUM_CYBER_FIXTURE;

    private byte Characteristics;

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Control {
      private byte Zero;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Receptacle {
      private byte Zero;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Terminal {
      private byte Zero;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Panel {
      private byte Zero;
    }

    [Serializable]
    [StructLayout(LayoutKind.Explicit, Size = 0)]
    public struct Vending {
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Cyber {
      private byte Zero;
    }
  }
}