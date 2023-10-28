using System;
using System.IO;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;
using static SS.Resources.ResourceFile;

namespace SS.Resources {
  public class BitmapProvider : IResProvider<BitmapSet> {
    private class BitmapLoader : LoaderBase<BitmapSet> {
      public BitmapLoader(ResourceFile resFile, ResourceInfo resInfo, ushort blockIndex) {
        InvokeCompletionEvent(Load(resFile, resInfo, blockIndex));
      }

      public BitmapSet Load(ResourceFile resFile, ResourceInfo resInfo, ushort blockIndex) {
        byte[] rawResource = resFile.GetResourceData(resInfo, blockIndex);

        using MemoryStream ms = new(rawResource);
        using BinaryReader msbr = new(ms);

        Bitmap bitmap = msbr.Read<Bitmap>();

        byte[] pixelData;

        //int bytesPerPixel = bitmap.Stride / bitmap.Width;

        if (bitmap.BitmapType == BitmapType.Uncompressed) {
          pixelData = msbr.ReadBytes(bitmap.Height * bitmap.Stride);
        } else if (bitmap.BitmapType == BitmapType.Compressed) {
          pixelData = RunLengthDecode(bitmap, msbr);
        } else {
          throw new Exception($"Unsupported bitmap type {bitmap.BitmapType}.");
        }

        // TODO using private palette should be decided by code using the texture.

        PrivatePalette? palette = null;

        if (bitmap.PaletteOffset != 0) {
          long resourceOffset = resInfo.dataOffset;

          BinaryReader binaryReader = resFile.GetBinaryReader(resourceOffset + bitmap.PaletteOffset);
          palette = binaryReader.Read<PrivatePalette>();
        }

        #region Create textures
        LGRect anchorRect = bitmap.AnchorArea;
        LGPoint anchorPoint = bitmap.AnchorPoint;
        // bool opaque = !bitmap.Flags.HasFlag(BitmapFlags.Transparent);

        Texture2D texture;
        int lastY = bitmap.Height - 1;

        // TODO Make shader so that we don't have to flip textures

        if (SystemInfo.SupportsTextureFormat(TextureFormat.R8)) {
          texture = new Texture2D(bitmap.Width, bitmap.Height, TextureFormat.R8, false, true) {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Repeat
          };

          NativeArray<byte> textureData = texture.GetRawTextureData<byte>();

          for (int y = 0; y < bitmap.Height; ++y)
            NativeArray<byte>.Copy(pixelData, (lastY - y) * bitmap.Width, textureData, y * bitmap.Width, bitmap.Width);

          texture.Apply();
        } else if (SystemInfo.SupportsTextureFormat(TextureFormat.RGBA32)) {
          texture = new Texture2D(bitmap.Width, bitmap.Height, TextureFormat.RGBA32, false, true) {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Repeat
          };

          NativeArray<Color32> textureData = texture.GetRawTextureData<Color32>();

          int pixelIndex = 0;
          for (int y = 0; y < bitmap.Height; ++y) {
            for (int x = 0; x < bitmap.Width; ++x) {
              byte paletteIndex = pixelData[((lastY - y) * bitmap.Width) + x];
              //textureData[pixelIndex++] = new Color32(paletteIndex, paletteIndex, paletteIndex, opaque || paletteIndex != 0 ? (byte)0xFF : (byte)0x00);
              textureData[pixelIndex++] = new Color32(paletteIndex, paletteIndex, paletteIndex, (byte)0xFF);
            }
          }

          texture.Apply();
        } else {
          throw new Exception("No supported TextureFormat found.");
        }
        #endregion

        return new BitmapSet {
          Texture = texture,
          Description = new() {
            Transparent = bitmap.Flags.HasFlag(BitmapFlags.Transparent),
            Size = new Vector2Int(texture.width, texture.height),
            AnchorPoint = new Vector2Int(anchorPoint.x, anchorPoint.y),
            AnchorRect = new RectInt(anchorRect.ul.x, anchorRect.ul.y, anchorRect.lr.x - anchorRect.ul.x, anchorRect.lr.y - anchorRect.ul.y),
            Palette = palette
          }
        };
      }

