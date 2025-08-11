namespace SS {
  public static class TimeUtils {
    public const ushort CIT_CYCLE = 280;

    public static long SecondsToSlowTicks(double seconds) => (long)(60.0 * seconds);
    public static long SecondsToFastTicks(double seconds) => (long)(CIT_CYCLE * seconds);

    public static ushort SecondsToTimestamp(double seconds) => (ushort)(((ulong)(CIT_CYCLE * seconds) >> 4) & 0xFFFF);
    public static ushort FastTicksToTimestamp(uint ticks) => (ushort)((ticks >> 4) & 0xFFFF);
    public static int TimestampToFastTicks(ushort timestamp) => timestamp << 4;
    
    /// <summary>
    /// This hack is to handle timestamp overflow.
    /// </summary>
    public static ushort ExpandTimestamp(ushort timestamp, ushort gametime) {
      if (timestamp <= (gametime - (ushort.MaxValue >> 1)))
        timestamp += ushort.MaxValue;
      else if (timestamp >= (gametime + (ushort.MaxValue >> 1)))
        timestamp -= ushort.MaxValue;

      return timestamp;
    }
  }
}
