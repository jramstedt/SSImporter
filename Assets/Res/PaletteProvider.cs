using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.IO.LowLevel.Unsafe;
using UnityEngine;
using static SS.Resources.ResourceFile;

namespace SS.Resources {
  public class PaletteProvider : IResProvider<Palette> {
    private class PaletteLoader : LoaderBase<Palette> {
      public PaletteLoader(ResourceFile resFile, ResourceInfo resInfo, ushort blockIndex) {
        InvokeCompletionEvent(Load(resFile, resInfo, blockIndex));
      }

      private Palette Load(ResourceFile resFile, ResourceInfo resInfo, ushort blockIndex) {
        return resFile.GetResourceData<Palette>(resInfo, blockIndex);
      }
    }

    IResHandle<Palette> IResProvider<Palette>.Provide(ResourceFile resFile, ResourceInfo resInfo, ushort blockIndex) {
      if (resInfo.info.ContentType != ResourceFile.ContentType.Palette)
        throw new Exception($"Resource {resInfo.info.Id:X4}:{blockIndex:X4} is not {nameof(ResourceFile.ContentType.Palette)}.");

      return new PaletteLoader(resFile, resInfo, blockIndex);
    }
  }

  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct Palette {
    public const byte PURPLE_8_BASE       = 0x20;
    public const byte MAIZE_8_BASE        = 0x28;
    public const byte RED_8_BASE          = 0x33;
    public const byte ORANGE_8_BASE       = 0x41;
    public const byte YELLOW_8_BASE       = 0x4b;
    public const byte GREEN_8_BASE        = 0x59;
    public const byte AQUA_8_BASE         = 0x66;
    public const byte BLUE_8_BASE         = 0x76;
    public const byte REDBROWN_8_BASE     = 0x85;
    public const byte BROWN_8_BASE        = 0x94;
    public const byte GRAYGREEN_8_BASE    = 0xA0;
    public const byte BRIGHTBROWN_8_BASE  = 0xA8;
    public const byte METALBLUE_8_BASE    = 0xB6;
    public const byte LIGHTBROWN_8_BASE   = 0xC6;
    public const byte GRAY_8_BASE         = 0xD6;

    public const byte PULSE_RED           = 0x1c;
    public const byte PULSE_GREEN         = 0x0d;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256 * 3)]
    private readonly byte[] rgb; // RGBRGB...

    public Palette(Palette copy) {
      rgb = new byte[256 * 3];
      for (int i = 0; i < rgb.Length; ++i)
        rgb[i] = copy.rgb[i];
    }

    public Color32 this[int index] {
      get {
        index *= 3;

        if (index > rgb.Length)
          throw new IndexOutOfRangeException();

        byte r = rgb[index];
        byte g = rgb[++index];
        byte b = rgb[++index];

        return new Color32(r, g, b, 0xFF);
      }
      set {
        index *= 3;

        if (index > rgb.Length)
          throw new IndexOutOfRangeException();

        rgb[index] = value.r;
        rgb[++index] = value.g;
        rgb[++index] = value.b;
      }
    }

    public readonly Color32 Get(int index, bool opaque) {
      opaque = opaque || index != 0;

      index *= 3;

      if (index > rgb.Length)
        throw new IndexOutOfRangeException();

      byte r = rgb[index];
      byte g = rgb[++index];
      byte b = rgb[++index];

      return new Color32(r, g, b, opaque ? (byte)0xFF : (byte)0x00);
    }

    public readonly NativeArray<Color32> ToNativeArray() {
      var palette = new NativeArray<Color32>(256, Allocator.Persistent);
      for (int i = 0; i < palette.Length; ++i) {
        var index = i * 3;
        palette[i] = new Color32(rgb[index], rgb[++index], rgb[++index], 0xFF);
      }
      return palette;
    }
  }
}