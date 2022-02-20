using UnityEngine;
using System.Collections;
using UnityEngine.ResourceManagement.ResourceProviders;
using System;
using UnityEngine.ResourceManagement.ResourceLocations;
using System.Runtime.InteropServices;
using System.IO;
using Unity.Collections;

namespace SS.Resources {
  public class PaletteProvider : ResourceProviderBase {
    public override Type GetDefaultType(IResourceLocation location) => typeof(Palette);
    public override void Provide(ProvideHandle provideHandle) {
      var location = provideHandle.Location;

      var resFile = provideHandle.GetDependency<ResourceFile>(0);
      if (resFile == null) {
        provideHandle.Complete<AudioClip>(null, false, new Exception($"Resource file failed to load for location {location.PrimaryKey}."));
        return;
      }

      var key = provideHandle.ResourceManager.TransformInternalId(location);
      ushort resId, block;
      if (!Utils.ExtractResourceIdAndBlock(key, out resId, out block)) {
        provideHandle.Complete<AudioClip>(null, false, new Exception($"Resource {location.InternalId} with key {key} is not valid."));
        return;
      }

      if (resFile.GetResourceInfo(resId).info.ContentType != ResourceFile.ContentType.Palette) {
        provideHandle.Complete<AudioClip>(null, false, new Exception($"Resource {location.InternalId} is not {nameof(ResourceFile.ContentType.Palette)}."));
        return;
      }

      byte[] rawResource = resFile.GetResourceData(resId, block);

      using (MemoryStream ms = new MemoryStream(rawResource)) {
        BinaryReader msbr = new BinaryReader(ms);
        provideHandle.Complete(msbr.Read<Palette>(), true, null);
      }
    }
  }

  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct Palette {
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

    public Color32 Get(int index, bool opaque) {
      opaque = opaque || index != 0;

      index *= 3;

      if (index > rgb.Length)
        throw new IndexOutOfRangeException();

      byte r = rgb[index];
      byte g = rgb[++index];
      byte b = rgb[++index];

      return new Color32(r, g, b, opaque ? (byte)0xFF : (byte)0x00);
    }

    public NativeArray<Color32> ToNativeArray() {
      var palette = new NativeArray<Color32>(256, Allocator.Persistent);
      for (int i = 0; i < palette.Length; ++i) {
        var index = i * 3;
        palette[i] = new Color32(rgb[index], rgb[++index], rgb[++index], 0xFF);
      }
      return palette;
    }
  }
}