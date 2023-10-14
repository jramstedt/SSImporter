using System.Runtime.InteropServices;

namespace SS.Resources {
  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct TexturePropertiesData {
    public const int Version = 9;

    private readonly int version;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 396)] // 400 / Marshal.SizeOf<TextureProperties>()
    private readonly TextureProperties[] textureProperties;

    public readonly TextureProperties this[int index] {
      get => textureProperties[index];
      set => textureProperties[index] = value;
    }
  }
}
