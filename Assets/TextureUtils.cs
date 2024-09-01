using System;
using SS.ObjectProperties;
using SS.Resources;
using SS.System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Rendering;

namespace SS {
  [BurstCompile]
  public static class TextureUtils {
    public const ushort CustomTextureIdBase = 2180;
    public const ushort ArtResourceIdBase = 1350;
    public const ushort DoorResourceIdBase = 2400;
    public const ushort ModelTextureIdBase = 475;
    public const ushort SmallTextureIdBase = 321;
    public const ushort RepulsorResourceIdBase = 80;
    public const ushort GraffitiResourceIdBase = 79;
    public const ushort IconResourceIdBase = 78;

    public const int TPOLY_INDEX_BITS = 7;
    public const int INDEX_MASK = 0x007F;
    public const int TPOLY_TYPE_BITS = 2;
    public const int TYPE_MASK = 0x0180;
    public const int SCALE_MASK = 0x0600;
    public const int STYLE_MASK = 0x0800;

    public const byte RANDOM_TEXT_MAGIC_COOKIE = 0x7F;
    public const byte REGULAR_STATIC_MAGIC_COOKIE = 0x77;
    public const byte SHODAN_STATIC_MAGIC_COOKIE = 0x76;

    public const int NUM_HACK_CAMERAS = 8;

    [BurstCompile]
    public static int CalculateTextureData(
      in Entity entity,
      in Base baseProperties,
      in ObjectInstance instanceData,
      in Level level,
      in ComponentLookup<ObjectInstance> instanceLookup,
      in ComponentLookup<ObjectInstance.Decoration> decorationLookup,
      in bool isAnimating
    ) {

      var textureData = 0;

      var isIndirectable = instanceData.Class == ObjectClass.Decoration &&
        (baseProperties.DrawType == DrawType.TexturedPolygon || baseProperties.DrawType == DrawType.TerrainPolygon);

      //(instanceData.Class == ObjectClass.Decoration && baseProperties.DrawType == DrawType.TexturedPolygon) ||
      //instanceData.Triple == 0x70208 || // SUPERSCREEN_TRIPLE
      //instanceData.Triple == 0x70209 || // BIGSCREEN_TRIPLE
      //instanceData.Triple == 0x70206; // SCREEN_TRIPLE

      if (isIndirectable) { // Must be ObjectClass.Decoration
        var decorationData = decorationLookup.GetRefRO(entity).ValueRO;

        var data = decorationData.Data2;

        const int INDIRECTED_STUFF_INDICATOR_MASK = 0x1000;
        const int INDIRECTED_STUFF_DATA_MASK = 0xFFF;

        if (data != 0 || isAnimating) {
          if ((data & INDIRECTED_STUFF_INDICATOR_MASK) != 0) {
            var dataEntity = level.ObjectInstances.Value[(int)data & INDIRECTED_STUFF_DATA_MASK];
            var databObjectInstance = instanceLookup.GetRefRO(dataEntity).ValueRO;
            var dataDecorationInstance = decorationLookup.GetRefRO(dataEntity).ValueRO;

            textureData = (int)dataDecorationInstance.Data2 + databObjectInstance.Info.CurrentFrame;
          } else {
            textureData = (int)data + instanceData.Info.CurrentFrame;
          }
        } else if (instanceData.SubClass == 1 /* BIGSTUFF_SUBCLASS_FURNISHING */) {
          const int SECRET_FURNITURE_DEFAULT_O3DREP = 0x80;
          textureData = SECRET_FURNITURE_DEFAULT_O3DREP;
        }
      } else if (baseProperties.DrawType != DrawType.TerrainPolygon && baseProperties.DrawType != DrawType.TexturedPolygon) {
        textureData = baseProperties.BitmapIndex;

        if (baseProperties.DrawType != DrawType.Voxel && instanceData.Class != ObjectClass.DoorAndGrating && instanceData.Info.CurrentFrame != -1) {
          textureData += instanceData.Info.CurrentFrame;
        }
      }

      return textureData;
    }

