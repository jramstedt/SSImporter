using System;
using System.Collections.Generic;
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
using UnityEngine.Rendering;
using static Unity.Mathematics.math;

namespace SS.System {
  [UpdateInGroup (typeof(InitializationSystemGroup))]
  public sealed class MapElementBuilderSystem : SystemBase {
    public Dictionary<ushort, Material> mapMaterial;

    private EntityArchetype viewPartArchetype;
    private EntityQuery mapElementQuery;
    private EntityQuery viewPartQuery;

    protected override void OnCreate() {
      base.OnCreate();

      RequireSingletonForUpdate<Level>();
      RequireSingletonForUpdate<LevelInfo>();

      viewPartArchetype = World.EntityManager.CreateArchetype(
        typeof(ViewPart),
        typeof(Parent),
        typeof(LocalToWorld),
        typeof(LocalToParent),
        typeof(RenderBounds),
        typeof(RenderMesh),
        typeof(FrozenRenderSceneTag)
      );

      mapElementQuery = GetEntityQuery(new EntityQueryDesc {
        All = new ComponentType[] {
          ComponentType.ReadOnly<TileLocation>(),
          ComponentType.ReadOnly<MapElement>(),
          ComponentType.ReadWrite<LocalToWorld>(),
          ComponentType.ReadOnly<ViewPartRebuildTag>()
        }
      });

      viewPartQuery = GetEntityQuery(new EntityQueryDesc {
        All = new ComponentType[] {
          ComponentType.ReadOnly<ViewPart>(),
          ComponentType.ReadOnly<Parent>()
        }
      });
    }

    protected override void OnUpdate() {
      var ecbSystem = World.GetExistingSystem<EndInitializationEntityCommandBufferSystem>();
      var commandBuffer = ecbSystem.CreateCommandBuffer();

      var entityCount = mapElementQuery.CalculateEntityCount();
      if (entityCount == 0) return;

      var entities = mapElementQuery.ToEntityArray(Allocator.TempJob);

      var level = GetSingleton<Level>();
      var levelInfo = GetSingleton<LevelInfo>();

      var meshDataArray = Mesh.AllocateWritableMeshData(entityCount);
      var submeshTextureIndex = new NativeArray<byte>(entityCount * 6, Allocator.TempJob);

      #region Clean up old view parts that are going to be replaced
      var cleanJob = new DestroyOldViewPartsJob {
        entityTypeHandle = GetEntityTypeHandle(),
        parentTypeHandle = GetComponentTypeHandle<Parent>(true),
        updateMapElements = entities,
        CommandBuffer = commandBuffer.AsParallelWriter()
      };

      var destroyOldViewParts = cleanJob.ScheduleParallel(viewPartQuery, dependsOn: Dependency);
      Dependency = destroyOldViewParts;
      ecbSystem.AddJobHandleForProducer(destroyOldViewParts);
      destroyOldViewParts.Complete();
      #endregion

      #region  Build new view parts
      var buildJob = new BuildMapElementMeshJob {
        entityTypeHandle = GetEntityTypeHandle(),
        tileLocationTypeHandle = GetComponentTypeHandle<TileLocation>(true),
        mapElementTypeHandle = GetComponentTypeHandle<MapElement>(true),
        localToWorldTypeHandle = GetComponentTypeHandle<LocalToWorld>(false),
        allMapElements = GetComponentDataFromEntity<MapElement>(true),
        map = GetSingleton<Level>(),

        levelInfo = levelInfo,
        meshDataArray = meshDataArray,
        submeshTextureIndex = submeshTextureIndex
      };

      var buildMapElements = buildJob.ScheduleParallel(mapElementQuery, dependsOn: Dependency);
      Dependency = buildMapElements;
      ecbSystem.AddJobHandleForProducer(buildMapElements);
      buildMapElements.Complete();
      #endregion

      // TODO we should reuse meshes from removed viewpart entities.

      var meshes = new Mesh[entityCount];
      for (var i = 0; i < entityCount; ++i)
        meshes[i] = new Mesh();

      Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, meshes);

