using SS.Data;
using SS.Resources;
using System;
using System.Collections.Concurrent;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static SS.TextureUtils;
using static Unity.Mathematics.math;

namespace SS.System {
  [CreateAfter(typeof(EntitiesGraphicsSystem))]
  [UpdateInGroup(typeof(VariableRateSimulationSystemGroup))]
  public partial class SpecialMeshSystem : SystemBase {
    private EntityQuery newMeshQuery;
    private EntityQuery activeMeshQuery;
    private EntityQuery removedMeshQuery;

    private EntityArchetype viewPartArchetype;

    private NativeArray<BatchMaterialID> materials;
    private readonly ConcurrentDictionary<Entity, Mesh> entityMeshes = new();
    private readonly NativeHashMap<Entity, BatchMeshID> entityMeshIDs = new(ObjectConstants.NUM_OBJECTS, Allocator.Persistent);

    private NativeArray<VertexAttributeDescriptor> vertexAttributes;
    private RenderMeshDescription renderMeshDescription;

    private MaterialProviderSystem materialProviderSystem;

    protected override void OnCreate() {
      base.OnCreate();

      RequireForUpdate<Level>();

      activeMeshQuery = new EntityQueryBuilder(Allocator.Temp)
        .WithAll<TexturedCuboid, LocalToWorld, MeshCachedTag>()
        .Build(this);

      removedMeshQuery = new EntityQueryBuilder(Allocator.Temp)
        .WithAll<MeshCachedTag>()
        .WithNone<TexturedCuboid>()
        .Build(this);

      viewPartArchetype = World.EntityManager.CreateArchetype(
        typeof(SpecialPart),

        typeof(LocalTransform),
        typeof(Parent),

        typeof(LocalToWorld),
        typeof(RenderBounds)
      );

      var entitiesGraphicsSystem = World.GetOrCreateSystemManaged<EntitiesGraphicsSystem>();

      materials = new(15, Allocator.Persistent);

      // TODO FIXME should be accessible from Services.
      for (var materialIndex = 0; materialIndex < materials.Length; ++materialIndex) {
        var material = new Material(Shader.Find("Shader Graphs/URP CLUT"));
        material.EnableKeyword(@"_LIGHTGRID");

        materials[materialIndex] = entitiesGraphicsSystem.RegisterMaterial(material);

        LoadBitmapToMaterial(materialIndex, material);
      }

      vertexAttributes = new(5, Allocator.Persistent) {
        [0] = new VertexAttributeDescriptor(VertexAttribute.Position),
        [1] = new VertexAttributeDescriptor(VertexAttribute.Normal),
        [2] = new VertexAttributeDescriptor(VertexAttribute.Tangent),
        [3] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float16, 2),
        [4] = new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 1)
      };

      renderMeshDescription = new RenderMeshDescription(
        shadowCastingMode: ShadowCastingMode.Off,
        receiveShadows: false,
        staticShadowCaster: false
      );

