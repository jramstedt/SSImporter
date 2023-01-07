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
using static SS.TextureUtils;
using UnityEngine.Rendering.Universal;
using SS.Data;

namespace SS.System {
  [CreateAfter(typeof(EntitiesGraphicsSystem))]
  [UpdateInGroup(typeof(VariableRateSimulationSystemGroup))]
  public partial class SpecialMeshSystem : SystemBase {
    public NativeHashMap<ushort, BatchMaterialID>.ReadOnly mapMaterial;

    private EntityQuery newMeshQuery;
    private EntityQuery activeMeshQuery;
    private EntityQuery removedMeshQuery;

    private EntityArchetype viewPartArchetype;

    private NativeArray<BatchMaterialID> materials;

    private NativeArray<VertexAttributeDescriptor> vertexAttributes;
    private RenderMeshDescription renderMeshDescription;

    protected override void OnCreate() {
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

        typeof(LocalTransform),
        typeof(WorldTransform),

        typeof(Parent),
        typeof(ParentTransform),

        typeof(LocalToWorld),
        typeof(RenderBounds)
      );

      var clutTextureOp = Services.ColorLookupTableTexture;
      var lightmapOp = Services.LightmapTexture;

      var entitiesGraphicsSystem = World.GetOrCreateSystemManaged<EntitiesGraphicsSystem>();

      materials = new(15, Allocator.Persistent);

      // TODO FIXME should be accessible from Services.
      for (var i = 0; i < materials.Length; ++i) {
        var materialIndex = i;

        var material = new Material(Shader.Find("Universal Render Pipeline/System Shock/Lightmap CLUT"));
        material.DisableKeyword(ShaderKeywordStrings._ALPHAPREMULTIPLY_ON);
        material.DisableKeyword(ShaderKeywordStrings._SURFACE_TYPE_TRANSPARENT);
        material.DisableKeyword(ShaderKeywordStrings._ALPHAMODULATE_ON);
        material.EnableKeyword(@"LINEAR");
        material.SetFloat(@"_BlendOp", (float)BlendOp.Add);
        material.SetFloat(@"_SrcBlend", (float)BlendMode.One);
        material.SetFloat(@"_DstBlend", (float)BlendMode.Zero);
        material.enableInstancing = true;

        materials[materialIndex] = entitiesGraphicsSystem.RegisterMaterial(material);

        var bitmapSetOp = Addressables.LoadAssetAsync<BitmapSet>($"{CustomTextureIdBase + materialIndex}:{0}");
        var loadOp = Addressables.ResourceManager.CreateGenericGroupOperation(new() { clutTextureOp, lightmapOp, bitmapSetOp });
        loadOp.Completed += op => {
          if (op.Status == AsyncOperationStatus.Succeeded) {
            var bitmapSet = bitmapSetOp.Result;

            material.SetTexture(Shader.PropertyToID(@"_BaseMap"), bitmapSet.Texture);
            material.SetTexture(Shader.PropertyToID(@"_CLUT"), clutTextureOp.Result);
            material.SetTexture(Shader.PropertyToID(@"_LightGrid"), lightmapOp.Result);

            if (bitmapSet.Description.Transparent) material.EnableKeyword(@"TRANSPARENCY_ON");
            else material.DisableKeyword(@"TRANSPARENCY_ON");
          } else {
            Debug.LogError($"{CustomTextureIdBase + materialIndex} failed.");
          }
        };
      }

