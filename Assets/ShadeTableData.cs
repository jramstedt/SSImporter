using System.Runtime.InteropServices;

namespace SS.Resources {
  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public unsafe struct ShadeTableData {
    private fixed byte paletteIndex[256 * 16];

    public byte this[int index] {
      get => paletteIndex[index];
      set => paletteIndex[index] = value;
    }
  }
}
