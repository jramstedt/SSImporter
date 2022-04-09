using System;
using System.Runtime.InteropServices;
using Unity.Entities;

namespace SS.ObjectProperties {
  [Serializable]
  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct DoorAndGrating {
    public const int NUM_NORMAL_DOOR = 10;
    public const int NUM_DOORWAYS_DOOR = 9;
    public const int NUM_FORCE_DOOR = 7;
    public const int NUM_ELEVATOR_DOOR = 5;
    public const int NUM_SPECIAL_DOOR = 10;
    
    public const int NUM_DOOR = NUM_NORMAL_DOOR + NUM_DOORWAYS_DOOR + NUM_FORCE_DOOR + NUM_ELEVATOR_DOOR + NUM_SPECIAL_DOOR;

    private byte Zero;

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Normal {
      private byte Zero;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Doorway {
      private byte Zero;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Force {
      private byte Zero;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Elevator {
      private byte Zero;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Special {
      private byte Zero;
    }
  }
}