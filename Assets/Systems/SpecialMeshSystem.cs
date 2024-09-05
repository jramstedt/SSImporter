using SS.Data;
using SS.Resources;
using System;
using System.Collections.Concurrent;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static SS.TextureUtils;
using static Unity.Mathematics.math;
using Object = UnityEngine.Object;

namespace SS.System {
  [CreateAfter(typeof(EntitiesGraphicsSystem))]
  [UpdateInGroup(typeof(VariableRateSimulationSystemGroup))]
  public partial class SpecialMeshSystem : SystemBase {
    private EntityQuery newMeshQuery;
    private EntityQuery activeMeshQuery;
    private EntityQuery removedMeshQuery;

    private EntityArchetype viewPartArchetype;

    private NativeArray<BatchMaterialID> materials;
    private NativeHashMap<Entity, BatchMeshID> entityMeshIDs = new(ObjectConstants.NUM_OBJECTS, Allocator.Persistent);

    private NativeArray<VertexAttributeDescriptor> vertexAttributes;
    private RenderMeshDescription renderMeshDescription;

    private MaterialProviderSystem materialProviderSystem;

    protected override void OnCreate() {
      base.OnCreate();

      RequireForUpdate<Level>();

      newMeshQuery = new EntityQueryBuilder(Allocator.Temp)
        .WithAll<Cuboid, ObjectInstance>()
        .WithNone<MeshCachedTag>()
        .Build(this);

      activeMeshQuery = new EntityQueryBuilder(Allocator.Temp)
        .WithAll<Cuboid, LocalToWorld, MeshCachedTag>()
        .Build(this);

      removedMeshQuery = new EntityQueryBuilder(Allocator.Temp)
        .WithAll<MeshCachedTag>()
        .WithNone<Cuboid>()
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
      var textureSet = CreateTexture(bitmapSet);

      material.SetTexture(MaterialProviderSystem.shaderTextureName, textureSet.Texture);

      if (textureSet.Description.Transparent)
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

      var level = SystemAPI.GetSingleton<Level>();
      using var entities = newMeshQuery.ToEntityListAsync(Allocator.TempJob, out var entitiesListJobHandle);
      using var cuboids = newMeshQuery.ToComponentDataListAsync<Cuboid>(Allocator.TempJob, out var cuboidsListJobHandle);

      var meshDataArray = Mesh.AllocateWritableMeshData(entityCount);

      new BuildSpecialMeshJob {
        MeshDataArray = meshDataArray,
        VertexAttributes = vertexAttributes,
      }.ScheduleParallel(newMeshQuery);
      
      Dependency = JobHandle.CombineDependencies(Dependency, entitiesListJobHandle, cuboidsListJobHandle);
      CompleteDependency();

      #region Cache mesh of new entities
      var meshes = new Mesh[entityCount];
      
      for (int entityIndex = 0; entityIndex < entityCount; ++entityIndex) {
        var entity = entities[entityIndex];

        if (entityMeshIDs.TryGetValue(entity, out var batchMeshID)) {
          meshes[entityIndex] = entitiesGraphicsSystem.GetMesh(batchMeshID);
        } else {
          var mesh = new Mesh();
          
          if (entityMeshIDs.TryAdd(entity, entitiesGraphicsSystem.RegisterMesh(mesh)) == false) {
            Object.Destroy(mesh);
            throw new Exception(@"Failed to add registered mesh.");
          }

          meshes[entityIndex] = mesh;
          
          commandBuffer.AddComponent<MeshCachedTag>(entity);
        }
      }
      #endregion

      Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, meshes);

      for (int meshIndex = 0; meshIndex < meshes.Length; ++meshIndex) {
        var mesh = meshes[meshIndex];
        mesh.RecalculateNormals();
        // mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        mesh.UploadMeshData(true);
      }

      // TODO physics

