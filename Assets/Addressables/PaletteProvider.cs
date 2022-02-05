using UnityEngine;
using System.Collections;
using UnityEngine.ResourceManagement.ResourceProviders;
using System;
using UnityEngine.ResourceManagement.ResourceLocations;
using System.Runtime.InteropServices;
using System.IO;

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
    private readonly byte[] palette; // RGBRGB...

    public Palette(Palette copy) {
      palette = new byte[256 * 3];
      for (int i = 0; i < palette.Length; ++i)
        palette[i] = copy.palette[i];
    }

    public Color32 this[int index] {
      get {
        index *= 3;

        if (index > palette.Length)
          throw new IndexOutOfRangeException();

        byte r = palette[index];
        byte g = palette[++index];
        byte b = palette[++index];

        return new Color32(r, g, b, 0xFF);
      }
      set {
        index *= 3;

        if (index > palette.Length)
          throw new IndexOutOfRangeException();

        palette[index] = value.r;
        palette[++index] = value.g;
        palette[++index] = value.b;
      }
    }

    public Color32 Get(int index, bool opaque) {
      opaque = opaque || index != 0;

      index *= 3;

      if (index > palette.Length)
        throw new IndexOutOfRangeException();

      byte r = palette[index];
      byte g = palette[++index];
      byte b = palette[++index];

      return new Color32(r, g, b, opaque ? (byte)0xFF : (byte)0x00);
    }

    // TODO PALFX system
    public Palette RotateSlots(int steps) {
      short[] rotatingSlots = new short[] {
        0x0304,
        0x0B04,
        0x1004,
        0x1502,
        0x1802,
        0x1B04,
      };

      Palette ret = new Palette(this);
      for (int slotIndex = 0; slotIndex < rotatingSlots.Length; ++slotIndex) {
        int count = rotatingSlots[slotIndex] & 0xFF;
        int startIndex = rotatingSlots[slotIndex] >> 8;

        for (int i = 0; i < count; ++i) {
          int newIndex = (i + steps) % count;
          ret[startIndex + i] = this[startIndex + newIndex];
        }
      }

      return ret;
    }
  }
}