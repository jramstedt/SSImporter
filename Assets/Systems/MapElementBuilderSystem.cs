using SS.Data;
using SS.Resources;
using System;
using System.Collections.Concurrent;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;
using static Unity.Mathematics.math;

namespace SS.System {
  [UpdateInGroup(typeof(InitializationSystemGroup))]
  public partial class MapElementBuilderSystem : SystemBase {
    public NativeHashMap<ushort, BatchMaterialID>.ReadOnly mapMaterial;

    private EntityArchetype viewPartArchetype;
    private EntityQuery mapElementQuery;
    private EntityQuery viewPartQuery;
    private NativeArray<VertexAttributeDescriptor> vertexAttributes;

    private ConcurrentDictionary<Entity, Mesh> entityMeshes = new();
    private NativeHashMap<Entity, BatchMeshID> entityMeshIDs = new(64 * 64, Allocator.Persistent);

    private RenderMeshDescription renderMeshDescription;

    protected override void OnCreate() {
      base.OnCreate();

      RequireForUpdate<Level>();
      RequireForUpdate<LevelInfo>();

      viewPartArchetype = EntityManager.CreateArchetype(
        typeof(LevelViewPart),

        typeof(LocalTransform),
        typeof(WorldTransform),

        typeof(Parent),
        typeof(ParentTransform),

        typeof(LocalToWorld),
        typeof(RenderBounds),

        typeof(FrozenRenderSceneTag)
      );

      mapElementQuery = GetEntityQuery(new EntityQueryDesc {
        All = new ComponentType[] {
          ComponentType.ReadOnly<TileLocation>(),
          ComponentType.ReadOnly<MapElement>(),
          ComponentType.ReadOnly<LevelViewPartRebuildTag>()
        }
      });

      viewPartQuery = GetEntityQuery(new EntityQueryDesc {
        All = new ComponentType[] {
          ComponentType.ReadOnly<LevelViewPart>(),
          ComponentType.ReadOnly<Parent>()
        }
      });

      this.vertexAttributes = new(5, Allocator.Persistent) {
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

      this.vertexAttributes.Dispose();
      this.entityMeshIDs.Dispose();
    }

    protected override void OnUpdate() {
      var ecbSystem = World.GetExistingSystemManaged<EndInitializationEntityCommandBufferSystem>();
      var commandBuffer = ecbSystem.CreateCommandBuffer();

      var entitiesGraphicsSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<EntitiesGraphicsSystem>();

      var entityCount = mapElementQuery.CalculateEntityCount();
      if (entityCount == 0) return;

      using var entities = mapElementQuery.ToEntityArray(Allocator.TempJob);

      var level = SystemAPI.GetSingleton<Level>();
      var levelInfo = SystemAPI.GetSingleton<LevelInfo>();

      var meshDataArray = Mesh.AllocateWritableMeshData(entityCount);
      using var submeshTextureIndex = new NativeArray<byte>(entityCount * 6, Allocator.TempJob);

      #region Clean up old view parts that are going to be replaced
      var cleanJob = new DestroyOldViewPartsJob {
        entityTypeHandle = GetEntityTypeHandle(),
        parentTypeHandle = GetComponentTypeHandle<Parent>(true),
        updateMapElements = entities,
        CommandBuffer = commandBuffer.AsParallelWriter()
      };

      Dependency = cleanJob.ScheduleParallel(viewPartQuery, Dependency);
      #endregion

      #region Build new view parts
      NativeArray<int> chunkBaseEntityIndices = mapElementQuery.CalculateBaseEntityIndexArrayAsync(Allocator.TempJob, Dependency, out JobHandle baseIndexJobHandle);

      var buildJob = new BuildMapElementMeshJob {
        entityTypeHandle = GetEntityTypeHandle(),
        tileLocationTypeHandle = GetComponentTypeHandle<TileLocation>(true),
        mapElementTypeHandle = GetComponentTypeHandle<MapElement>(true),
        allMapElements = GetComponentLookup<MapElement>(true),
        ChunkBaseEntityIndices = chunkBaseEntityIndices,

        map = level,
        levelInfo = levelInfo,

        vertexAttributes = vertexAttributes,
        meshDataArray = meshDataArray,
        submeshTextureIndex = submeshTextureIndex,
      };

      Dependency = buildJob.ScheduleParallel(mapElementQuery, baseIndexJobHandle);
      #endregion

      CompleteDependency();
      EntityManager.RemoveComponent<LevelViewPartRebuildTag>(mapElementQuery);

      #region Update meshes
      var meshes = new Mesh[entityCount];
      for (int entityIndex = 0; entityIndex < entityCount; ++entityIndex) {
        var entity = entities[entityIndex];
        meshes[entityIndex] = entityMeshes.GetOrAdd(entity, entity => {
          var mesh = new Mesh();
          // mesh.MarkDynamic();
          if (entityMeshIDs.TryAdd(entity, entitiesGraphicsSystem.RegisterMesh(mesh)) == false)
            throw new Exception(@"Failed to add registered mesh.");

          return mesh;
        });
      }
      Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, meshes);

      for (int meshIndex = 0; meshIndex < meshes.Length; ++meshIndex) {
        var mesh = meshes[meshIndex];
        mesh.RecalculateNormals();
        // mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        mesh.UploadMeshData(false);
      }
      #endregion

      var sceneTileTag = new FrozenRenderSceneTag {
        SceneGUID = new Unity.Entities.Hash128 { Value = level.Id }
      };

      var prototype = EntityManager.CreateEntity(viewPartArchetype); // Sync point
      EntityManager.SetComponentData(prototype, LocalTransform.Identity);
      RenderMeshUtility.AddComponents(
        prototype,
        EntityManager,
        renderMeshDescription,
        new RenderMeshArray(new Material[0], new Mesh[0])
      );

      for (int entityIndex = 0; entityIndex < entityCount; ++entityIndex) {
        var entity = entities[entityIndex];

        if (entityMeshes.TryGetValue(entity, out Mesh mesh) == false)
          continue;

        if (mesh.subMeshCount == 0) continue;

        if (entityMeshIDs.TryGetValue(entity, out BatchMeshID meshID) == false)
          continue;

        var textureIndices = new NativeSlice<byte>(submeshTextureIndex, entityIndex * 6, 6);

        var renderBounds = new RenderBounds { Value = mesh.bounds.ToAABB() };

        for (sbyte subMesh = 0; subMesh < mesh.subMeshCount; ++subMesh) {
          if (mesh.GetIndexCount(subMesh) == 0) continue;

          var viewPart = commandBuffer.Instantiate(prototype);
          commandBuffer.SetComponent(viewPart, new Parent { Value = entity });
          commandBuffer.SetComponent(viewPart, renderBounds);
          commandBuffer.SetComponent(viewPart, new MaterialMeshInfo {
            MeshID = meshID,
            MaterialID = mapMaterial[textureIndices[subMesh]],
            Submesh = subMesh
          });

          sceneTileTag.SectionIndex = entityIndex;
          commandBuffer.SetSharedComponent(viewPart, sceneTileTag);
        }
      }

      var finalizeCommandBuffer = ecbSystem.CreateCommandBuffer();
      finalizeCommandBuffer.DestroyEntity(prototype);
    }
  }

