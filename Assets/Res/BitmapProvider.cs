using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities.Serialization;
using Unity.Mathematics;
using UnityEngine;
using static SS.Resources.ResourceFile;

namespace SS.Resources {
  [BurstCompile]
  public class BitmapProvider : IResProvider<BitmapSet> {
    [BurstCompile]
    private class BitmapLoader : LoaderBase<BitmapSet> {
      public BitmapLoader(ResourceFile resFile, ResourceInfo resInfo, ushort blockIndex) {
        InvokeCompletionEvent(Load(resFile, resInfo, blockIndex));
      }

      [BurstCompile]
      private unsafe BitmapSet Load(ResourceFile resFile, ResourceInfo resInfo, ushort blockIndex) {
        byte[] rawResource = resFile.GetResourceData(resInfo, blockIndex);

        BufferBinaryReader bbr;
        fixed (byte* rawResourcePtr = rawResource) {
          bbr = new BufferBinaryReader(rawResourcePtr, rawResource.Length);
        }

        Bitmap bitmap = bbr.Read<Bitmap>();

        NativeArray<byte> pixelData;

        //int bytesPerPixel = bitmap.Stride / bitmap.Width;

        if (bitmap.BitmapType == BitmapType.Flat8) {
          pixelData = new NativeArray<byte>(bitmap.Height * bitmap.Stride, Allocator.Persistent);
          bbr.ReadBytes(pixelData, pixelData.Length);
        } else if (bitmap.BitmapType == BitmapType.RSD8) {
          pixelData = RunLengthDecode(bitmap, bbr);
        } else {
          throw new Exception($"Unsupported bitmap type {bitmap.BitmapType}.");
        }

        // TODO using private palette should be decided by code using the texture.

        NativeArray<byte> palette = default;
        if (bitmap.PaletteOffset != 0) {
          palette = new NativeArray<byte>(UnsafeUtility.SizeOf<PrivatePalette>(), Allocator.Persistent);
          bbr.Position = bitmap.PaletteOffset;
          bbr.ReadBytes(palette, palette.Length);
        }
        
        bbr.Dispose();
        
        return new BitmapSet {
          Bitmap = bitmap,
          Data = pixelData,
          Palette = palette
        };
      }

      [BurstCompile]
      private unsafe NativeArray<byte> RunLengthDecode(Bitmap bitmap, BufferBinaryReader bbr) {
        var bitmapData = new NativeArray<byte>(bitmap.Width * bitmap.Height, Allocator.Persistent);
        var bbw = new BufferBinaryWriter((byte*)bitmapData.GetUnsafePtr(), bitmapData.Length);
        
        while (bbr.Position < bbr.Length) {
          byte cmd = bbr.ReadByte();

          if (cmd == 0x00) { // 00 nn xx      write nn bytes of colour xx
            byte amount = bbr.ReadByte();
            byte data = bbr.ReadByte();
            
            bbw.WriteBytes(data, amount);
          } else if (cmd < 0x80) { // 0<nn<0x80	copy nn bytes direct
            bbw.CopyBytes(ref bbr, cmd);
          } else if (cmd == 0x80) {
            byte param1 = bbr.ReadByte();
            byte param2 = bbr.ReadByte();

            if (param1 == 0x00 && param2 == 0x00) // EOF
              break;
            if (param2 < 0x80) { // skip (nn*256+mm) bytes
              // TODO if video frame, copy from previous frame
              bbw.WriteBytes(0x00, param2 * 256 + param1);
            } else if (param2 < 0xC0) { // copy ((nn&0x3f)*256+mm) bytes
              bbw.CopyBytes(ref bbr, (param2 & 0x3F) * 256 + param1);
            } else if (param2 > 0xC0) { // 0xC0<nn	write ((nn&0x3f)*256+mm) bytes of colour xx
              byte color = bbr.ReadByte();

              bbw.WriteBytes(color, (param2 & 0x3F) * 256 + param1);
            } else {
              throw new Exception($"Unhandled subcommand {param2:2X}");
            }
          } else { // 0x80<nn	skip (nn&0x7f) bytes
            // TODO if video frame, copy from previous frame
            bbw.WriteBytes(0x00, cmd & 0x7F);
          }
        }
        
        bbw.Dispose();

        return bitmapData;
      }
    }

