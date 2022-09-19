using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SS.ObjectProperties;
using SS.System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Rendering;
using UnityEngine.ResourceManagement.AsyncOperations;
using static Unity.Mathematics.math;

namespace SS.Resources {
  public class SpriteLibrary : IDisposable {
    private const ushort ArtResourceIdBase = 1350;

    private readonly ushort[] spriteBase = new ushort[Base.NUM_OBJECT];

    private readonly ushort[] spriteIndices = new ushort[Base.NUM_OBJECT + 500];
    private readonly SpriteMesh[] spriteMeshes = new SpriteMesh[Base.NUM_OBJECT + 500];
    private readonly Material[] spriteMaterials = new Material[Base.NUM_OBJECT + 500];

    private Resources.ObjectProperties objectProperties;

    private Dictionary<string, AsyncOperationHandle<BitmapSet>> bitmapLoaders = new();

    private SpriteLibrary () { }

    public static async Task<SpriteLibrary> ConstructInstance() {
      var instance = new SpriteLibrary();
      await instance.LoadSprites();
      return instance;
    }

    private async Task LoadSprites () {
      objectProperties = await Services.ObjectProperties;
      var clutTexture = await Services.ColorLookupTableTexture;
      var lightmap = await Services.LightmapTexture;

      ushort bitmapIndex = 1;
      ushort artIndex = 1;
      for (var i = 0; i < Base.NUM_OBJECT; ++i) {
        var baseData = objectProperties.BasePropertyData(i);
        var frameCount = baseData.BitmapFrameCount + 1;

        spriteBase[i] = bitmapIndex;

        ++artIndex; // Skip 2D icon

        for (var j = 0; j < frameCount; ++j) {
          var currentBitmapIndex = bitmapIndex;
          var currentArtIndex = artIndex;

          var key = $"{ArtResourceIdBase}:{artIndex++}";
          if (!bitmapLoaders.TryGetValue(key, out var loadOp))
            bitmapLoaders.TryAdd(key, loadOp = Addressables.LoadAssetAsync<BitmapSet>(key));

          loadOp.Completed += op => {
            if (op.Status != AsyncOperationStatus.Succeeded)
              throw op.OperationException;
            
            var bitmapSet = op.Result;

            var material = new Material(Shader.Find("Universal Render Pipeline/System Shock/Lightmap CLUT"));
            material.SetTexture(Shader.PropertyToID(@"_BaseMap"), bitmapSet.Texture);
            material.SetTexture(Shader.PropertyToID(@"_CLUT"), clutTexture);
            material.SetTexture(Shader.PropertyToID(@"_LightGrid"), lightmap);
            material.DisableKeyword(@"_SPECGLOSSMAP");
            material.DisableKeyword(@"_SPECULAR_COLOR");
            material.DisableKeyword(@"_GLOSSINESS_FROM_BASE_ALPHA");
            material.DisableKeyword(@"_ALPHAPREMULTIPLY_ON");

            material.EnableKeyword(@"LINEAR");
            if (bitmapSet.Transparent) {
              material.EnableKeyword(@"TRANSPARENCY_ON");
              material.EnableKeyword(@"_ALPHATEST_ON");
              material.renderQueue = 2450;
            } else {
              material.DisableKeyword(@"TRANSPARENCY_ON");
              material.DisableKeyword(@"_ALPHATEST_ON");
            }

            material.SetFloat(@"_BlendOp", (float)UnityEngine.Rendering.BlendOp.Add);
            material.SetFloat(@"_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat(@"_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
            material.enableInstancing = true;

            spriteMeshes[currentBitmapIndex] = BuildSpriteMesh(bitmapSet);
            spriteMaterials[currentBitmapIndex] = material;
            spriteIndices[currentBitmapIndex] = currentArtIndex;
          };

          ++bitmapIndex;
        }

        ++artIndex; // Skip editor icon
      }
    }

    private SpriteMesh BuildSpriteMesh (BitmapSet bitmapSet) {
      var pivot = bitmapSet.AnchorPoint;
      var texture = bitmapSet.Texture;

      if (pivot.x <= 0 && pivot.y <= 0) {
        pivot.x = texture.width >> 1;
        pivot.y = texture.height - 1;
      }

      Mesh mesh = new Mesh();
      mesh.SetVertexBufferParams(4, 
        new VertexAttributeDescriptor(VertexAttribute.Position),
        new VertexAttributeDescriptor(VertexAttribute.Normal),
        new VertexAttributeDescriptor(VertexAttribute.Tangent),
        new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float16, 2),
        new VertexAttributeDescriptor(VertexAttribute.BlendWeight, VertexAttributeFormat.Float32, 1)
      );
      
      mesh.SetVertexBufferData(new[] {
        new Vertex { pos = float3(-pivot.x, pivot.y, 0f),                                 uv = half2(half(0f), half(1f)), light = 1f },
        new Vertex { pos = float3(-pivot.x, -(texture.height-pivot.y), 0f),               uv = half2(half(0f), half(0f)), light = 0f },
        new Vertex { pos = float3(texture.width-pivot.x, -(texture.height-pivot.y), 0f),  uv = half2(half(1f), half(0f)), light = 0f },
        new Vertex { pos = float3(texture.width-pivot.x, pivot.y, 0f),                    uv = half2(half(1f), half(1f)), light = 1f },
      }, 0, 0, 4);

      mesh.subMeshCount = 1;

      mesh.SetIndexBufferParams(6, IndexFormat.UInt16);
      mesh.SetIndexBufferData(new ushort[] { 0, 1, 2, 2, 3, 0 }, 0, 0, 6);
      mesh.SetSubMesh(0, new SubMeshDescriptor(0, 6, MeshTopology.Triangles));

      mesh.RecalculateNormals();
      // mesh.RecalculateTangents();
      mesh.RecalculateBounds();

      return new SpriteMesh {
        BitmapSet = bitmapSet,
        Mesh = mesh
      };
    }

    public ushort GetSpriteIndex(Triple triple, int frame = 0) {
      var startIndex = spriteBase[objectProperties.BasePropertyIndex(triple)];
      return spriteIndices[startIndex + frame];
    }

    public (SpriteMesh mesh, Material material) GetSprite (Triple triple, int frame = 0) {
      var startIndex = spriteBase[objectProperties.BasePropertyIndex(triple)];

      var spriteMesh = spriteMeshes[startIndex + frame];
      var material = spriteMaterials[startIndex + frame];

      return (spriteMesh, material);
    }

    public void Dispose() {
      
    }
  }

  public class SpriteMesh : IDisposable {
    public BitmapSet BitmapSet;
    public Mesh Mesh;

    public void Dispose() {
      if (BitmapSet != null) BitmapSet.Dispose();
      if (Mesh != null) UnityEngine.Object.Destroy(Mesh);
    }
  }
}