      for (int i = 0; i < entityCount; ++i) {
        var entity = entities[i];
        var tile = GetComponent<MapElement>(entity);

        var mesh = meshes[i];
        mesh.RecalculateNormals();
        // mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        mesh.UploadMeshData(true);

        var sceneTileTag = new FrozenRenderSceneTag {
          SceneGUID = new Unity.Entities.Hash128 { Value = level.Id },
          SectionIndex = i
        };

        var textureIndices = new NativeSlice<byte>(submeshTextureIndex, i * 6, 6);

        for (int subMesh = 0; subMesh < mesh.subMeshCount; ++subMesh) {
          if (mesh.GetIndexCount(subMesh) == 0) continue;

          var viewPart = commandBuffer.CreateEntity(viewPartArchetype);
          commandBuffer.SetComponent(viewPart, default(ViewPart));
          commandBuffer.SetComponent(viewPart, new Parent { Value = entity });
          commandBuffer.SetComponent(viewPart, default(LocalToWorld));
          commandBuffer.SetComponent(viewPart, new LocalToParent { Value = Unity.Mathematics.float4x4.Translate(float3(0f, 0f, 0f)) });
          commandBuffer.SetComponent(viewPart, new RenderBounds { Value = new AABB { Center = mesh.bounds.center, Extents = mesh.bounds.extents } });
          commandBuffer.SetSharedComponent(viewPart, new RenderMesh {
            mesh = mesh,
            material = mapMaterial[textureIndices[subMesh]],
            subMesh = subMesh,
            layer = 0,
            castShadows = ShadowCastingMode.On,
            receiveShadows = true,
            needMotionVectorPass = false
          });
          commandBuffer.SetSharedComponent(viewPart, sceneTileTag);
        }
      }

      EntityManager.RemoveComponent<ViewPartRebuildTag>(mapElementQuery);

