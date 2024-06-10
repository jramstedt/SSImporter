using SS.Resources;
using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace SS {
  public static class GraphicUtils {
    private const char SOFTCR = (char)1;
    private const char SOFTSP = (char)2;

    public static void DrawString(ref NativeArray<byte> textureData, TextureFormat format, in int2 size, in FontSet fontSet, string fullText, in int2 origin, byte colorIndex = Palette.RED_8_BASE + 3) {
      // TODO Almost burst compatible. Need to get rid of string.
      
      var font = fontSet.Font;
      var offsets = fontSet.Offsets;
      var bits = fontSet.Data;
      
      var bytesShift = format switch { // Log2 of number of bytes per pixel in canvas.
        TextureFormat.R8 => 0,
        TextureFormat.RGBA32 => 2,
        _ => throw new NotImplementedException()
      };

      int2 chrPos = new(origin);
      for (int index = 0; index < fullText.Length; index++) {
        char chr = fullText[index];

        if (chr == '\n' || chr == SOFTCR) {
          chrPos.x = origin.x;
          chrPos.y += font.Rows;
          continue;
        }

        if (chr > font.LastAscii || chr < font.FirstAscii || chr == SOFTSP)
          continue;

        if ((chrPos.y + font.Rows) < 0 || chrPos.y >= size.y) // out of view
          continue;

        int xOffset = offsets[chr - font.FirstAscii];
        var width = offsets[chr - font.FirstAscii + 1] - xOffset;

        var yOffset = 0;
        var height = (int)font.Rows;

        if ((chrPos.x + width) < 0 || chrPos.x >= size.x) { // out of view
          chrPos.x += width;
          continue;
        }

        if (chrPos.x < 0) { // clip left
          var extra = 0 - chrPos.x;
          width -= extra;
          xOffset += extra;
          chrPos.x = 0;
        }

        if ((chrPos.x + width) > size.x) { // clip right
          width -= chrPos.x + width - size.x;
        }

        if (chrPos.y < 0) { // clip top
          var extra = 0 - chrPos.y;
          height -= extra;
          yOffset += extra;
          chrPos.y = 0;
        }

        if ((chrPos.y + height) > size.y) { // clip bottom
          height -= chrPos.y + height - size.y;
        }

        var lastRow = size.y - 1;
        if (font.DataType == BitmapFont.BitmapDataType.Mono) {
          var texRow = (lastRow - chrPos.y) * size.x + chrPos.x;
          for (int y = 0; y < height; ++y) {
            var bitRow = (y + yOffset) * font.RowBytes + (xOffset >> 3);
            var bit = xOffset & 7;

            for (int x = 0; x < width; ++x, ++bit) {
              byte bitmask = (byte)(0x80 >> (bit & 7));
              if ((bits[bitRow + (bit >> 3)] & bitmask) != 0)
                textureData[(texRow + x) << bytesShift] = colorIndex;
            }

            texRow -= size.x;
          }
        } else {
          var texRow = (lastRow - chrPos.y) * size.x + chrPos.x;
          for (int y = 0; y < height; ++y) {
            var bitRow = (y + yOffset) * font.RowBytes + xOffset;

            for (int x = 0; x < width; ++x) {
              var color = bits[bitRow + x];
              if (color != 0)
                textureData[(texRow + x) << bytesShift] = color;
            }

            texRow -= size.x;
          }
        }

        chrPos.x += width;
      }
    }

    public static int2 MeasureString(in FontSet fontSet, string fullText) {
      // TODO Almost burst compatible. Need to get rid of string.
      
      var font = fontSet.Font;
      var offsets = fontSet.Offsets;

      var widestLine = 0;
      var lineWide = 0;
      var height = font.Rows;

      for (int index = 0; index < fullText.Length; index++) {
        char chr = fullText[index];

        if (chr == '\n' || chr == SOFTCR) {
          if (lineWide > widestLine) widestLine = lineWide;
          lineWide = 0;
          height += font.Rows;
          continue;
        }

        if (chr > font.LastAscii || chr < font.FirstAscii || chr == SOFTSP)
          continue;

        int xOffset = offsets[chr - font.FirstAscii];
        lineWide += offsets[chr - font.FirstAscii + 1] - xOffset;
      }

      return new int2(
        lineWide > widestLine ? lineWide : widestLine,
        height
      );
    }

    public static (TextTargetSettings settings, byte color, ushort fontRes) GetTextProperties(TextType type, byte colorIndex, byte style) {
      TextTargetSettings settings = type switch {
        TextType.Word => new() { Width = 128, Height = 32, Transparent = true, ResId = 0x868 /* RES_words */ },
        TextType.Screen => new() { Width = 64, Height = 64, Transparent = false, ResId = 0x877 /* RES_screenText */ },
        _ => throw new global::System.NotImplementedException(),
      };

      ushort fontRes = style switch {
        1 => 0x261, // RES_graffitiFont,
        2 => 0x25a, // RES_smallTechFont,
        3 => 0x25d, // RES_largeTechFont,
        _ => 0x25e, // RES_citadelFont
      };

      if (colorIndex == 0)
        colorIndex = Palette.RED_8_BASE + 3;

      return (settings, colorIndex, fontRes);
    }
  }

  public enum TextType {
    Word = 0,
    Screen = 1
  }

  public struct TextTargetSettings {
    public byte Width;
    public byte Height;
    public bool Transparent;
    public ushort ResId;
  }
}
