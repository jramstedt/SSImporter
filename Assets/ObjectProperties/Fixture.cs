using System;
using System.Runtime.InteropServices;
using Unity.Entities;

namespace SS.ObjectProperties {
  [Serializable]
  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct Fixture {
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