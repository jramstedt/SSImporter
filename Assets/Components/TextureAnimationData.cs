using System;
using System.Runtime.InteropServices;
using Unity.Entities;

namespace SS.Resources {
  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct TextureAnimationData : IComponentData {
    [Flags]
    public enum FlagMask : byte {
      Cyclic = 0x01, // Ping Pong
      Reversing = 0x80
    }

    public readonly ushort FrameTime;
    public ushort TimeRemaining;
    public sbyte CurrentFrame;
    public readonly byte TotalFrames;
    public FlagMask Flags;

    public bool IsCyclic => (Flags & FlagMask.Cyclic) == FlagMask.Cyclic;
    public bool IsReversing => (Flags & FlagMask.Reversing) == FlagMask.Reversing;
  }
}
