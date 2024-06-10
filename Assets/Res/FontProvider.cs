using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities.Serialization;
using static SS.Resources.ResourceFile;

namespace SS.Resources {
  [BurstCompile]

  public class FontProvider : IResProvider<FontSet> {
    [BurstCompile]

    public class FontLoader : LoaderBase<FontSet> {
      public FontLoader(ResourceFile resFile, ResourceInfo resInfo, ushort blockIndex) {
        InvokeCompletionEvent(Load(resFile, resInfo, blockIndex));
      }

      [BurstCompile]

      private unsafe FontSet Load(ResourceFile resFile, ResourceInfo resInfo, ushort blockIndex) {
        var rawResource = resFile.GetResourceData(resInfo, blockIndex);

        MemoryBinaryReader mbr;
        fixed (byte* rawResourcePtr = rawResource) {
          mbr = new MemoryBinaryReader(rawResourcePtr, rawResource.Length);
        }

        var font = mbr.Read<BitmapFont>();

        var charactersCount = font.LastAscii - font.FirstAscii + 1; // inclusive
        var offsetsCount = charactersCount + 1;

        mbr.Position = font.xOffset;
        
        var offsets = new NativeArray<ushort>(offsetsCount, Allocator.Persistent);
        mbr.ReadArray(offsets, offsets.Length);

        var data = new NativeArray<byte>(font.RowBytes * font.Rows, Allocator.Persistent);
        mbr.ReadBytes(data, data.Length);
        
        mbr.Dispose();
        
        return new FontSet() {
          Font = font,
          Offsets = offsets,
          Data = data
        };
      }
    }

    IResHandle<FontSet> IResProvider<FontSet>.Provide(ResourceFile resFile, ResourceInfo resInfo, ushort blockIndex) {
      if (resInfo.info.ContentType != ResourceFile.ContentType.Font)
        throw new Exception($"Resource {resInfo.info.Id:X4}:{blockIndex:X4} is not {nameof(ResourceFile.ContentType.Font)}.");

      return new FontLoader(resFile, resInfo, blockIndex);
    }
  }

  public struct FontSet: IDisposable {
    public BitmapFont Font;
    public NativeArray<ushort> Offsets;
    public NativeArray<byte> Data;
    
    public void Dispose() {
      Offsets.Dispose();
      Data.Dispose();
    }
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