      for (int entityIndex = 0; entityIndex < entityCount; ++entityIndex) {
        var entity = entities[entityIndex];
        var cuboid = cuboids[entityIndex];

        if (entityMeshIDs.TryGetValue(entity, out BatchMeshID meshID) == false)
          continue;

        var SideTexture = cuboid.SideTexture;
        var TopBottomTexture = cuboid.TopBottomTexture;

        #region Sides
        {
          BatchMaterialID material;

          if (SideTexture < 0) {
            material = materialProviderSystem.GetTranslucentMaterial((byte)(-SideTexture));
          } else if ((SideTexture & 0x80) == 0x80) {
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
          commandBuffer.SetComponent(viewPart, LocalTransform.FromPosition(0f, -cuboid.Offset, 0f));
        }
        #endregion

        #region Top and Bottom
        {
          BatchMaterialID material;

          if (TopBottomTexture < 0) {
            material = materialProviderSystem.GetTranslucentMaterial((byte)(-TopBottomTexture));
          } else if ((TopBottomTexture & 0x80) == 0x80) {
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
          commandBuffer.SetComponent(viewPart, LocalTransform.FromPosition(0f, -cuboid.Offset, 0f));
        }
        #endregion
      }
    }

    [BurstCompile]
    internal partial struct BuildSpecialMeshJob : IJobEntity {
      public Mesh.MeshDataArray MeshDataArray;
      [ReadOnly] public NativeArray<VertexAttributeDescriptor> VertexAttributes;

      private void Execute([EntityIndexInQuery] int entityIndexInQuery, in Cuboid cuboid, in ObjectInstance instanceData) {
        var meshData = MeshDataArray[entityIndexInQuery];

        meshData.subMeshCount = 2;
        meshData.SetVertexBufferParams(4 * 6, VertexAttributes);
        meshData.SetIndexBufferParams(6 * 6, IndexFormat.UInt16);

        ReadOnlySpan<ushort> indicesTemplate = stackalloc ushort[] {
          2, 1, 0, 0, 3, 2,
          4, 5, 6, 6, 7, 4,

          8, 9, 10, 10, 11, 8,
          12, 13, 14, 14, 15, 12,
          16, 17, 18, 18, 19, 16,
          22, 21, 20, 20, 23, 22
        };

        // TODO Use Offset? obj_model_hack

        ReadOnlySpan<float3> verticesTemplate = stackalloc float3[] {
          // Top
          float3(-cuboid.Size.x, cuboid.Size.z * 2f, -cuboid.Size.y),
          float3(cuboid.Size.x, cuboid.Size.z * 2f, -cuboid.Size.y),
          float3(cuboid.Size.x, cuboid.Size.z * 2f, cuboid.Size.y),
          float3(-cuboid.Size.x, cuboid.Size.z * 2f, cuboid.Size.y),

          // Bottom
          float3(-cuboid.Size.x, 0f, -cuboid.Size.y),
          float3(cuboid.Size.x, 0f, -cuboid.Size.y),
          float3(cuboid.Size.x, 0f, cuboid.Size.y),
          float3(-cuboid.Size.x, 0f, cuboid.Size.y)
        };
        var vertices = meshData.GetVertexData<Vertex>();

        // +Y
        vertices[0] = new Vertex { pos = verticesTemplate[0], uv = half2(half(0f), half(1f)), light = 0f };
        vertices[1] = new Vertex { pos = verticesTemplate[1], uv = half2(half(1f), half(1f)), light = 0f };
        vertices[2] = new Vertex { pos = verticesTemplate[2], uv = half2(half(1f), half(0f)), light = 0f };
        vertices[3] = new Vertex { pos = verticesTemplate[3], uv = half2(half(0f), half(0f)), light = 0f };

        // -Y
        vertices[4] = new Vertex { pos = verticesTemplate[4], uv = half2(half(0f), half(1f)), light = 0f };
        vertices[5] = new Vertex { pos = verticesTemplate[5], uv = half2(half(1f), half(1f)), light = 0f };
        vertices[6] = new Vertex { pos = verticesTemplate[6], uv = half2(half(1f), half(0f)), light = 0f };
        vertices[7] = new Vertex { pos = verticesTemplate[7], uv = half2(half(0f), half(0f)), light = 0f };

        // +Z
        vertices[8] = new Vertex { pos = verticesTemplate[0], uv = half2(half(0f), half(1f)), light = 0f };
        vertices[9] = new Vertex { pos = verticesTemplate[1], uv = half2(half(1f), half(1f)), light = 0f };
        vertices[10] = new Vertex { pos = verticesTemplate[5], uv = half2(half(1f), half(0f)), light = 0f };
        vertices[11] = new Vertex { pos = verticesTemplate[4], uv = half2(half(0f), half(0f)), light = 0f };

        // -Z
        vertices[12] = new Vertex { pos = verticesTemplate[2], uv = half2(half(0f), half(1f)), light = 0f };
        vertices[13] = new Vertex { pos = verticesTemplate[3], uv = half2(half(1f), half(1f)), light = 0f };
        vertices[14] = new Vertex { pos = verticesTemplate[7], uv = half2(half(1f), half(0f)), light = 0f };
        vertices[15] = new Vertex { pos = verticesTemplate[6], uv = half2(half(0f), half(0f)), light = 0f };

        // +X
        vertices[16] = new Vertex { pos = verticesTemplate[1], uv = half2(half(0f), half(1f)), light = 0f };
        vertices[17] = new Vertex { pos = verticesTemplate[2], uv = half2(half(1f), half(1f)), light = 0f };
        vertices[18] = new Vertex { pos = verticesTemplate[6], uv = half2(half(1f), half(0f)), light = 0f };
        vertices[19] = new Vertex { pos = verticesTemplate[5], uv = half2(half(0f), half(0f)), light = 0f };

        // -X
        vertices[20] = new Vertex { pos = verticesTemplate[0], uv = half2(half(1f), half(1f)), light = 0f };
        vertices[21] = new Vertex { pos = verticesTemplate[3], uv = half2(half(0f), half(1f)), light = 0f };
        vertices[22] = new Vertex { pos = verticesTemplate[7], uv = half2(half(0f), half(0f)), light = 0f };
        vertices[23] = new Vertex { pos = verticesTemplate[4], uv = half2(half(1f), half(0f)), light = 0f };

        var indices = meshData.GetIndexData<ushort>();
        indicesTemplate.CopyTo(indices);

        meshData.SetSubMesh(0, new SubMeshDescriptor(0, 2 * 6, MeshTopology.Triangles));
        meshData.SetSubMesh(1, new SubMeshDescriptor(2 * 6, 4 * 6, MeshTopology.Triangles));
      }
    }
  }

  public struct SpecialPart : IComponentData { }

  internal struct MeshCachedTag : ICleanupComponentData { }

  public struct Cuboid : IComponentData {
    public float3 Size;
    public float Offset;
    public short SideTexture;
    public short TopBottomTexture;
  }
}