    public static bool IsAnimated(ushort objectIndex, in NativeArray<AnimationData>.ReadOnly animationData) {
      for (var index = 0; index < animationData.Length; ++index)
        if (animationData[index].ObjectIndex == objectIndex) return true;

      return false;
    }

    public static BatchMaterialID GetResource(
      in Entity entity,
      in ObjectInstance instanceData,
      in Level level,
      in BlobAssetReference<ObjectDatas> objectProperties,
      in MaterialProviderSystem materialProviderSystem,
      in ComponentLookup<ObjectInstance> instanceLookup,
      in ComponentLookup<ObjectInstance.Decoration> decorationLookup,
      in ComponentLookup<ObjectInstance.DoorAndGrating> doorLookup,
      in bool decal,
      out ushort refWidthOverride
    ) {
      var baseProperties = objectProperties.Value.BasePropertyData(instanceData);

      refWidthOverride = 0;

      if (baseProperties.DrawType == DrawType.TerrainPolygon) {
        const int DESTROYED_SCREEN_ANIM_BASE = 0x1B;

        if (instanceData.Class == ObjectClass.Decoration) {
          var decorationData = decorationLookup.GetRefRO(entity).ValueRO; // TODO FIXME this is also called in CalculateTextureData
          var isAnimating = IsAnimated(decorationData.Link.ObjectIndex, level.Animations.AsReadOnly());
          var textureData = CalculateTextureData(entity, baseProperties, instanceData, level, instanceLookup, decorationLookup, isAnimating);

          if (instanceData.Triple == 0x70207) { // TMAP_TRIPLE
            refWidthOverride = 128;
            return materialProviderSystem.GetMaterial((ushort)(0x03E8 + level.TextureMap[textureData]), 0, true, decal, false);
          } else if (instanceData.Triple == 0x70208) { // SUPERSCREEN_TRIPLE
            var lightmapped = decorationData.Data2 == DESTROYED_SCREEN_ANIM_BASE + 3; // screen is full bright if not destroyed
            refWidthOverride = 128; // 1 << 7
            return materialProviderSystem.ParseTextureData(textureData, lightmapped, decal, out var textureType, out var scale);
          } else if (instanceData.Triple == 0x70209) { // BIGSCREEN_TRIPLE
            var lightmapped = decorationData.Data2 == DESTROYED_SCREEN_ANIM_BASE + 3; // screen is full bright if not destroyed
            refWidthOverride = 64; // 1 << 6
            return materialProviderSystem.ParseTextureData(textureData, lightmapped, decal, out var textureType, out var scale);
          } else if (instanceData.Triple == 0x70206) { // SCREEN_TRIPLE
            var lightmapped = decorationData.Data2 == DESTROYED_SCREEN_ANIM_BASE + 3; // screen is full bright if not destroyed
            refWidthOverride = 32; // 1 << 5
            return materialProviderSystem.ParseTextureData(textureData, lightmapped, decal, out var textureType, out var scale);
          } else {
            var materialID = materialProviderSystem.ParseTextureData(textureData, true, decal, out var textureType, out var scale);
            refWidthOverride = (ushort)(1 << scale);
            return materialID;
          }
        }
      } else if (baseProperties.DrawType == DrawType.FlatTexture) {
        if (instanceData.Class == ObjectClass.Decoration) {
          if (instanceData.Triple == 0x70203) { // WORDS_TRIPLE
            const byte MEDIAN_WORD_SCALE = 4;

            var decorationData = decorationLookup.GetRefRO(entity).ValueRO;
            byte size = (byte)decorationData.WordScale;

            var colorIndex = decorationData.WordColor;
            var style = decorationData.WordStyle;
            var wordIndex = decorationData.WordIndex;

            if (size != 0)
              size -= MEDIAN_WORD_SCALE;

            refWidthOverride = (ushort)(size == 0 ? 128 : (1 << (7 + size))); // 1 << 7 = 128 is default word size

            return materialProviderSystem.GetWordMaterial(wordIndex, colorIndex, style);
          } else if (instanceData.Triple == 0x70201) { // ICON_TRIPLE
            return materialProviderSystem.GetMaterial(IconResourceIdBase, (ushort)instanceData.Info.CurrentFrame, true, decal, false);
          } else if (instanceData.Triple == 0x70202) { // GRAF_TRIPLE
            return materialProviderSystem.GetMaterial(GraffitiResourceIdBase, (ushort)instanceData.Info.CurrentFrame, true, decal, false);
          } else if (instanceData.Triple == 0x7020a) { // REPULSWALL_TRIPLE
            return materialProviderSystem.GetMaterial(RepulsorResourceIdBase, (ushort)instanceData.Info.CurrentFrame, true, decal, false);
          }
        } else if (instanceData.Class == ObjectClass.DoorAndGrating) {
          // Debug.Log($"{DoorResourceIdBase} {objectProperties.ClassPropertyIndex(instanceData)} : {instanceData.Info.CurrentFrame}");
          return materialProviderSystem.GetMaterial((ushort)(DoorResourceIdBase + objectProperties.Value.ClassPropertyIndex(instanceData)), (ushort)instanceData.Info.CurrentFrame, true, decal, false);
        }
      } else if (baseProperties.DrawType == DrawType.TranslucentPolygon) {
        byte colorIndex = 0;

        // TODO ObjectClass.Item

        if (instanceData.Class == ObjectClass.Decoration) {
          var decorationData = decorationLookup.GetRefRO(entity).ValueRO;
          colorIndex = (byte)decorationData.Data2;
        } else if (instanceData.Class == ObjectClass.DoorAndGrating) {
          var doorData = doorLookup.GetRefRO(entity).ValueRO;
          colorIndex = doorData.Color;
        }

        if (colorIndex == 0) colorIndex = 0xFF;

        return materialProviderSystem.GetTranslucentMaterial(colorIndex);
      }

      return BatchMaterialID.Null;
    }
    