      this.vertexAttributes = new (5, Allocator.Persistent) {
        [0] = new VertexAttributeDescriptor(VertexAttribute.Position),
        [1] = new VertexAttributeDescriptor(VertexAttribute.Normal),
        [2] = new VertexAttributeDescriptor(VertexAttribute.Tangent),
        [3] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float16, 2),
        [4] = new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 1)
      };

      this.renderMeshDescription = new RenderMeshDescription(
        shadowCastingMode: ShadowCastingMode.Off,
        receiveShadows: false,
        staticShadowCaster: false
      );
    }

    protected override void OnDestroy() {
      base.OnDestroy();

      this.materials.Dispose();
      this.vertexAttributes.Dispose();
    }

    protected override void  OnUpdate() {
      var ecbSystem = World.GetExistingSystemManaged<EndVariableRateSimulationEntityCommandBufferSystem>();
      var commandBuffer = ecbSystem.CreateCommandBuffer();

      int entityCount = newMeshQuery.CalculateEntityCount();
      var meshDataArray = Mesh.AllocateWritableMeshData(entityCount);

      var meshes = new Mesh[entityCount];
      for (var i = 0; i < entityCount; ++i)
        meshes[i] = new Mesh();

      var prototype = EntityManager.CreateEntity(viewPartArchetype); // Sync point
      EntityManager.SetComponentData(prototype, new RenderBounds { Value = new AABB { Center = float3(0f), Extents = float3(0.5f) } });
      RenderMeshUtility.AddComponents(
        prototype,
        EntityManager,
        renderMeshDescription,
        new RenderMeshArray(new Material[0], meshes) // TODO reuse meshes and don't recreate mesharray all the time
      );

      // TODO FIXME improve, parallelize

      var vertexAttributes = this.vertexAttributes;
      var mapMaterial = this.mapMaterial;
      var materials = this.materials;

      Entities
        .WithAll<TexturedCuboid, ObjectInstance>()
        .WithNone<MeshAddedTag>()
        .ForEach((Entity entity, int entityInQueryIndex, in TexturedCuboid texturedCuboid, in ObjectInstance instanceData) => {
          var SideTexture = texturedCuboid.SideTexture;
          var TopBottomTexture = texturedCuboid.TopBottomTexture;

          var meshData = meshDataArray[entityInQueryIndex];

          meshData.subMeshCount = 2;
          meshData.SetVertexBufferParams(4 * 6, vertexAttributes);
          meshData.SetIndexBufferParams(6 * 6, IndexFormat.UInt16);
          
          ReadOnlySpan<ushort> indiceTemplate = stackalloc ushort[] {
            2, 1, 0, 0, 3, 2,
            4, 5, 6, 6, 7, 4,

            8, 9, 10, 10, 11, 8,
            12, 13, 14, 14, 15, 12,
            16, 17, 18, 18, 19, 16,
            22, 21, 20, 20, 23, 22
          };

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
          indiceTemplate.CopyTo(indices.AsSpan());

          meshData.SetSubMesh(0, new SubMeshDescriptor(0, 2 * 6, MeshTopology.Triangles));
          meshData.SetSubMesh(1, new SubMeshDescriptor(2 * 6, 4 * 6, MeshTopology.Triangles));

          // TODO var renderBounds = new RenderBounds { Value = mesh.bounds.ToAABB() };

          #region Sides
          {
            BatchMaterialID material;

            if ((SideTexture & 0x80) == 0x80) {
              material = mapMaterial[(ushort)(SideTexture & 0x7F)];
            } else {
              material = materials[SideTexture & 0x7F];
            }

            var viewPart = commandBuffer.Instantiate(prototype);
            commandBuffer.SetComponent(viewPart, new Parent { Value = entity });
            commandBuffer.SetComponent(viewPart, LocalTransform.FromPosition(0f, -texturedCuboid.Offset, 0f));
            commandBuffer.SetComponent(viewPart, new MaterialMeshInfo {
              Mesh = MaterialMeshInfo.ArrayIndexToStaticIndex(entityInQueryIndex),
              MaterialID = material,
              Submesh = 0
            });
          }
          #endregion

          #region Top and Bottom
          {
            BatchMaterialID material;

            if ((TopBottomTexture & 0x80) == 0x80) {
              material = mapMaterial[(ushort)(TopBottomTexture & 0x7F)];
            } else {
              material = materials[TopBottomTexture & 0x7F];
            }

            var viewPart = commandBuffer.Instantiate(prototype);
            commandBuffer.SetComponent(viewPart, new Parent { Value = entity });
            commandBuffer.SetComponent(viewPart, LocalTransform.FromPosition(0f, -texturedCuboid.Offset, 0f));
            commandBuffer.SetComponent(viewPart, new MaterialMeshInfo {
              Mesh = MaterialMeshInfo.ArrayIndexToStaticIndex(entityInQueryIndex),
              MaterialID = material,
              Submesh = 1
            });
          }
          #endregion

          commandBuffer.AddComponent<MeshAddedTag>(entity);
        })
        .Run();

      Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, meshes);
      for (int i = 0; i < entityCount; ++i) {
          var mesh = meshes[i];
          mesh.RecalculateNormals();
          // mesh.RecalculateTangents();
          mesh.RecalculateBounds();
          mesh.UploadMeshData(true);
      }

      var finalizeCommandBuffer = ecbSystem.CreateCommandBuffer();
      finalizeCommandBuffer.DestroyEntity(prototype);
    }
  }

  public struct SpecialPart : IComponentData { }

  internal struct MeshAddedTag : ICleanupComponentData { }

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