      materialProviderSystem = World.GetOrCreateSystemManaged<MaterialProviderSystem>();
    }

    // TODO FIXME Almost equals to one in MaterialProviderSystem
    private async void LoadBitmapToMaterial(int materialIndex, Material material) {
      var bitmapSet = await Res.Load<BitmapSet>((ushort)(CustomTextureIdBase + materialIndex));

      material.SetTexture(MaterialProviderSystem.shaderTextureName, bitmapSet.Texture);

      if (bitmapSet.Description.Transparent)
        material.EnableKeyword(ShaderKeywordStrings._ALPHATEST_ON);
      else
        material.DisableKeyword(ShaderKeywordStrings._ALPHATEST_ON);
    }

    protected override void OnDestroy() {
      base.OnDestroy();

      entityMeshIDs.Dispose();
      materials.Dispose();
      vertexAttributes.Dispose();
    }

    protected override void OnUpdate() {
      int entityCount = newMeshQuery.CalculateEntityCount();
      if (entityCount == 0) return;

      var ecbSystem = World.GetExistingSystemManaged<EndVariableRateSimulationEntityCommandBufferSystem>();
      var commandBuffer = ecbSystem.CreateCommandBuffer();

      var entitiesGraphicsSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<EntitiesGraphicsSystem>();

      using var entities = newMeshQuery.ToEntityArray(Allocator.TempJob);

      var meshDataArray = Mesh.AllocateWritableMeshData(entityCount);

      var level = SystemAPI.GetSingleton<Level>();

      var vertexAttributes = this.vertexAttributes;
      var materials = this.materials;

      Entities
        .WithStoreEntityQueryInField(ref newMeshQuery)
        .WithAll<TexturedCuboid, ObjectInstance>()
        .WithNone<MeshCachedTag>()
        .ForEach((Entity entity, int entityInQueryIndex, in TexturedCuboid texturedCuboid, in ObjectInstance instanceData) => {
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

        })
        .ScheduleParallel();

      #region Cache mesh of new entities
      var meshes = new Mesh[entityCount];
      for (int entityIndex = 0; entityIndex < entityCount; ++entityIndex) {
        var entity = entities[entityIndex];
        meshes[entityIndex] = entityMeshes.GetOrAdd(entity, entity => {
          var mesh = new Mesh();
          // mesh.MarkDynamic();

          if (entityMeshIDs.TryAdd(entity, entitiesGraphicsSystem.RegisterMesh(mesh)) == false) {
            UnityEngine.Object.Destroy(mesh);
            throw new Exception(@"Failed to add registered mesh.");
          }

          commandBuffer.AddComponent<MeshCachedTag>(entity);

          return mesh;
        });
      }
      #endregion

      CompleteDependency();

      Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, meshes);

      for (int i = 0; i < meshes.Length; ++i) {
        var mesh = meshes[i];
        mesh.RecalculateNormals();
        // mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        mesh.UploadMeshData(true);
      }

      // TODO physics

      Entities
        .WithAll<TexturedCuboid, ObjectInstance>()
        .WithNone<MeshCachedTag>()
        .ForEach((Entity entity, int entityInQueryIndex, in TexturedCuboid texturedCuboid, in ObjectInstance instanceData) => {
          var SideTexture = texturedCuboid.SideTexture;
          var TopBottomTexture = texturedCuboid.TopBottomTexture;

          if (entityMeshIDs.TryGetValue(entity, out BatchMeshID meshID) == false)
            return;

          #region Sides
          {
            BatchMaterialID material;

            if ((SideTexture & 0x80) == 0x80) {
              byte textureMapIndex = (byte)(SideTexture & 0x7F);
              ushort textureIndex = level.TextureMap[textureMapIndex];
              material = materialProviderSystem.GetTextureMaterial(textureIndex);
            } else {
              material = materials[SideTexture & 0x7F];
            }

            var viewPart = EntityManager.CreateEntity(viewPartArchetype); // Sync point
            RenderMeshUtility.AddComponents(
              viewPart,
              EntityManager,
              renderMeshDescription,
              new MaterialMeshInfo {
                MeshID = meshID,
                MaterialID = material,
                SubMesh = 0
              }
            );

            commandBuffer.SetComponent(viewPart, new Parent { Value = entity });
            commandBuffer.SetComponent(viewPart, LocalTransform.FromPosition(0f, -texturedCuboid.Offset, 0f));
          }
          #endregion

          #region Top and Bottom
          {
            BatchMaterialID material;

            if ((TopBottomTexture & 0x80) == 0x80) {
              byte textureMapIndex = (byte)(TopBottomTexture & 0x7F);
              ushort textureIndex = level.TextureMap[textureMapIndex];
              material = materialProviderSystem.GetTextureMaterial(textureIndex);
            } else {
              material = materials[TopBottomTexture & 0x7F];
            }

            var viewPart = EntityManager.CreateEntity(viewPartArchetype); // Sync point
            RenderMeshUtility.AddComponents(
              viewPart,
              EntityManager,
              renderMeshDescription,
              new MaterialMeshInfo {
                MeshID = meshID,
                MaterialID = material,
                SubMesh = 1
              }
            );

            commandBuffer.SetComponent(viewPart, new Parent { Value = entity });
            commandBuffer.SetComponent(viewPart, LocalTransform.FromPosition(0f, -texturedCuboid.Offset, 0f));
          }
          #endregion
        })
        .WithStructuralChanges()
        .Run();
    }
  }

  public struct SpecialPart : IComponentData { }

  internal struct MeshCachedTag : ICleanupComponentData { }

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