    public static TextureSet CreateTexture(string name, int width, int height, bool transparent = false) {
      ushort stride;
      Texture2D texture;
      if (SystemInfo.SupportsTextureFormat(TextureFormat.R8)) {
        texture = new Texture2D(width, height, TextureFormat.R8, false, true);
        stride = (ushort)width;
      } else if (SystemInfo.SupportsTextureFormat(TextureFormat.RGBA32)) {
        texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
        stride = (ushort)(width * 4);
      } else {
        throw new Exception("No supported TextureFormat found.");
      }
      texture.name = name;
      texture.filterMode = FilterMode.Point;
      texture.wrapMode = TextureWrapMode.Clamp;

      TextureSet bitmapSet = new() {
        Texture = texture,
        Description = new() {
          Transparent = transparent,
          Size = new(texture.width, texture.height),
          AnchorPoint = new((short)(texture.width >> 1), (short)(texture.height >> 1)),
          Stride = stride
        }
      };

      return bitmapSet;
    }
    
    public static TextureSet CreateTexture(BitmapSet bitmapSet) {
      var bitmap = bitmapSet.Bitmap;
      var pixelData = bitmapSet.Data;
      
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
        bitmap.Stride = bitmap.Width;
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
            textureData[pixelIndex++] = new Color32(paletteIndex, paletteIndex, paletteIndex, (byte)0xFF);
          }
        }

