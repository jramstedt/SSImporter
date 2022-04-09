using System.Runtime.InteropServices;

namespace SS.Resources {
  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct ShadeTableData {
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256 * 16)]
    private readonly byte[] paletteIndex;

    public byte this[int index] {
      get => paletteIndex[index];
      set => paletteIndex[index] = value;
    }
  }
}