  [BurstCompile]
  struct DestroyOldViewPartsJob : IJobChunk {
    [ReadOnly] public EntityTypeHandle entityTypeHandle;

    [ReadOnly] public ComponentTypeHandle<Parent> parentTypeHandle;

    [ReadOnly] public NativeArray<Entity> updateMapElements;

    [WriteOnly] public EntityCommandBuffer.ParallelWriter CommandBuffer;

    public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask) {
      var entities = chunk.GetNativeArray(entityTypeHandle);
      var parents = chunk.GetNativeArray(ref parentTypeHandle);

      for (int i = 0; i < chunk.Count; ++i) {
        var entity = entities[i];
        var parent = parents[i];

        if (updateMapElements.Contains(parent.Value))
          CommandBuffer.DestroyEntity(unfilteredChunkIndex, entity);
      }
    }
  }

  [BurstCompile]
  struct BuildMapElementMeshJob : IJobChunk {
    [ReadOnly] public EntityTypeHandle entityTypeHandle;

    [ReadOnly] public ComponentTypeHandle<TileLocation> tileLocationTypeHandle;
    [ReadOnly] public ComponentTypeHandle<MapElement> mapElementTypeHandle;
    [ReadOnly] public ComponentLookup<MapElement> allMapElements;
    [ReadOnly][DeallocateOnJobCompletion] public NativeArray<int> ChunkBaseEntityIndices;

    [ReadOnly] public Level map;
    [ReadOnly] public LevelInfo levelInfo;

    [ReadOnly] public NativeArray<VertexAttributeDescriptor> vertexAttributes;

    [NativeDisableParallelForRestriction] public Mesh.MeshDataArray meshDataArray;
    [WriteOnly, NativeDisableParallelForRestriction] public NativeArray<byte> submeshTextureIndex;

    public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask) {
      var entities = chunk.GetNativeArray(entityTypeHandle);
      var tileLocations = chunk.GetNativeArray(ref tileLocationTypeHandle);
      var mapElements = chunk.GetNativeArray(ref mapElementTypeHandle);

      // Mesh.ApplyAndDisposeWritableMeshData()

      int baseEntityIndex = ChunkBaseEntityIndices[unfilteredChunkIndex];

      for (int i = 0; i < chunk.Count; ++i) {
        var realIndex = baseEntityIndex + i;

        var entity = entities[i];
        var meshData = meshDataArray[realIndex];

        var tileLocation = tileLocations[i];
        var mapElement = mapElements[i];

        var textureIndices = new NativeSlice<byte>(submeshTextureIndex, realIndex * 6, 6);

        BuildMesh(tileLocation, mapElement, ref meshData, ref textureIndices);
      }
    }

    private bool2 IsWallTextureFlipped(in TileLocation tileLocation, in MapElement texturing) {
      bool2 flip = default;

      if (texturing.TextureAlternate) {
        flip.x = ((tileLocation.X ^ ~tileLocation.Y) & 1) == 1;
        flip.y = !flip.x;
      }

      if (texturing.TextureParity) {
        flip.x = !flip.x;
        flip.y = !flip.y;
      }

      return flip;
    }

    private const int VerticesPerViewPart = 8;
    private const int IndicesPerViewPart = 12;

    private unsafe void ClearIndexArray(in Mesh.MeshData mesh) {
      var index = mesh.GetIndexData<ushort>();
      UnsafeUtility.MemClear(index.GetUnsafePtr(), index.Length * UnsafeUtility.SizeOf<ushort>());
    }

    private unsafe void ClearVertexArray(in Mesh.MeshData mesh) {
      Vertex* nullVertex = stackalloc Vertex[] {
        new Vertex { pos = float3(0f), normal = float3(0f), tangent = float3(0f), uv = half2(0f), light = 0f }
      };

      var vertices = mesh.GetVertexData<Vertex>();
      UnsafeUtility.MemCpyReplicate(vertices.GetUnsafePtr(), nullVertex, UnsafeUtility.SizeOf<Vertex>(), vertices.Length);
    }

    private void BuildMesh(in TileLocation tileLocation, in MapElement tile, ref Mesh.MeshData mesh, ref NativeSlice<byte> textureIndices) {
      if (tile.TileType == TileType.Solid) {
        mesh.subMeshCount = 0;
        return;
      }

      mesh.subMeshCount = 6; // TODO precalculate how many submeshes really is needed.

      mesh.SetVertexBufferParams(
        VerticesPerViewPart * mesh.subMeshCount,
        vertexAttributes
      );

      mesh.SetIndexBufferParams(IndicesPerViewPart * mesh.subMeshCount, IndexFormat.UInt16);

      ClearIndexArray(mesh);
      ClearVertexArray(mesh);

      var subMeshAccumulator = 0;

      subMeshAccumulator += this.CreatePlane(tile, mesh, ref textureIndices, subMeshAccumulator, false);
      subMeshAccumulator += this.CreatePlane(tile, mesh, ref textureIndices, subMeshAccumulator, true);

      #region North Wall
      {
        var adjacentTileEntity = map.TileMap.Value[(tileLocation.Y + 1) * levelInfo.Width + tileLocation.X];
        MapElement adjacentTile = allMapElements[adjacentTileEntity];

        var flip = IsWallTextureFlipped(tileLocation, tile).y;

        if (tile.TileType == TileType.OpenDiagonalSE)
          subMeshAccumulator += CreateWall(tile, mesh, ref textureIndices, subMeshAccumulator, 0, 2, ref adjacentTile, 0, 2, flip, true);
        else if (tile.TileType == TileType.OpenDiagonalSW)
          subMeshAccumulator += CreateWall(tile, mesh, ref textureIndices, subMeshAccumulator, 1, 3, ref adjacentTile, 1, 3, flip, true);
        else
          subMeshAccumulator += CreateWall(tile, mesh, ref textureIndices, subMeshAccumulator, 1, 2, ref adjacentTile, 0, 3, flip, adjacentTile.TileType == TileType.OpenDiagonalNE || adjacentTile.TileType == TileType.OpenDiagonalNW);
      }
      #endregion

      #region East Wall
      if (tile.TileType != TileType.OpenDiagonalSW && tile.TileType != TileType.OpenDiagonalNW) {
        var adjacentTileEntity = map.TileMap.Value[tileLocation.Y * levelInfo.Width + tileLocation.X + 1];
        MapElement adjacentTile = allMapElements[adjacentTileEntity];

        var flip = IsWallTextureFlipped(tileLocation, tile).x;
        subMeshAccumulator += CreateWall(tile, mesh, ref textureIndices, subMeshAccumulator, 2, 3, ref adjacentTile, 1, 0, flip, adjacentTile.TileType == TileType.OpenDiagonalNE || adjacentTile.TileType == TileType.OpenDiagonalSE);
      }
      #endregion

      #region South Wall
      {
        var adjacentTileEntity = map.TileMap.Value[(tileLocation.Y - 1) * levelInfo.Width + tileLocation.X];
        MapElement adjacentTile = allMapElements[adjacentTileEntity];

        var flip = IsWallTextureFlipped(tileLocation, tile).y;

        if (tile.TileType == TileType.OpenDiagonalNE)
          subMeshAccumulator += CreateWall(tile, mesh, ref textureIndices, subMeshAccumulator, 3, 1, ref adjacentTile, 3, 1, flip, true);
        else if (tile.TileType == TileType.OpenDiagonalNW)
          subMeshAccumulator += CreateWall(tile, mesh, ref textureIndices, subMeshAccumulator, 2, 0, ref adjacentTile, 2, 0, flip, true);
        else
          subMeshAccumulator += CreateWall(tile, mesh, ref textureIndices, subMeshAccumulator, 3, 0, ref adjacentTile, 2, 1, flip, adjacentTile.TileType == TileType.OpenDiagonalSE || adjacentTile.TileType == TileType.OpenDiagonalSW);
      }
      #endregion

      #region West Wall
      if (tile.TileType != TileType.OpenDiagonalSE && tile.TileType != TileType.OpenDiagonalNE) {
        var adjacentTileEntity = map.TileMap.Value[tileLocation.Y * levelInfo.Width + tileLocation.X - 1];
        MapElement adjacentTile = allMapElements[adjacentTileEntity];

        var flip = IsWallTextureFlipped(tileLocation, tile).x;
        subMeshAccumulator += CreateWall(tile, mesh, ref textureIndices, subMeshAccumulator, 0, 1, ref adjacentTile, 3, 2, flip, adjacentTile.TileType == TileType.OpenDiagonalNW || adjacentTile.TileType == TileType.OpenDiagonalSW);
      }
      #endregion
    }

    private int CreatePlane(in MapElement tile, in Mesh.MeshData mesh, ref NativeSlice<byte> textureIndices, [AssumeRange(0, 5)] int subMeshIndex, bool isCeiling) {
      var vertices = mesh.GetVertexData<Vertex>();
      var indices = mesh.GetIndexData<ushort>();

      var vertexStart = subMeshIndex * VerticesPerViewPart;
      var indexStart = subMeshIndex * IndicesPerViewPart;

      ReadOnlySpan<half2> UVTemplate = stackalloc half2[] {
        half2(half(0f), half(0f)),
        half2(half(0f), half(1f)),
        half2(half(1f), half(1f)),
        half2(half(1f), half(0f))
      };

      ReadOnlySpan<ushort> faceIndices = stackalloc ushort[] {
        0, 1, 2, 2, 3, 0,

        2, 3, 0,
        0, 1, 3,
        0, 1, 2,
        1, 2, 3,

        0, 1, 2, 2, 3, 0,
        0, 1, 2, 2, 3, 0,
        0, 1, 2, 2, 3, 0,
        0, 1, 2, 2, 3, 0,

        0, 1, 3, 1, 2, 3,
        0, 1, 2, 2, 3, 0,
        0, 1, 3, 1, 2, 3,
        0, 1, 2, 2, 3, 0,

        0, 1, 3, 1, 2, 3,
        0, 1, 2, 2, 3, 0,
        0, 1, 3, 1, 2, 3,
        0, 1, 2, 2, 3, 0
      };

      ReadOnlySpan<int> faceIndicesOffset = stackalloc int[] { 0, 0, 6, 9, 12, 15, 18, 24, 30, 36, 42, 48, 54, 60, 66, 72, 78, 84 };
      ReadOnlySpan<int> faceIndicesLength = stackalloc int[] { 0, 6, 3, 3, 3, 3, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6 };

      int tileType = (int)tile.TileType;

      var indicesTemplate = faceIndices.Slice(faceIndicesOffset[tileType], faceIndicesLength[tileType]);

      if (isCeiling == true) {
        vertices[vertexStart + 0] = new Vertex { pos = float3(0f, (float)tile.CeilingCornerHeight(0) / (float)levelInfo.HeightDivisor, 0f), uv = UVTemplate[(0 + tile.CeilingRotation) & 0b11], light = 1f };
        vertices[vertexStart + 1] = new Vertex { pos = float3(0f, (float)tile.CeilingCornerHeight(1) / (float)levelInfo.HeightDivisor, 1f), uv = UVTemplate[(1 + tile.CeilingRotation) & 0b11], light = 1f };
        vertices[vertexStart + 2] = new Vertex { pos = float3(1f, (float)tile.CeilingCornerHeight(2) / (float)levelInfo.HeightDivisor, 1f), uv = UVTemplate[(2 + tile.CeilingRotation) & 0b11], light = 1f };
        vertices[vertexStart + 3] = new Vertex { pos = float3(1f, (float)tile.CeilingCornerHeight(3) / (float)levelInfo.HeightDivisor, 0f), uv = UVTemplate[(3 + tile.CeilingRotation) & 0b11], light = 1f };

        // Reverses index order in ceiling
        int lastIndex = indicesTemplate.Length - 1;

        for (int i = 0; i < indicesTemplate.Length; ++i) indices[indexStart + i] = (ushort)(indicesTemplate[lastIndex - i] + vertexStart);
        textureIndices[subMeshIndex] = tile.CeilingTexture;
      } else {
        vertices[vertexStart + 0] = new Vertex { pos = float3(0f, (float)tile.FloorCornerHeight(0) / (float)levelInfo.HeightDivisor, 0f), uv = UVTemplate[(0 + tile.FloorRotation) & 0b11], light = 0f };
        vertices[vertexStart + 1] = new Vertex { pos = float3(0f, (float)tile.FloorCornerHeight(1) / (float)levelInfo.HeightDivisor, 1f), uv = UVTemplate[(1 + tile.FloorRotation) & 0b11], light = 0f };
        vertices[vertexStart + 2] = new Vertex { pos = float3(1f, (float)tile.FloorCornerHeight(2) / (float)levelInfo.HeightDivisor, 1f), uv = UVTemplate[(2 + tile.FloorRotation) & 0b11], light = 0f };
        vertices[vertexStart + 3] = new Vertex { pos = float3(1f, (float)tile.FloorCornerHeight(3) / (float)levelInfo.HeightDivisor, 0f), uv = UVTemplate[(3 + tile.FloorRotation) & 0b11], light = 0f };

        for (int i = 0; i < indicesTemplate.Length; ++i) indices[indexStart + i] = (ushort)(indicesTemplate[i] + vertexStart);
        textureIndices[subMeshIndex] = tile.FloorTexture;
      }

      vertices[vertexStart + 4] = new Vertex { pos = float3(0f), uv = half2(0f), light = 0f };
      vertices[vertexStart + 5] = new Vertex { pos = float3(0f), uv = half2(0f), light = 0f };
      vertices[vertexStart + 6] = new Vertex { pos = float3(0f), uv = half2(0f), light = 0f };
      vertices[vertexStart + 7] = new Vertex { pos = float3(0f), uv = half2(0f), light = 0f };

      mesh.SetSubMesh(subMeshIndex, new SubMeshDescriptor(indexStart, indicesTemplate.Length, MeshTopology.Triangles));

      return 1;
    }

    private int CreateWall(in MapElement tile, in Mesh.MeshData mesh, ref NativeSlice<byte> textureIndices, [AssumeRange(0, 5)] int subMeshIndex, [AssumeRange(0, 3)] int leftCorner, [AssumeRange(0, 3)] int rightCorner, ref MapElement adjacent, [AssumeRange(0, 3)] int adjacentLeftCorner, [AssumeRange(0, 3)] int adjacentRightCorner, bool flip, bool forceSolid) {
      var vertices = mesh.GetVertexData<Vertex>();
      var index = mesh.GetIndexData<ushort>();

      var vertexStart = subMeshIndex * VerticesPerViewPart;
      var indexStart = subMeshIndex * IndicesPerViewPart;

      ReadOnlySpan<ushort> faceIndices = stackalloc ushort[] { 0, 1, 2, 2, 3, 0 };

      ReadOnlySpan<float3> verticeTemplate = stackalloc float3[] {
        float3(0f, 0f, 0f),
        float3(0f, 0f, 1f),
        float3(1f, 0f, 1f),
        float3(1f, 0f, 0f)
      };

      Span<float3> wallVertices = stackalloc float3[] {
        verticeTemplate[leftCorner], // Lower
        verticeTemplate[leftCorner], // Upper
        verticeTemplate[rightCorner], // Upper
        verticeTemplate[rightCorner] // Lower
      };

      ReadOnlySpan<half2> UVTemplate = stackalloc half2[] {
        half2(half(0f), half(0f)),
        half2(half(0f), half(1f)),
        half2(half(1f), half(1f)),
        half2(half(1f), half(0f))
      };

      ReadOnlySpan<half2> UVTemplateFlipped = stackalloc half2[] {
        half2(half(1f), half(0f)),
        half2(half(1f), half(1f)),
        half2(half(0f), half(1f)),
        half2(half(0f), half(0f))
      };

      var uvs = flip ? UVTemplateFlipped : UVTemplate;

      bool isSolidWall = forceSolid
      || (adjacent.TileType == TileType.Solid)
      || (tile.FloorCornerHeight(leftCorner) > adjacent.CeilingCornerHeight(adjacentLeftCorner) && tile.FloorCornerHeight(rightCorner) > adjacent.CeilingCornerHeight(adjacentRightCorner))
      || (tile.CeilingCornerHeight(leftCorner) < adjacent.FloorCornerHeight(adjacentLeftCorner) && tile.CeilingCornerHeight(rightCorner) < adjacent.FloorCornerHeight(adjacentRightCorner));

      float mapScale = 1f / (float)levelInfo.HeightDivisor;
      float textureVerticalOffset = tile.TextureOffset * mapScale;

      if (isSolidWall) { // Add solid wall
        wallVertices[0].y = (float)tile.FloorCornerHeight(leftCorner) * mapScale;
        wallVertices[1].y = (float)tile.CeilingCornerHeight(leftCorner) * mapScale;
        wallVertices[2].y = (float)tile.CeilingCornerHeight(rightCorner) * mapScale;
        wallVertices[3].y = (float)tile.FloorCornerHeight(rightCorner) * mapScale;

        vertices[vertexStart + 0] = new Vertex { pos = wallVertices[0], uv = half2(uvs[0].x, (half)(wallVertices[0].y - textureVerticalOffset)), light = 0f };
        vertices[vertexStart + 1] = new Vertex { pos = wallVertices[1], uv = half2(uvs[1].x, (half)(wallVertices[1].y - textureVerticalOffset)), light = 1f };
        vertices[vertexStart + 2] = new Vertex { pos = wallVertices[2], uv = half2(uvs[2].x, (half)(wallVertices[2].y - textureVerticalOffset)), light = 1f };
        vertices[vertexStart + 3] = new Vertex { pos = wallVertices[3], uv = half2(uvs[3].x, (half)(wallVertices[3].y - textureVerticalOffset)), light = 0f };
        vertices[vertexStart + 4] = new Vertex { pos = float3(0f), uv = half2(0f), light = 0f };
        vertices[vertexStart + 5] = new Vertex { pos = float3(0f), uv = half2(0f), light = 0f };
        vertices[vertexStart + 6] = new Vertex { pos = float3(0f), uv = half2(0f), light = 0f };
        vertices[vertexStart + 7] = new Vertex { pos = float3(0f), uv = half2(0f), light = 0f };

        for (int i = 0; i < faceIndices.Length; ++i) index[indexStart + i] = (ushort)(faceIndices[i] + vertexStart);

        mesh.SetSubMesh(subMeshIndex, new SubMeshDescriptor(indexStart, faceIndices.Length, MeshTopology.Triangles));
        textureIndices[subMeshIndex] = tile.UseAdjacentTexture ? adjacent.WallTexture : tile.WallTexture;
        return 1;
      } else { // Possibly two part wall
        ReadOnlySpan<int> portalPoints = stackalloc int[] {
          math.max(tile.FloorCornerHeight(leftCorner), adjacent.FloorCornerHeight(adjacentLeftCorner)), // Bottom left
          math.min(tile.CeilingCornerHeight(leftCorner), adjacent.CeilingCornerHeight(adjacentLeftCorner)), // Top left
          math.min(tile.CeilingCornerHeight(rightCorner), adjacent.CeilingCornerHeight(adjacentRightCorner)), // Top right
          math.max(tile.FloorCornerHeight(rightCorner), adjacent.FloorCornerHeight(adjacentRightCorner)) // Bottom right
        };

        bool floorAboveCeiling = portalPoints[0] > portalPoints[1] ^ portalPoints[3] > portalPoints[2]; // Other corner of ceiling is above and other below floor

        var originalIndexStart = indexStart;
        var originalVertexStart = vertexStart;
        var indexCount = 0;

        // Upper portal border is below ceiling
        if (portalPoints[1] < tile.CeilingCornerHeight(leftCorner) || portalPoints[2] < tile.CeilingCornerHeight(rightCorner)) {
          wallVertices[0].y = (floorAboveCeiling ? portalPoints[1] : math.max(portalPoints[1], tile.FloorCornerHeight(leftCorner))) * mapScale;
          wallVertices[1].y = tile.CeilingCornerHeight(leftCorner) * mapScale;
          wallVertices[2].y = tile.CeilingCornerHeight(rightCorner) * mapScale;
          wallVertices[3].y = (floorAboveCeiling ? portalPoints[2] : math.max(portalPoints[2], tile.FloorCornerHeight(leftCorner))) * mapScale;

          vertices[vertexStart + 0] = new Vertex { pos = wallVertices[0], uv = half2(uvs[0].x, (half)(wallVertices[0].y - textureVerticalOffset)), light = wallVertices[0].y / wallVertices[1].y };
          vertices[vertexStart + 1] = new Vertex { pos = wallVertices[1], uv = half2(uvs[1].x, (half)(wallVertices[1].y - textureVerticalOffset)), light = 1f };
          vertices[vertexStart + 2] = new Vertex { pos = wallVertices[2], uv = half2(uvs[2].x, (half)(wallVertices[2].y - textureVerticalOffset)), light = 1f };
          vertices[vertexStart + 3] = new Vertex { pos = wallVertices[3], uv = half2(uvs[3].x, (half)(wallVertices[3].y - textureVerticalOffset)), light = wallVertices[3].y / wallVertices[2].y };

          for (int i = 0; i < faceIndices.Length; ++i) index[indexStart + i] = (ushort)(faceIndices[i] + vertexStart);

          indexCount += faceIndices.Length;

          vertexStart += wallVertices.Length;
          indexStart += faceIndices.Length;
        }

        // Lower border is above floor
        if (portalPoints[0] > tile.FloorCornerHeight(leftCorner) || portalPoints[3] > tile.FloorCornerHeight(rightCorner)) {
          wallVertices[0].y = tile.FloorCornerHeight(leftCorner) * mapScale;
          wallVertices[1].y = math.min(portalPoints[0], math.max(tile.CeilingCornerHeight(leftCorner), tile.CeilingCornerHeight(rightCorner))) * mapScale;
          wallVertices[2].y = math.min(portalPoints[3], math.max(tile.CeilingCornerHeight(leftCorner), tile.CeilingCornerHeight(rightCorner))) * mapScale;
          wallVertices[3].y = tile.FloorCornerHeight(rightCorner) * mapScale;

          vertices[vertexStart + 0] = new Vertex { pos = wallVertices[0], uv = half2(uvs[0].x, (half)(wallVertices[0].y - textureVerticalOffset)), light = 0f };
          vertices[vertexStart + 1] = new Vertex { pos = wallVertices[1], uv = half2(uvs[1].x, (half)(wallVertices[1].y - textureVerticalOffset)), light = wallVertices[1].y / (tile.CeilingCornerHeight(leftCorner) * mapScale) };
          vertices[vertexStart + 2] = new Vertex { pos = wallVertices[2], uv = half2(uvs[2].x, (half)(wallVertices[2].y - textureVerticalOffset)), light = wallVertices[2].y / (tile.CeilingCornerHeight(rightCorner) * mapScale) };
          vertices[vertexStart + 3] = new Vertex { pos = wallVertices[3], uv = half2(uvs[3].x, (half)(wallVertices[3].y - textureVerticalOffset)), light = 0f };

          for (int i = 0; i < faceIndices.Length; ++i) index[indexStart + i] = (ushort)(faceIndices[i] + vertexStart);

          indexCount += faceIndices.Length;

          vertexStart += wallVertices.Length;
        }

        for (int vertex = vertexStart; vertex < (originalVertexStart + VerticesPerViewPart); ++vertex)
          vertices[vertex] = new Vertex { pos = float3(0f), uv = half2(0f), light = 0f };

        if (indexCount > 0) {
          mesh.SetSubMesh(subMeshIndex, new SubMeshDescriptor(originalIndexStart, indexCount, MeshTopology.Triangles));
          textureIndices[subMeshIndex] = tile.UseAdjacentTexture ? adjacent.WallTexture : tile.WallTexture;
          return 1;
        }

        return 0;
      }
    }
  }

  public struct LevelViewPart : IComponentData { }
  public struct LevelViewPartRebuildTag : IComponentData { }
}