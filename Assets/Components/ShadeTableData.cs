using System.Runtime.InteropServices;

namespace SS.Resources {
  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct ShadeTableData {
    private unsafe fixed byte paletteIndex[256 * 16];

    public unsafe byte this[int index] {
      get => paletteIndex[index];
      set => paletteIndex[index] = value;
    }
  }
}
