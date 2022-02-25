using System.Runtime.InteropServices;
using Unity.Entities;

namespace SS {
  public enum EventType : ushort {
    Null,
    Grenade,
    Explosion,
    Door,
    Trap,
    Expose,
    Floor,
    Ceil,
    Light,
    Bark,
    Email
  }

  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public unsafe struct ScheduleEvent : IComponentData {
    public ushort Timestamp;
    public EventType Type;
    public fixed byte Data[4];

    public static ushort TicksToTimestamp(int ticks) => (ushort)((ticks >> 4) & 0xFFFF);
    public static int TimestampToTicks(ushort timestamp) => timestamp << 4;
  }

  [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 4)]
  public struct GrenadeScheduleEvent {
    public short ObjectIndex;
    public byte UniqueId; // Matches ObjectInstance.Explosive.UniqueId
    private byte Dummy;
  }

  [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 4)]
  public struct DoorScheduleEvent {
    public short ObjectIndex;
    public byte UniqueId; // Matches ObjectInstance.Explosive.UniqueId
    private byte Dummy;
  }

  [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 4)]
  public struct TrapScheduleEvent {
    public short TargetObjectIndex;
    public short SourceObjectIndex;
  }

  [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 4)]
  public struct ExposureScheduleEvent {
    public sbyte Damage;
    public byte Type;
    /// <summary>Time for next exposure.</summary>
    public byte TimeSeconds;
    public byte Count;
  }

  [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 4)]
  public struct HeightScheduleEvent {
    /*
      Semaphor and Key are used to check if map element is already changing floor or ceiling height.
    */

    public byte Semaphor;
    public byte Key;
    /// <summary>Signed for direction.</summary>
    public sbyte StepsRemaining;
    public byte SoundEffectCode;
  }

  
  [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 4)]
  public struct EmailScheduleEvent {
    public enum TypeVersion : byte {
      Email,
      Log,
      Data
    }

    public ushort DataMung; // version, munge

    public byte Mung => (byte)(DataMung & 0xFF);
    public TypeVersion Version => (TypeVersion)(DataMung >> 8);
  }
}