      private byte[] RunLengthDecode(Bitmap bitmap, BinaryReader msbr) {
        byte[] bitmapData = new byte[bitmap.Width * bitmap.Height];
        using (MemoryStream ms = new(bitmapData)) {
          BinaryWriter msbw = new(ms);

          while (ms.Position < ms.Length) {
            byte cmd = msbr.ReadByte();

            if (cmd == 0x00) { // 00 nn xx      write nn bytes of colour xx
              byte amount = msbr.ReadByte();
              byte data = msbr.ReadByte();

              for (byte i = 0; i < amount; ++i)
                msbw.Write(data);
            } else if (cmd < 0x80) { // 0<nn<0x80	copy nn bytes direct
              for (byte i = 0; i < cmd; ++i)
                msbw.Write(msbr.ReadByte());
            } else if (cmd == 0x80) {
              byte param1 = msbr.ReadByte();
              byte param2 = msbr.ReadByte();

              if (param1 == 0x00 && param2 == 0x00) { // EOF
                break;
              } else if (param2 < 0x80) { // skip (nn*256+mm) bytes
                                          // TODO if video frame, copy from previous frame
                for (int i = 0; i < (param2 * 256 + param1); ++i)
                  msbw.Write((byte)0x00);
              } else if (param2 < 0xC0) { // copy ((nn&0x3f)*256+mm) bytes
                for (int i = 0; i < ((uint)(param2 & 0x3F) * 256 + param1); ++i)
                  msbw.Write(msbr.ReadByte());
              } else if (param2 > 0xC0) { // 0xC0<nn	write ((nn&0x3f)*256+mm) bytes of colour xx
                byte color = msbr.ReadByte();
                for (int i = 0; i < ((uint)(param2 & 0x3F) * 256 + param1); ++i)
                  msbw.Write(color);
              } else {
                throw new Exception($"Unhandled subcommand {param2:2X}");
              }
            } else { // 0x80<nn	skip (nn&0x7f) bytes
                     // TODO if video frame, copy from previous frame
              for (int i = 0; i < (cmd & 0x7F); ++i)
                msbw.Write((byte)0x00);
            }
          }
        }
        return bitmapData;
      }
    }

    IResHandle<BitmapSet> IResProvider<BitmapSet>.Provide(ResourceFile resFile, ResourceInfo resInfo, ushort blockIndex) {
      if (resInfo.info.ContentType != ResourceFile.ContentType.Image)
        throw new Exception($"Resource {resInfo.info.Id:X4}:{blockIndex:X4} is not {nameof(ResourceFile.ContentType.Image)}.");

      return new BitmapLoader(resFile, resInfo, blockIndex);
    }
  }

  public enum BitmapType : byte {
    Uncompressed = 0x02,
    Compressed = 0x04
  }

  [Flags]
  public enum BitmapFlags : ushort {
    Black = 0x0000,
    Transparent = 0x0001
  }

  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct LGPoint {
    public short x;
    public short y;
  }

  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct LGRect {
    public LGPoint ul;
    public LGPoint lr;
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

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    private ushort[] pivot;

    public uint PaletteOffset;

    public readonly LGRect AnchorArea {
      get {
        GCHandle handle = GCHandle.Alloc(pivot, GCHandleType.Pinned);
        try { return Marshal.PtrToStructure<LGRect>(handle.AddrOfPinnedObject()); } finally { handle.Free(); }
      }
    }

    public readonly LGPoint AnchorPoint {
      get {
        GCHandle handle = GCHandle.Alloc(pivot, GCHandleType.Pinned);
        try { return Marshal.PtrToStructure<LGPoint>(handle.AddrOfPinnedObject()); } finally { handle.Free(); }
      }
    }

    public override readonly string ToString() {
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

  public class BitmapSet : IDisposable {
    public Texture2D Texture;
    public BitmapDesc Description;

    public void Dispose() {
      if (Texture != null) UnityEngine.Object.Destroy(Texture);
    }
  }

  public struct BitmapDesc {
    public bool Transparent;
    public Vector2Int Size;
    public Vector2Int AnchorPoint;
    public RectInt AnchorRect;
    public PrivatePalette? Palette;
  }
}
