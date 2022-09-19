using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using SS.Resources;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Rendering;
using UnityEngine.ResourceManagement.AsyncOperations;
using static Unity.Mathematics.math;

namespace SS.System {
  [UpdateInGroup(typeof(VariableRateSimulationSystemGroup))]
  public partial class SpecialMeshSystem : SystemBase {
    private const ushort CustomTextureIdBase = 2180;
    private const ushort SmallTextureIdBase = 321;

    public Dictionary<ushort, Material> mapMaterial;

    private EntityQuery newMeshQuery;
    private EntityQuery activeMeshQuery;
    private EntityQuery removedMeshQuery;

    private EntityArchetype viewPartArchetype;

    private Material[] materials = new Material[64];
    private Material colorMaterial;

    private Texture clutTexture;
    private Texture2D lightmap;

    private NativeArray<VertexAttributeDescriptor> vertexAttributes;

    protected override async void OnCreate() {
      base.OnCreate();

      newMeshQuery = GetEntityQuery(new EntityQueryDesc {
        All = new ComponentType[] { ComponentType.ReadOnly<TexturedCuboid>() },
        None = new ComponentType[] { ComponentType.ReadOnly<MeshAddedTag>() },
      });

      activeMeshQuery = GetEntityQuery(new EntityQueryDesc {
        All = new ComponentType[] {
          ComponentType.ReadOnly<TexturedCuboid>(),
          ComponentType.ReadOnly<LocalToWorld>(),
          ComponentType.ReadOnly<MeshAddedTag>()
        }
      });

      removedMeshQuery = GetEntityQuery(new EntityQueryDesc {
        All = new ComponentType[] { ComponentType.ReadOnly<MeshAddedTag>() },
        None = new ComponentType[] { ComponentType.ReadOnly<TexturedCuboid>() },
      });

      viewPartArchetype = World.EntityManager.CreateArchetype(
        typeof(SpecialPart),
        typeof(Parent),
        typeof(LocalToWorld),
        typeof(LocalToParent),
        typeof(Translation),
        typeof(Rotation),
        typeof(Scale),
        typeof(RenderBounds),
        typeof(RenderMesh)
      );

      clutTexture = await Services.ColorLookupTableTexture;
      lightmap = await Services.LightmapTexture;

      {
        var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        material.SetTexture(Shader.PropertyToID(@"_BaseMap"), clutTexture);
        material.DisableKeyword(@"_SPECGLOSSMAP");
        material.DisableKeyword(@"_SPECULAR_COLOR");
        material.DisableKeyword(@"_GLOSSINESS_FROM_BASE_ALPHA");
        material.DisableKeyword(@"_ALPHAPREMULTIPLY_ON");
        material.EnableKeyword(@"LINEAR");
        material.DisableKeyword(@"TRANSPARENCY_ON");
        material.SetFloat(@"_BlendOp", (float)UnityEngine.Rendering.BlendOp.Add);
        material.SetFloat(@"_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
        material.SetFloat(@"_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
        material.enableInstancing = true;

        colorMaterial = material;
      }

      // TODO FIXME should be accessible from Services.
      for (var i = 0; i < materials.Length; ++i) { // FIXME wrong length
        var materialIndex = i;

        var checkOp = Addressables.LoadResourceLocationsAsync($"{CustomTextureIdBase + materialIndex}:{0}", typeof(BitmapSet));
        checkOp.Completed += op => {
          if (op.Status == AsyncOperationStatus.Succeeded && op.Result.Count > 0) {
            var bitmapSetOp = Addressables.LoadAssetAsync<BitmapSet>(op.Result[0]);
            bitmapSetOp.Completed += op => {
              if (op.Status == AsyncOperationStatus.Succeeded) {
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
                if (bitmapSet.Transparent) material.EnableKeyword(@"TRANSPARENCY_ON");
                else material.DisableKeyword(@"TRANSPARENCY_ON");

                material.SetFloat(@"_BlendOp", (float)UnityEngine.Rendering.BlendOp.Add);
                material.SetFloat(@"_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
                material.SetFloat(@"_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
                material.enableInstancing = true;

                materials[materialIndex] = material;
              } else {
                Debug.LogError($"{CustomTextureIdBase + materialIndex} failed.");
              }
            };
          } else {
            Debug.LogWarning($"{CustomTextureIdBase + materialIndex} not found.");
          }
        };
      }

      this.vertexAttributes = new (5, Allocator.Persistent) {
        [0] = new VertexAttributeDescriptor(VertexAttribute.Position),
        [1] = new VertexAttributeDescriptor(VertexAttribute.Normal),
        [2] = new VertexAttributeDescriptor(VertexAttribute.Tangent),
        [3] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float16, 2),
        [4] = new VertexAttributeDescriptor(VertexAttribute.BlendWeight, VertexAttributeFormat.Float32, 1)
      };
    }

    protected override void OnDestroy() {
      base.OnDestroy();

      this.vertexAttributes.Dispose();
    }

    protected override void  OnUpdate() {
      var ecbSystem = World.GetExistingSystem<EndVariableRateSimulationEntityCommandBufferSystem>();
      var commandBuffer = ecbSystem.CreateCommandBuffer();

      Entities
        .WithAll<TexturedCuboid, ObjectInstance>()
        .WithNone<MeshAddedTag>()
        .ForEach((Entity entity, in TexturedCuboid texturedCuboid, in ObjectInstance instanceData) => {
          var SideTexture = texturedCuboid.SideTexture;
          var TopBottomTexture = texturedCuboid.TopBottomTexture;

          var mesh = new Mesh();
          var meshDataArray = Mesh.AllocateWritableMeshData(1);
          var meshData = meshDataArray[0];

          meshData.subMeshCount = 2;
          meshData.SetVertexBufferParams(4 * 6, vertexAttributes);
          meshData.SetIndexBufferParams(6 * 6, IndexFormat.UInt16);

          ReadOnlySpan<float3> verticeTemplate = stackalloc float3[] {
            // Top
            float3(-texturedCuboid.SizeX, texturedCuboid.SizeZ * 2f, -texturedCuboid.SizeY),
            float3(texturedCuboid.SizeX, texturedCuboid.SizeZ * 2f, -texturedCuboid.SizeY),
            float3(texturedCuboid.SizeX, texturedCuboid.SizeZ * 2f, texturedCuboid.SizeY),
            float3(-texturedCuboid.SizeX, texturedCuboid.SizeZ * 2f, texturedCuboid.SizeY),

            // Bottom
            float3(-texturedCuboid.SizeX, 0f, -texturedCuboid.SizeY),
            float3(texturedCuboid.SizeX, 0f, -texturedCuboid.SizeY),
            float3(texturedCuboid.SizeX, 0f, texturedCuboid.SizeY),
            float3(-texturedCuboid.SizeX, 0f, texturedCuboid.SizeY)
          };

          var vertices = meshData.GetVertexData<Vertex>();

          // +Y
          vertices[0] = new Vertex { pos = verticeTemplate[0], uv = half2(half(0f), half(1f)), light = 0f };
          vertices[1] = new Vertex { pos = verticeTemplate[1], uv = half2(half(1f), half(1f)), light = 0f };
          vertices[2] = new Vertex { pos = verticeTemplate[2], uv = half2(half(1f), half(0f)), light = 0f };
          vertices[3] = new Vertex { pos = verticeTemplate[3], uv = half2(half(0f), half(0f)), light = 0f };

          // -Y
          vertices[4] = new Vertex { pos = verticeTemplate[4], uv = half2(half(0f), half(1f)), light = 0f };
          vertices[5] = new Vertex { pos = verticeTemplate[5], uv = half2(half(1f), half(1f)), light = 0f };
          vertices[6] = new Vertex { pos = verticeTemplate[6], uv = half2(half(1f), half(0f)), light = 0f };
          vertices[7] = new Vertex { pos = verticeTemplate[7], uv = half2(half(0f), half(0f)), light = 0f };

          // +Z
          vertices[8] = new Vertex { pos = verticeTemplate[0], uv = half2(half(0f), half(1f)), light = 0f };
          vertices[9] = new Vertex { pos = verticeTemplate[1], uv = half2(half(1f), half(1f)), light = 0f };
          vertices[10] = new Vertex { pos = verticeTemplate[5], uv = half2(half(1f), half(0f)), light = 0f };
          vertices[11] = new Vertex { pos = verticeTemplate[4], uv = half2(half(0f), half(0f)), light = 0f };

          // -Z
          vertices[12] = new Vertex { pos = verticeTemplate[2], uv = half2(half(0f), half(1f)), light = 0f };
          vertices[13] = new Vertex { pos = verticeTemplate[3], uv = half2(half(1f), half(1f)), light = 0f };
          vertices[14] = new Vertex { pos = verticeTemplate[7], uv = half2(half(1f), half(0f)), light = 0f };
          vertices[15] = new Vertex { pos = verticeTemplate[6], uv = half2(half(0f), half(0f)), light = 0f };

          // +X
          vertices[16] = new Vertex { pos = verticeTemplate[1], uv = half2(half(0f), half(1f)), light = 0f };
          vertices[17] = new Vertex { pos = verticeTemplate[2], uv = half2(half(1f), half(1f)), light = 0f };
          vertices[18] = new Vertex { pos = verticeTemplate[6], uv = half2(half(1f), half(0f)), light = 0f };
          vertices[19] = new Vertex { pos = verticeTemplate[5], uv = half2(half(0f), half(0f)), light = 0f };
            
          // -X
          vertices[20] = new Vertex { pos = verticeTemplate[0], uv = half2(half(1f), half(1f)), light = 0f };
          vertices[21] = new Vertex { pos = verticeTemplate[3], uv = half2(half(0f), half(1f)), light = 0f };
          vertices[22] = new Vertex { pos = verticeTemplate[7], uv = half2(half(0f), half(0f)), light = 0f };
          vertices[23] = new Vertex { pos = verticeTemplate[4], uv = half2(half(1f), half(0f)), light = 0f };

          var indices = meshData.GetIndexData<ushort>();
          
          // TODO is there a better way?
          indices.CopyFrom(new ushort[] {
            2, 1, 0, 0, 3, 2,
            4, 5, 6, 6, 7, 4,

            8, 9, 10, 10, 11, 8,
            12, 13, 14, 14, 15, 12,
            16, 17, 18, 18, 19, 16,
            22, 21, 20, 20, 23, 22
          });

          meshData.SetSubMesh(0, new SubMeshDescriptor(0, 2 * 6, MeshTopology.Triangles));
          meshData.SetSubMesh(1, new SubMeshDescriptor(2 * 6, 4 * 6, MeshTopology.Triangles));

          Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, mesh);

          mesh.RecalculateNormals();
          // mesh.RecalculateTangents();
          mesh.RecalculateBounds();
          mesh.UploadMeshData(true);

          #region Sides
          {
            Material material;

            if ((SideTexture & 0x80) == 0x80) {
              material = mapMaterial[(ushort)(SideTexture & 0x7F)];
            } else {
              material = materials[SideTexture & 0x7F];
            }

            var viewPart = commandBuffer.CreateEntity(viewPartArchetype);
            commandBuffer.SetComponent(viewPart, default(SpecialPart));
            commandBuffer.SetComponent(viewPart, default(LocalToWorld));
            commandBuffer.SetComponent(viewPart, new Parent { Value = entity });
            //commandBuffer.SetComponent(viewPart, new LocalToParent { Value = math.mul(Unity.Mathematics.float4x4.Translate(new float3(0f, -radius, 0f)), Unity.Mathematics.float4x4.Scale(new float3(scale, scale, scale))) });
            commandBuffer.SetComponent(viewPart, default(LocalToParent));
            commandBuffer.SetComponent(viewPart, new Translation { Value = new float3(0f, -texturedCuboid.Offset, 0f) });
            commandBuffer.SetComponent(viewPart, new Rotation { Value = Unity.Mathematics.quaternion.identity });
            commandBuffer.SetComponent(viewPart, new Scale { Value = 1f });
            commandBuffer.SetComponent(viewPart, new RenderBounds { Value = new AABB { Center = mesh.bounds.center, Extents = mesh.bounds.extents } });
            commandBuffer.SetSharedComponent(viewPart, new RenderMesh {
              mesh = mesh,
              material = material,
              subMesh = 0,
              layer = 0,
              castShadows = ShadowCastingMode.On,
              receiveShadows = true,
              needMotionVectorPass = false,
              layerMask = uint.MaxValue
            });
          }
          #endregion

          #region Top and Bottom
          {
            Material material;
            
            if ((TopBottomTexture & 0x80) == 0x80) {
              material = mapMaterial[(ushort)(TopBottomTexture & 0x7F)];
            } else {
              material = materials[TopBottomTexture & 0x7F];
            }

            var viewPart = commandBuffer.CreateEntity(viewPartArchetype);
            commandBuffer.SetComponent(viewPart, default(SpecialPart));
            commandBuffer.SetComponent(viewPart, default(LocalToWorld));
            commandBuffer.SetComponent(viewPart, new Parent { Value = entity });
            //commandBuffer.SetComponent(viewPart, new LocalToParent { Value = math.mul(Unity.Mathematics.float4x4.Translate(new float3(0f, -radius, 0f)), Unity.Mathematics.float4x4.Scale(new float3(scale, scale, scale))) });
            commandBuffer.SetComponent(viewPart, default(LocalToParent));
            commandBuffer.SetComponent(viewPart, new Translation { Value = new float3(0f, -texturedCuboid.Offset, 0f) });
            commandBuffer.SetComponent(viewPart, new Rotation { Value = Unity.Mathematics.quaternion.identity });
            commandBuffer.SetComponent(viewPart, new Scale { Value = 1f });
            commandBuffer.SetComponent(viewPart, new RenderBounds { Value = new AABB { Center = mesh.bounds.center, Extents = mesh.bounds.extents } });
            commandBuffer.SetSharedComponent(viewPart, new RenderMesh {
              mesh = mesh,
              material = material,
              subMesh = 1,
              layer = 0,
              castShadows = ShadowCastingMode.On,
              receiveShadows = true,
              needMotionVectorPass = false,
              layerMask = uint.MaxValue
            });
          }
          #endregion

          commandBuffer.AddComponent<MeshAddedTag>(entity);
        })
        .WithoutBurst()
        .Run();

      ecbSystem.AddJobHandleForProducer(Dependency);
    }
  }

  public struct SpecialPart : IComponentData { }

  internal struct MeshAddedTag : ISystemStateComponentData { }

  public struct TexturedCuboid : IComponentData {
    public float SizeX;
    public float SizeY;
    public float SizeZ;
    public float Offset;
    public byte SideTexture;
    public byte TopBottomTexture;
  }

  public struct TransparentCuboid : IComponentData {
    public float SizeX;
    public float SizeY;
    public float SizeZ;
    public float Offset;
    public uint Color;
  }
}