        texture.Apply();
        bitmap.Stride = (ushort)(bitmap.Width * 4);
      } else {
        throw new Exception("No supported TextureFormat found.");
      }

      PrivatePalette? palette = null;
      unsafe {
        if (bitmapSet.Palette.IsCreated) {
          palette = *(PrivatePalette*)bitmapSet.Palette.GetUnsafePtr();
        }
      }

      bitmap.BitmapType = BitmapType.Device;
      
      return new TextureSet {
        Texture = texture,
        Palette = palette,
        Description = bitmap
      };
    }
    
    public static TextureSet CreateDoubledTexture (BitmapSet bitmapSet) {
      var blendTables = Services.BlendTables.Result; // TODO FIXME

      var srcBitmap = bitmapSet.Bitmap;
      var srcPixelData = bitmapSet.Data;
      
      var dstPixelData = new NativeArray<byte>((srcBitmap.Width << 1) * (srcBitmap.Height << 1), Allocator.Persistent);

      unsafe {
        // Horizontal doubling
        /*
        { // Double pixels
          var src = (byte*)srcPixelData.GetUnsafeReadOnlyPtr();
          var dst = (ushort*)dstPixelData.GetUnsafePtr();
          var srcRowPadding = srcBitmap.Stride - srcBitmap.Width;

          for (int y = 0; y < srcBitmap.Height; ++y) {
            for (int x = 0; x < srcBitmap.Width; ++x) {
              *dst++ = (ushort)((*src << 8) | *src++);
            }

            src += srcRowPadding;
            dst += srcBitmap.Width; // Skip one row. Will be filled later.
          }
        }
        */
        { // Blend pixels
          var src = (byte*)srcPixelData.GetUnsafeReadOnlyPtr();
          var dst = (ushort*)dstPixelData.GetUnsafePtr();
          var srcRowPadding = srcBitmap.Stride - srcBitmap.Width;

          for (int y = 0; y < srcBitmap.Height; ++y) {
            for (int x = 1; x < srcBitmap.Width; ++x) { // x = 1 because reading short would overflow. Last pixel will be done after loop.
              var curpix = *(ushort*)src;
              *dst++ = (ushort)(blendTables[curpix] << 8 | *src++);
            }
            
            *dst++ = (ushort)((*src << 8) | *src++); // Double last pixel

            src += srcRowPadding;
            dst += srcBitmap.Width; // Skip one row. Will be filled later.
          }
        }

        // Vertical doubling
        /*
        { // Double pixels
          var dstBitmapWidth = srcBitmap.Width << 1;

          var src = (byte*)dstPixelData.GetUnsafePtr();
          var dst = src + dstBitmapWidth;

          for (int y = 0; y < srcBitmap.Height; ++y) {
            for (int x = 0; x < dstBitmapWidth; ++x) {
              *dst++ = *src++;
            }

            src += dstBitmapWidth; // skip line
            dst += dstBitmapWidth;
          }
        }
        */
        { // Blend pixels
          var dstBitmapWidth = srcBitmap.Width << 1;
          
          var src = (byte*)dstPixelData.GetUnsafePtr();
          var dst = src + dstBitmapWidth;
          
          for (int y = 1; y < srcBitmap.Height; ++y) { // y = 1 because reading next row after last row would overflow. Last row will be done after loop.
            for (int x = 0; x < dstBitmapWidth; ++x) {
              var top = *src;
              var bottom = *(src++ + (dstBitmapWidth << 1));
              *dst++ = blendTables[(top << 8) | bottom];
            }
            
            src += dstBitmapWidth; // skip line
            dst += dstBitmapWidth;
          }
          
          for (int x = 0; x < dstBitmapWidth; ++x)
            *dst++ = *src++; // Double last row
        }

      }

      var textureSet = CreateTexture(new BitmapSet {
        Bitmap = new Bitmap {
          BitmapType = BitmapType.Device,
          Flags = srcBitmap.Flags,
          Width = (ushort)(srcBitmap.Width << 1),
          Height = (ushort)(srcBitmap.Height << 1),
          Stride = (ushort)(srcBitmap.Stride << 1),
          WidthShift = (byte)(srcBitmap.WidthShift + 1),
          HeightShift = (byte)(srcBitmap.HeightShift + 1),
          AnchorArea = srcBitmap.AnchorArea
        },
        Data = dstPixelData,
        Palette = default
      });

      textureSet.Description.Width >>= 1; // Halve the size to keep render size the same.
      textureSet.Description.Height >>= 1;
      
      return textureSet;
    }
  }

  public enum TextureType {
    Alt, // TPOLY_TYPE_ALT_TMAP
    Custom, // TPOLY_TYPE_CUSTOM_MAT
    Text, // TPOLY_TYPE_TEXT_BITMAP
    ScrollText // TPOLY_TYPE_SCROLL_TEXT
  }
  
  public class TextureSet : IDisposable {
    public Texture2D Texture;
    public Bitmap Description;
    public PrivatePalette? Palette;

    public void Dispose() {
      if (Texture != null) UnityEngine.Object.Destroy(Texture);
    }
  }
}
