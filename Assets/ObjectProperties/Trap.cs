using System;
using System.Runtime.InteropServices;
using Unity.Entities;

namespace SS.ObjectProperties {
  [Serializable]
  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct Trap {
    public const int NUM_TRIGGER_TRAP = 13;
    public const int NUM_FEEDBACKS_TRAP = 1;
    public const int NUM_SECRET_TRAP = 5;

    public const int NUM_TRAP = NUM_TRIGGER_TRAP + NUM_FEEDBACKS_TRAP + NUM_SECRET_TRAP;

    private byte Dummy;

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Trigger {
      private byte Dummy;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Feedback {
      private byte Dummy;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Secret {
      private byte Dummy;
    }
  }
}