    IResHandle<BitmapSet> IResProvider<BitmapSet>.Provide(ResourceFile resFile, ResourceInfo resInfo, ushort blockIndex) {
      if (resInfo.info.ContentType != ContentType.Image)
        throw new Exception($"Resource {resInfo.info.Id:X4}:{blockIndex:X4} is not {nameof(ContentType.Image)}.");

      return new BitmapLoader(resFile, resInfo, blockIndex);
    }
  }

  public enum BitmapType : byte {
    Device = 0x00,        // BMT_DEVICE
    Mono = 0x01,          // BMT_MONO
    Flat8 = 0x02,         // BMT_FLAT8
    Flat24 = 0x03,        // BMT_FLAT24
    RSD8 = 0x04,          // BMT_RSD8
    Translucent8 = 0x05,  // BMT_TLUC8
    Span = 0x06,          // BMT_SPAN
    Generic = 0x07        // BMT_GEN
  }

  [Flags]
  public enum BitmapFlags : ushort {
    Transparent = 0x0001,
    UnpackAsTranslucent8 = 0x0002
  }

  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct LGPoint {
    public short x;
    public short y;

    public LGPoint(short x, short y) {
      this.x = x;
      this.y = y;
    }
    
    public static implicit operator int2(LGPoint p) => new (p.x, p.y);
    public static implicit operator Vector2Int(LGPoint p) => new (p.x, p.y);
  }

  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct LGRect {
    public LGPoint ul;
    public LGPoint lr;
    
    public static implicit operator RectInt(LGRect p) => new (p.ul.x, p.ul.y, p.lr.x - p.ul.x, p.lr.y - p.ul.y);
  }

  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct Bitmap {
    private uint Reserved; // 32-bit runtime pointer to bitmap in memory
    public BitmapType BitmapType;
    private byte Align;
    public BitmapFlags Flags;
    public ushort Width;
    public ushort Height;
    public ushort Stride;
    public byte WidthShift;
    public byte HeightShift;
    
    private unsafe fixed ushort pivot[4];

    public uint PaletteOffset;

    public bool Transparent {
      readonly get => Flags.HasFlag(BitmapFlags.Transparent);
      set {
        if (value)
          Flags |= BitmapFlags.Transparent;
        else
          Flags &= ~BitmapFlags.Transparent;
      }
    }

    public int2 Size {
      readonly get => new int2(Width, Height);
      set {
        var bytesPerPixel = Width > 0 ? Stride / Width : 1;
        Width = (ushort)value.x;
        WidthShift = (byte)math.floorlog2(value.x);
        Stride = (ushort)(Width * bytesPerPixel);
        Height = (ushort)value.y;
        HeightShift = (byte)math.floorlog2(value.y);
      }
    }
    
    public readonly unsafe LGRect AnchorArea {
      get {
        fixed (ushort* ptr = pivot)
          return *(LGRect*)ptr;
      }
      set {
        fixed (ushort* ptr = pivot)
          *(LGRect*)ptr = value;
      }
    }

    public readonly unsafe LGPoint AnchorPoint {
      get {
        fixed (ushort* ptr = pivot)
          return *(LGPoint*)ptr;
      }
      set {
        fixed (ushort* ptr = pivot)
          *(LGPoint*)ptr = value;
      }
    }

    public readonly override string ToString() {
      return $"BitmapType = {BitmapType}, Width = {Width}, Height = {Height}, Stride = {Stride}, WidthShift = {WidthShift}, HeightShift = {HeightShift}";
    }
  }

  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct PrivatePalette {
    private readonly uint header;
    private unsafe fixed byte values[256 * 3];

    public unsafe Color32 this[int index] {
      set {
        if (index > 255)
          throw new IndexOutOfRangeException();

        index *= 3;

        values[index] = value.r;
        values[++index] = value.g;
        values[++index] = value.b;
      }
    }

    public unsafe Color32 Get(int index, bool opaque) {
      if (index > 255)
        throw new IndexOutOfRangeException();

      opaque = opaque || index != 0;

      index *= 3;

      byte r = values[index];
      byte g = values[++index];
      byte b = values[++index];

      return new Color32(r, g, b, opaque ? (byte)0xFF : (byte)0x00);
    }
  }

  public struct BitmapSet: IDisposable {
    public Bitmap Bitmap;
    public NativeArray<byte> Data;
    public NativeArray<byte> Palette;
    
    public void Dispose() {
      Data.Dispose();
      Palette.Dispose();
    }
  }
}
