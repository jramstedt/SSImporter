using System;
using System.IO;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using static SS.Resources.ResourceFile;

namespace SS.Resources {
  public class FontProvider : IResProvider<FontSet> {
    public class FontLoader : LoaderBase<FontSet> {
      public FontLoader(ResourceFile resFile, ResourceInfo resInfo, ushort blockIndex) {
        InvokeCompletionEvent(Load(resFile, resInfo, blockIndex));
      }

      private FontSet Load(ResourceFile resFile, ResourceInfo resInfo, ushort blockIndex) {
        byte[] rawResource = resFile.GetResourceData(resInfo, blockIndex);

        const int PADDING = 1;

        using MemoryStream ms = new(rawResource);
        using BinaryReader msbr = new(ms);

        BitmapFont font = msbr.Read<BitmapFont>();

        int charactersCount = font.LastAscii - font.FirstAscii + 1; // inclusive
        int offsetsCount = charactersCount + 1;

        ms.Position = font.xOffset;

        ushort[] offsets = new ushort[offsetsCount];
        for (var i = 0; i < offsets.Length; ++i)
          offsets[i] = msbr.ReadUInt16();

        var fontTextureWidth = offsets[^1] + PADDING * offsetsCount; // +1 pixel padding on all sides and between characters
        var fontTextureHeight = font.Rows + PADDING * 2;             //

        RectInt[] characterRects = new RectInt[charactersCount];
        CharacterInfo[] characterInfo = new CharacterInfo[charactersCount];
        #region Build character info and rects
        for (int c = 0; c < charactersCount; ++c) {
          var x = offsets[c];
          var width = offsets[c + 1] - x;
          var height = font.Rows;

          var rect = characterRects[c] = new(
            PADDING + x + c * PADDING,
            PADDING,
            width,
            height
          );

          characterInfo[c] = new() {
            index = font.FirstAscii + c,
            size = font.Rows,
            style = FontStyle.Normal,

            advance = width + 1,
            bearing = 0,
            glyphHeight = height,
            glyphWidth = width,

            uvTopLeft = new Vector2(rect.xMin / (float)fontTextureWidth, rect.yMin / (float)fontTextureHeight),
            uvTopRight = new Vector2(rect.xMax / (float)fontTextureWidth, rect.yMin / (float)fontTextureHeight),
            uvBottomLeft = new Vector2(rect.xMin / (float)fontTextureWidth, rect.yMax / (float)fontTextureHeight),
            uvBottomRight = new Vector2(rect.xMax / (float)fontTextureWidth, rect.yMax / (float)fontTextureHeight),
          };
        }
        #endregion

        Texture2D fontTexture;
        #region Create texture and copy pixels
        if (font.DataType == BitmapFont.BitmapDataType.Mono && SystemInfo.SupportsTextureFormat(TextureFormat.Alpha8)) {
          fontTexture = new Texture2D(fontTextureWidth, fontTextureHeight, TextureFormat.Alpha8, false, true);

          var textureData = fontTexture.GetRawTextureData<byte>();
          unsafe {
            UnsafeUtility.MemClear(textureData.GetUnsafePtr(), textureData.Length);

            var lastY = font.Rows - 1;
            for (int c = 0; c < charactersCount; ++c) {
              var rect = characterRects[c];
              var offset = offsets[c];
              var width = offsets[c + 1] - offset;

              for (int y = 0; y < font.Rows; ++y) {
                var lineStart = font.bitsOffset + (offset >> 3) + (lastY - y) * font.RowBytes;
                for (int x = 0; x < width; ++x) {
                  byte stub = rawResource[(x >> 3) + lineStart];
                  textureData[rect.x + x + ((rect.y + y) * fontTextureWidth)] = ((0x80 >> (x & 7)) & stub) == 0 ? (byte)0x00 : (byte)0x01;
                }
              }
            }
          }
        } else if (font.DataType == BitmapFont.BitmapDataType.Color && SystemInfo.SupportsTextureFormat(TextureFormat.R8)) {
          fontTexture = new Texture2D(fontTextureWidth, fontTextureHeight, TextureFormat.R8, false, true);

          var textureData = fontTexture.GetRawTextureData<byte>();
          unsafe {
            UnsafeUtility.MemClear(textureData.GetUnsafePtr(), textureData.Length);

            var lastY = font.Rows - 1;
            for (int c = 0; c < charactersCount; ++c) {
              var rect = characterRects[c];
              var offset = offsets[c];
              var width = offsets[c + 1] - offset;
              for (int y = 0; y < font.Rows; ++y)
                NativeArray<byte>.Copy(rawResource, (int)(font.bitsOffset + offset + ((lastY - y) * font.RowBytes)), textureData, rect.x + ((rect.y + y) * fontTextureWidth), width);
            }
          }
        } else if (SystemInfo.SupportsTextureFormat(TextureFormat.RGBA32)) {
          fontTexture = new Texture2D(fontTextureWidth, fontTextureHeight, TextureFormat.RGBA32, false, true);

          var textureData = fontTexture.GetRawTextureData<byte>();
          var stride = UnsafeUtility.SizeOf<Color32>();

          unsafe {
            var srcPtr = (byte*)UnsafeUtility.PinGCArrayAndGetDataAddress(rawResource, out ulong gcHandle);
            var dstPtr = (byte*)textureData.GetUnsafePtr();
            UnsafeUtility.MemClear(dstPtr, textureData.Length);

            var lastY = font.Rows - 1;
            for (int c = 0; c < charactersCount; ++c) {
              var rect = characterRects[c];
              var offset = offsets[c];
              var width = offsets[c + 1] - offset;

              srcPtr += font.bitsOffset;
              dstPtr += (rect.x + rect.y * fontTextureWidth) * stride;

              if (font.DataType == BitmapFont.BitmapDataType.Color) {
                srcPtr += offset;
                for (int y = 0; y < font.Rows; ++y) {
                  UnsafeUtility.MemCpyStride(dstPtr, stride, srcPtr + ((lastY - y) * font.RowBytes), 1, 1, width);
                  dstPtr += fontTextureWidth * stride;
                }
              } else if (font.DataType == BitmapFont.BitmapDataType.Mono) {
                srcPtr += offset >> 3;
                for (int y = 0; y < font.Rows; ++y) {
                  var lineStart = (lastY - y) * font.RowBytes;
                  for (int x = 0; x < width; ++x) {
                    byte stub = *(srcPtr + (x >> 3) + lineStart);
                    *(dstPtr + x) = ((0x80 >> (x & 7)) & stub) == 0 ? (byte)0x00 : (byte)0x01;
                  }

                  dstPtr += fontTextureWidth * stride;
                }
              }

              UnsafeUtility.ReleaseGCObject(gcHandle);
            }
          }
        } else {
          throw new Exception("No supported TextureFormat found.");
        }

        fontTexture.wrapMode = TextureWrapMode.Clamp;
        fontTexture.filterMode = FilterMode.Point;
        #endregion

        return new FontSet() {
          Texture = fontTexture,
          CharacterInfo = characterInfo
        };
      }
    }

    IResHandle<FontSet> IResProvider<FontSet>.Provide(ResourceFile resFile, ResourceInfo resInfo, ushort blockIndex) {
      if (resInfo.info.ContentType != ResourceFile.ContentType.Font)
        throw new Exception($"Resource {resInfo.info.Id:X4}:{blockIndex:X4} is not {nameof(ResourceFile.ContentType.Font)}.");

      return new FontLoader(resFile, resInfo, blockIndex);
    }
  }

  public class FontSet : IDisposable {
    public Texture2D Texture;
    public CharacterInfo[] CharacterInfo;

    public void Dispose() {
      if (Texture != null) UnityEngine.Object.Destroy(Texture);
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
  }
}