      submeshTextureIndex.Dispose();
      entities.Dispose();
    }
  }

  [BurstCompile]
  struct DestroyOldViewPartsJob : IJobEntityBatchWithIndex {
    [ReadOnly] public EntityTypeHandle entityTypeHandle;

    [ReadOnly] public ComponentTypeHandle<Parent> parentTypeHandle;

    [ReadOnly] public NativeArray<Entity> updateMapElements;

    [WriteOnly] public EntityCommandBuffer.ParallelWriter CommandBuffer;

    public void Execute(ArchetypeChunk batchInChunk, int batchIndex, int indexOfFirstEntityInQuery) {
      var entities = batchInChunk.GetNativeArray(entityTypeHandle);
      var parents = batchInChunk.GetNativeArray(parentTypeHandle);

      for (int i = 0; i < batchInChunk.Count; ++i) {
        var entity = entities[i];
        var parent = parents[i];

        if (updateMapElements.Contains(parent.Value))
          CommandBuffer.DestroyEntity(batchIndex, entity);
      }
    }
  }

  [BurstCompile]
  struct BuildMapElementMeshJob : IJobEntityBatchWithIndex {
    [ReadOnly] public EntityTypeHandle entityTypeHandle;

    [ReadOnly] public ComponentTypeHandle<TileLocation> tileLocationTypeHandle;
    [ReadOnly] public ComponentTypeHandle<MapElement> mapElementTypeHandle;
    public ComponentTypeHandle<LocalToWorld> localToWorldTypeHandle;
    [ReadOnly] public ComponentDataFromEntity<MapElement> allMapElements;
    [ReadOnly] public Level map;

    [ReadOnly] public LevelInfo levelInfo;
    public Mesh.MeshDataArray meshDataArray;
    [WriteOnly, NativeDisableParallelForRestriction] public NativeArray<byte> submeshTextureIndex;

    public void Execute(ArchetypeChunk batchInChunk, int batchIndex, int indexOfFirstEntityInQuery) {
      var entities = batchInChunk.GetNativeArray(entityTypeHandle);
      var tileLocations = batchInChunk.GetNativeArray(tileLocationTypeHandle);
      var localToWorld = batchInChunk.GetNativeArray(localToWorldTypeHandle);
      var mapElements = batchInChunk.GetNativeArray(mapElementTypeHandle);

      // Mesh.ApplyAndDisposeWritableMeshData()

      for (int i = 0; i < batchInChunk.Count; ++i) {
        var realIndex = indexOfFirstEntityInQuery + i;

        var entity = entities[i];
        var meshData = meshDataArray[realIndex];
        
        var tileLocation = tileLocations[i];
        var mapElement = mapElements[i];

        var textureIndices = new NativeSlice<byte>(submeshTextureIndex, realIndex * 6, 6);

        localToWorld[i] = new LocalToWorld { Value = Unity.Mathematics.float4x4.Translate(float3(tileLocation.X, 0f, tileLocation.Y)) };

        BuildMesh(tileLocation, mapElement, ref meshData, ref textureIndices);
      }
    }

    private bool2 IsWallTextureFlipped (in TileLocation tileLocation, in MapElement texturing) {
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

    private unsafe void ClearIndexArray (in Mesh.MeshData mesh) {
      var index = mesh.GetIndexData<ushort>();
      UnsafeUtility.MemClear(index.GetUnsafePtr(), index.Length * UnsafeUtility.SizeOf<ushort>());
    }

    private void BuildMesh (in TileLocation tileLocation, in MapElement tile, ref Mesh.MeshData mesh, ref NativeSlice<byte> textureIndices) {
      if (tile.TileType == TileType.Solid) {
        mesh.subMeshCount = 0;
        return;
      }

      mesh.subMeshCount = 6; // TODO precalculate how many submeshes really is needed.

      var vertexAttributes = new NativeArray<VertexAttributeDescriptor>(5, Allocator.Temp) {
        [0] = new VertexAttributeDescriptor(VertexAttribute.Position),
        [1] = new VertexAttributeDescriptor(VertexAttribute.Normal),
        [2] = new VertexAttributeDescriptor(VertexAttribute.Tangent),
        [3] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float16, 2),
        [4] = new VertexAttributeDescriptor(VertexAttribute.BlendWeight, VertexAttributeFormat.Float32, 1)
      };

      mesh.SetVertexBufferParams(
        VerticesPerViewPart * mesh.subMeshCount,
        vertexAttributes
      );
      vertexAttributes.Dispose();

      mesh.SetIndexBufferParams(IndicesPerViewPart * mesh.subMeshCount, IndexFormat.UInt16);

      ClearIndexArray(mesh);

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

    private int CreatePlane (in MapElement tile, in Mesh.MeshData mesh, ref NativeSlice<byte> textureIndices, int subMeshIndex, bool isCeiling) {
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

      ReadOnlySpan<int> faceIndicesOffset = stackalloc int[] { 0, 0, 6, 9, 12, 15, 18, 24, 30, 36, 42, 48, 54, 60, 66, 72, 78, 94 };
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
    
    private int CreateWall(in MapElement tile, in Mesh.MeshData mesh, ref NativeSlice<byte> textureIndices, [AssumeRange(0, 6)] int subMeshIndex, [AssumeRange(0, 4)] int leftCorner, [AssumeRange(0, 4)] int rightCorner, ref MapElement adjacent, [AssumeRange(0, 4)] int adjacentLeftCorner, [AssumeRange(0, 4)] int adjacentRightCorner, bool flip, bool forceSolid) {
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
          math.max(tile.FloorCornerHeight(leftCorner), adjacent.FloorCornerHeight(adjacentLeftCorner)),
          math.min(tile.CeilingCornerHeight(leftCorner), adjacent.CeilingCornerHeight(adjacentLeftCorner)),
          math.min(tile.CeilingCornerHeight(rightCorner), adjacent.CeilingCornerHeight(adjacentRightCorner)),
          math.max(tile.FloorCornerHeight(rightCorner), adjacent.FloorCornerHeight(adjacentRightCorner))
        };

        bool floorAboveCeiling = portalPoints[0] > portalPoints[1] ^ portalPoints[3] > portalPoints[2]; // Other corner of ceiling is above and other below floor

        var originalIndexStart = indexStart;
        var originalVertexStart = vertexStart;
        var indexCount = 0;

        // Upper portal border is below ceiling
        if (math.min(portalPoints[1], portalPoints[2]) < math.max(tile.CeilingCornerHeight(leftCorner), tile.CeilingCornerHeight(rightCorner))) {
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
        if (math.max(portalPoints[0], portalPoints[3]) > math.min(tile.FloorCornerHeight(leftCorner), tile.FloorCornerHeight(rightCorner))) {
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

  public struct TileLocation : IComponentData {
    public byte X;
    public byte Y;
  }

  public struct ViewPart : IComponentData { }
  public struct ViewPartRebuildTag : IComponentData { }

  internal struct Vertex {
    public float3 pos;
    public float3 normal;
    public float3 tangent;
    public half2 uv;
    public float light;
  }
}