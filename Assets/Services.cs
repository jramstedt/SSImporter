using SS.Resources;
using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace SS {
  public static class Services {
    public static readonly IResHandle<Palette> Palette;
    public static readonly IResHandle<ShadeTableData> ShadeTable;
    public static readonly IResHandle<Texture2D> ColorLookupTableTexture;
    public static readonly IResHandle<Texture2D> LightmapTexture;
    public static readonly IResHandle<TexturePropertiesData> TextureProperties;
    public static readonly IResHandle<Resources.ObjectProperties> ObjectProperties;
    public static readonly IResHandle<NativeArray<byte>> BlendTables;
    public static readonly IResHandle<NativeArray<byte>> InversePalette;

    static Services() {
      Debug.Log(@"Services");

      Palette = Res.Load<Palette>(0x02BC);

      ShadeTable = Res.Open<ShadeTableData>(Res.dataPath + @"\SHADTABL.DAT");

      ColorLookupTableTexture = new CreateColorLookupTable();
      LightmapTexture = new CreateLightmap();

      TextureProperties = Res.Open<TexturePropertiesData>(Res.dataPath + @"\TEXTPROP.DAT");

      ObjectProperties = Res.OpenObjectProperties(Res.dataPath + @"\OBJPROP.DAT");

      InversePalette = new CreateInversePalette();
      BlendTables = new CreateBlendTable(1);
    }

    private class CreateColorLookupTable : LoaderBase<Texture2D> {
      public CreateColorLookupTable() {
        CreateColorLookupTableAsync();
      }

      private async void CreateColorLookupTableAsync() {
        var palette = await Palette;
        var shadeTable = await ShadeTable;

        Texture2D colorLookupTable = new(256, 16, TextureFormat.RGBA32, false, false) {
          name = @"Color lookup table",
          filterMode = FilterMode.Point,
          wrapMode = TextureWrapMode.Clamp
        };

        var textureData = colorLookupTable.GetRawTextureData<Color32>();

        for (var i = 0; i < textureData.Length; ++i)
          textureData[i] = palette[shadeTable[i]];

        colorLookupTable.Apply(false, false);

        Shader.SetGlobalTexture(Shader.PropertyToID(@"_CLUT"), colorLookupTable);

        InvokeCompletionEvent(colorLookupTable);
      }
    }

    private class CreateLightmap : LoaderBase<Texture2D> {
      public CreateLightmap() {
        Texture2D lightmap;
        if (SystemInfo.SupportsTextureFormat(TextureFormat.RG16))
          lightmap = new(64, 64, TextureFormat.RG16, false, true);
        else if (SystemInfo.SupportsTextureFormat(TextureFormat.RGBA32))
          lightmap = new(64, 64, TextureFormat.RGBA32, false, true);
        else
          throw new Exception("No supported TextureFormat found.");

        lightmap.name = @"Lightmap";

        Shader.SetGlobalTexture(Shader.PropertyToID(@"_LightGrid"), lightmap);

        InvokeCompletionEvent(lightmap);
      }
    }

    private class CreateBlendTable : LoaderBase<NativeArray<byte>>, IDisposable {
      private const int TABLE_RES_LOG = 8;
      private const int TABLE_RES = 1 << TABLE_RES_LOG;
      private const int TABLE_SIZE = TABLE_RES * TABLE_RES;
      
      private NativeArray<byte> tables = default;
      
      public CreateBlendTable(byte logBlendLevels) {
        BuildTables(logBlendLevels);
      }

      private async void BuildTables(byte logBlendLevels) {
        var palette = await Palette;
        var inversePalette = await InversePalette;
        
        if (logBlendLevels > 0) {
          var factor = TABLE_RES >> logBlendLevels;
          var tableCount = (1 << logBlendLevels) - 1;

          tables = new NativeArray<byte>(tableCount * TABLE_SIZE, Allocator.Persistent);
          for (var i = 0; i < tableCount; ++i) 
            BuildTable(tables.GetSubArray(i * TABLE_SIZE, TABLE_SIZE), (byte)(factor + factor * i), palette, inversePalette);
        } 
        
        InvokeCompletionEvent(tables);
      }

      private static void BuildTable(NativeArray<byte> table, byte fraction, Palette palette, NativeArray<byte> inversePalette) {
        var fractionRemaining = TABLE_RES - fraction;
        
        unsafe {
          var dst = (byte*)table.GetUnsafePtr();
        
          for (var i = 0; i < TABLE_RES; ++i) {
            var first = palette[i];
          
            for (var j = 0; j < TABLE_RES; ++j) {
              if (i == 0 || i == j || first.r + first.g + first.b == 0) {
                *dst++ = (byte)i;
                continue;
              }

              var second = palette[j];
              if (j == 0 || second.r + second.g + second.b == 0) {
                *dst++ = (byte)j;
                continue;
              }

              int offs = 0;
              for (var k = 0; k < 3; ++k) {
                var blended = first[k] * fractionRemaining + second[k] * fraction >> 8; // fixed point: 32.0 * 0.8 >> 8 = 24.0
                offs = (offs << 5) | (blended >> 3); // 5 bits per color.
              }

              *dst++ = inversePalette[offs];
            }
          }
        }
      }

      public void Dispose() {
        tables.Dispose();
      }
    }

    private class CreateInversePalette : LoaderBase<NativeArray<byte>>, IDisposable {
      private const int BITS_PER_COLOR = 5;
      private const int BITS_FREE_PER_COLOR = 8 - BITS_PER_COLOR;
      private const int MAX_COLOR = 1 << BITS_PER_COLOR;
      private const float INTERVAL = byte.MaxValue / (float)(MAX_COLOR - 1);

      private const int RED_STRIDE = MAX_COLOR * MAX_COLOR;
      private const int GREEN_STRIDE = MAX_COLOR;
      private const int BLUE_STRIDE = 1;
      
      private NativeArray<byte> ipal = default;

      public CreateInversePalette () {
        ipal = new NativeArray<byte>(MAX_COLOR * MAX_COLOR * MAX_COLOR, Allocator.Persistent);
        
        BuildTable();
      }

      private async void BuildTable() {
        var palette = await Palette;
        var shadeTable = await ShadeTable;

        var colors = palette.ToNativeArray(Allocator.Temp);
        var distances = new NativeArray<uint>(MAX_COLOR * MAX_COLOR * MAX_COLOR, Allocator.Temp);

        unsafe {
          var value = stackalloc uint[1] { uint.MaxValue };
          UnsafeUtility.MemCpyReplicate(distances.GetUnsafePtr(), value, UnsafeUtility.SizeOf<uint>(), MAX_COLOR * MAX_COLOR * MAX_COLOR);
          UnsafeUtility.MemSet(ipal.GetUnsafePtr(), 0x00, colors.Length);
        }
        
        invMap(colors, distances, shadeTable);
        
        InvokeCompletionEvent(ipal);
      }

      private unsafe void invMap(in NativeArray<Color32> colors, in NativeArray<uint> distances, in ShadeTableData shadeTable) {
        for (var colorIndex = 0; colorIndex < colors.Length; ++colorIndex) {
          // for (var colorIndex = colors.Length - 1; colorIndex >= 0; --colorIndex) {
          var color = colors[colorIndex];
          
          //uint3 quantized = new uint3(math.round(math.trunc(new float3(color.r, color.g, color.b) / INTERVAL + .5f) * INTERVAL)) >> BITS_FREE_PER_COLOR;
          int3 quantized = new int3(color.r, color.g, color.b) >> BITS_FREE_PER_COLOR;

          var distancesPtr = (uint*)distances.GetUnsafePtr() + quantized.x * RED_STRIDE + quantized.y * GREEN_STRIDE + quantized.z * BLUE_STRIDE;
          var ipalPtr = (byte*)ipal.GetUnsafePtr() + quantized.x * RED_STRIDE + quantized.y * GREEN_STRIDE + quantized.z * BLUE_STRIDE;
          
          // Add center color
          *(ipalPtr) = (byte)colorIndex;
          *(distancesPtr) = 0;
          
          // Skip palette effects
          /*
          if (colorIndex >= 0x03 && colorIndex <= 0x07) continue;
          if (colorIndex >= 0x0B && colorIndex <= 0x0F) continue;
          if (colorIndex >= 0x10 && colorIndex <= 0x14) continue;
          if (colorIndex >= 0x15 && colorIndex <= 0x17) continue;
          if (colorIndex >= 0x18 && colorIndex <= 0x1A) continue;
          if (colorIndex >= 0x1B && colorIndex <= 0x1F) continue;
          */
          if (shadeTable[colorIndex] == shadeTable[colorIndex + 256*15]) continue; // Dont expand bright colors
          
          redloop((byte)colorIndex, distancesPtr, ipalPtr, quantized); // Expand color
        }
      }

      private unsafe void redloop(in byte colorIndex, in uint* distancesPtr, in byte* ipalPtr, in int3 center) {
        uint3 extension = new ();
        int3 min = new(0 - center.x, 0 - center.y, 0 - center.z);
        int3 max = new(MAX_COLOR - center.x, MAX_COLOR - center.y, MAX_COLOR - center.z);
        
        for (uint ext = 1; ext < MAX_COLOR; ++ext) {
          extension.x = ext;

          if (extension.x < max.x)
            greenloop(colorIndex, distancesPtr + extension.x * RED_STRIDE, ipalPtr + extension.x * RED_STRIDE, ref extension, ref min, ref max);

          if (-extension.x >= min.x)
            greenloop(colorIndex, distancesPtr - extension.x * RED_STRIDE, ipalPtr - extension.x * RED_STRIDE, ref extension, ref min, ref max);
        }
      }

      private unsafe void greenloop(in byte colorIndex, in uint* distancesPtr, in byte* ipalPtr, ref uint3 extension, ref int3 min, ref int3 max) {
        for (uint ext = 1; ext < MAX_COLOR; ++ext) {
          extension.y = ext;
          
          if (extension.y < max.y)
            blueloop(colorIndex, distancesPtr + extension.y * GREEN_STRIDE, ipalPtr + extension.y * GREEN_STRIDE, ref extension, ref min, ref max);

          if (-extension.y >= min.y)
            blueloop(colorIndex, distancesPtr - extension.y * GREEN_STRIDE, ipalPtr - extension.y * GREEN_STRIDE, ref extension, ref min, ref max);
        }
      }

      private unsafe void blueloop(in byte colorIndex, in uint* distancesPtr, in byte* ipalPtr, ref uint3 extension, ref int3 min, ref int3 max) {
        for (uint ext = 1; ext < MAX_COLOR; ++ext) {
          extension.z = ext;
          
          var distance = math.dot(extension, extension);

          if (extension.z < max.z) {
            if (*(distancesPtr + extension.z * BLUE_STRIDE) > distance) {
              *(ipalPtr + extension.z * BLUE_STRIDE) = colorIndex;
              *(distancesPtr + extension.z * BLUE_STRIDE) = distance;
            }
          }
          
          if (-extension.z >= min.z) {
            if (*(distancesPtr - extension.z * BLUE_STRIDE) > distance) {
              *(ipalPtr - extension.z * BLUE_STRIDE) = colorIndex;
              *(distancesPtr - extension.z * BLUE_STRIDE) = distance;
            }
          }
        }
      }

      public void Dispose() {
        ipal.Dispose();
      }
    }

    /*
     * TODO Caching and refcounting Res.Load
     *
    private static Texture2D ColorLookupTable;
    private static Texture2D Lightmap;

    public static IResHandle<Palette> Palette => Res.Load<Palette>(0x02BC);
    public static IResHandle<ShadeTableData> ShadeTable => Res.Open<ShadeTableData>(Res.dataPath + @"\SHADTABL.DAT");
    public static IResHandle<Texture2D> ColorLookupTableTexture => CreateColorLookupTable();
    public static IResHandle<Texture2D> LightmapTexture => CreateLightmap();
    public static IResHandle<TexturePropertiesData> TextureProperties => Res.Open<TexturePropertiesData>(Res.dataPath + @"\TEXTPROP.DAT");
    public static IResHandle<Resources.ObjectProperties> ObjectProperties => Res.OpenObjectProperties(Res.dataPath + @"\OBJPROP.DAT");
    */
  }
}
