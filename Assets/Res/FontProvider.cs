using System;
using System.IO;
using System.Runtime.InteropServices;
using static SS.Resources.ResourceFile;

namespace SS.Resources {
  public class FontProvider : IResProvider<FontSet> {
    public class FontLoader : LoaderBase<FontSet> {
      public FontLoader(ResourceFile resFile, ResourceInfo resInfo, ushort blockIndex) {
        InvokeCompletionEvent(Load(resFile, resInfo, blockIndex));
      }

      private FontSet Load(ResourceFile resFile, ResourceInfo resInfo, ushort blockIndex) {
        byte[] rawResource = resFile.GetResourceData(resInfo, blockIndex);

        using MemoryStream ms = new(rawResource);
        using BinaryReader msbr = new(ms);

        BitmapFont font = msbr.Read<BitmapFont>();

        int charactersCount = font.LastAscii - font.FirstAscii + 1; // inclusive
        int offsetsCount = charactersCount + 1;

        ms.Position = font.xOffset;

        ushort[] offsets = new ushort[offsetsCount];
        for (var i = 0; i < offsets.Length; ++i)
          offsets[i] = msbr.ReadUInt16();

        return new FontSet() {
          Font = font,
          Offsets = offsets,
          Data = msbr.ReadBytes(font.RowBytes * font.Rows)
        };
      }
    }

    IResHandle<FontSet> IResProvider<FontSet>.Provide(ResourceFile resFile, ResourceInfo resInfo, ushort blockIndex) {
      if (resInfo.info.ContentType != ResourceFile.ContentType.Font)
        throw new Exception($"Resource {resInfo.info.Id:X4}:{blockIndex:X4} is not {nameof(ResourceFile.ContentType.Font)}.");

      return new FontLoader(resFile, resInfo, blockIndex);
    }
  }

  public struct FontSet {
    public BitmapFont Font;
    public ushort[] Offsets;
    public byte[] Data;
  }

  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct BitmapFont {
    public enum BitmapDataType : ushort {
      Mono = 0x0000,
      Color = 0xCCCC
    }

    public BitmapDataType DataType;

    private unsafe fixed byte Dummy1[34];

    public ushort FirstAscii;
    public ushort LastAscii;

    private unsafe fixed byte Dummy2[32];

    public uint xOffset;
    public uint bitsOffset;

    public ushort RowBytes;
    public ushort Rows;

    /* Resource continues with
     * ushort[LastAscii - FirstAscii + 1] offsets
     * byte[RowBytes * Rows] data
     */
  }
}
