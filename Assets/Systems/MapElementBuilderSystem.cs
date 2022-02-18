using System;
using System.Collections.Generic;
using SS.Resources;
using Unity.Burst;
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

    // private EndSimulationEntityCommandBufferSystem ecbSystem;
    private EntityQuery mapElementQuery;
    private EntityQuery viewPartQuery;

    protected override void OnCreate() {
      base.OnCreate();

      RequireSingletonForUpdate<Map>();
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

      var map = GetSingleton<Map>();
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
        map = GetSingleton<Map>(),

        levelInfo = levelInfo,
        meshDataArray = meshDataArray,
        submeshTextureIndex = submeshTextureIndex
      };

      var buildMapElements = buildJob.ScheduleParallel(mapElementQuery, dependsOn: Dependency);
      Dependency = buildMapElements;
      ecbSystem.AddJobHandleForProducer(buildMapElements);
      buildMapElements.Complete();
      #endregion

      // TODO reuse meshes from removed viewpart entities.

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
          SceneGUID = new Unity.Entities.Hash128 { Value = map.Id },
          SectionIndex = i
        };

        var textureIndices = new NativeSlice<byte>(submeshTextureIndex, i * 6, 6);

        for (int subMesh = 0; subMesh < mesh.subMeshCount; ++subMesh) {
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

  struct DestroyOldViewPartsJob : IJobEntityBatchWithIndex {
    [ReadOnly] public EntityTypeHandle entityTypeHandle;

    [ReadOnly] public ComponentTypeHandle<Parent> parentTypeHandle;

    [ReadOnly] public NativeArray<Entity> updateMapElements;

    [WriteOnly] public EntityCommandBuffer.ParallelWriter CommandBuffer;

    [BurstCompile]
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

  struct BuildMapElementMeshJob : IJobEntityBatchWithIndex {
    [ReadOnly] public EntityTypeHandle entityTypeHandle;

    [ReadOnly] public ComponentTypeHandle<TileLocation> tileLocationTypeHandle;
    [ReadOnly] public ComponentTypeHandle<MapElement> mapElementTypeHandle;
    public ComponentTypeHandle<LocalToWorld> localToWorldTypeHandle;
    [ReadOnly] public ComponentDataFromEntity<MapElement> allMapElements;
    [ReadOnly] public Map map;

    [ReadOnly] public LevelInfo levelInfo;
    public Mesh.MeshDataArray meshDataArray;
    [WriteOnly, NativeDisableParallelForRestriction] public NativeArray<byte> submeshTextureIndex;

    [BurstCompile]
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

    [BurstCompile]
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

    [BurstCompile]
    private unsafe void ClearIndexArray (in Mesh.MeshData mesh) {
      var index = mesh.GetIndexData<ushort>();
      UnsafeUtility.MemClear(index.GetUnsafePtr(), index.Length * UnsafeUtility.SizeOf<ushort>());
    }

    private struct Vertex {
      public float3 pos;
      public float3 normal;
      public float3 tangent;
      public half2 uv;
      public float light;
    }

    [BurstCompile]
    private void BuildMesh (in TileLocation tileLocation, in MapElement tile, ref Mesh.MeshData mesh, ref NativeSlice<byte> textureIndices) {
      if (tile.TileType == TileType.Solid) {
        mesh.subMeshCount = 0;
        return;
      }

      mesh.subMeshCount = 6; // TODO precalculate how many submeshes really is needed.

      mesh.SetVertexBufferParams(
        VerticesPerViewPart * mesh.subMeshCount,
        new VertexAttributeDescriptor(VertexAttribute.Position),
        new VertexAttributeDescriptor(VertexAttribute.Normal),
        new VertexAttributeDescriptor(VertexAttribute.Tangent),
        new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float16, 2),
        new VertexAttributeDescriptor(VertexAttribute.BlendWeight, VertexAttributeFormat.Float32, 1)
      );
      mesh.SetIndexBufferParams(IndicesPerViewPart * mesh.subMeshCount, IndexFormat.UInt16);

      ClearIndexArray(mesh);

      var subMeshAccumulator = 0;

      subMeshAccumulator += this.CreatePlane(tile, mesh, ref textureIndices, subMeshAccumulator, false);
      subMeshAccumulator += this.CreatePlane(tile, mesh, ref textureIndices, subMeshAccumulator, true);

      #region North Wall
      {
        var adjacentTileEntity = map.TileMap.Value[(tileLocation.Y + 1) * levelInfo.Width + tileLocation.X];
        MapElement adjacentTile = allMapElements[adjacentTileEntity];

        var flip = IsWallTextureFlipped(tileLocation, adjacentTile).y;

        if (tile.TileType == TileType.OpenDiagonalSE)
          subMeshAccumulator += CreateWall(tile, mesh, ref textureIndices, subMeshAccumulator, 0, 2, ref adjacentTile, 0, 2, flip);
        else if (tile.TileType == TileType.OpenDiagonalSW)
          subMeshAccumulator += CreateWall(tile, mesh, ref textureIndices, subMeshAccumulator, 1, 3, ref adjacentTile, 1, 3, flip);
        else
          subMeshAccumulator += CreateWall(tile, mesh, ref textureIndices, subMeshAccumulator, 1, 2, ref adjacentTile, 0, 3, flip, TileType.OpenDiagonalNE, TileType.OpenDiagonalNW);
      }
      #endregion

      #region East Wall
      if (tile.TileType != TileType.OpenDiagonalSW && tile.TileType != TileType.OpenDiagonalNW) {
        var adjacentTileEntity = map.TileMap.Value[tileLocation.Y * levelInfo.Width + tileLocation.X + 1];
        MapElement adjacentTile = allMapElements[adjacentTileEntity];

        var flip = IsWallTextureFlipped(tileLocation, adjacentTile).x;
        subMeshAccumulator += CreateWall(tile, mesh, ref textureIndices, subMeshAccumulator, 2, 3, ref adjacentTile, 1, 0, flip, TileType.OpenDiagonalNE, TileType.OpenDiagonalSE);
      }
      #endregion

      #region South Wall
      {
        var adjacentTileEntity = map.TileMap.Value[(tileLocation.Y - 1) * levelInfo.Width + tileLocation.X];
        MapElement adjacentTile = allMapElements[adjacentTileEntity];

        var flip = IsWallTextureFlipped(tileLocation, adjacentTile).y;

        if (tile.TileType == TileType.OpenDiagonalNE)
          subMeshAccumulator += CreateWall(tile, mesh, ref textureIndices, subMeshAccumulator, 3, 1, ref adjacentTile, 3, 1, flip);
        else if (tile.TileType == TileType.OpenDiagonalNW)
          subMeshAccumulator += CreateWall(tile, mesh, ref textureIndices, subMeshAccumulator, 2, 0, ref adjacentTile, 2, 0, flip);
        else
          subMeshAccumulator += CreateWall(tile, mesh, ref textureIndices, subMeshAccumulator, 3, 0, ref adjacentTile, 2, 1, flip, TileType.OpenDiagonalSE, TileType.OpenDiagonalSW);
      }
      #endregion

      #region West Wall
      if (tile.TileType != TileType.OpenDiagonalSE && tile.TileType != TileType.OpenDiagonalNE) {
        var adjacentTileEntity = map.TileMap.Value[tileLocation.Y * levelInfo.Width + tileLocation.X - 1];
        MapElement adjacentTile = allMapElements[adjacentTileEntity];

        var flip = IsWallTextureFlipped(tileLocation, adjacentTile).x;
        subMeshAccumulator += CreateWall(tile, mesh, ref textureIndices, subMeshAccumulator, 0, 1, ref adjacentTile, 3, 2, flip, TileType.OpenDiagonalNW, TileType.OpenDiagonalSW);
      }
      #endregion
    }

    [BurstCompile]
    private int CreatePlane (in MapElement tile, in Mesh.MeshData mesh, ref NativeSlice<byte> textureIndices, int subMeshIndex, bool isCeiling) {
      var vertices = mesh.GetVertexData<Vertex>();
      var indices = mesh.GetIndexData<ushort>();

      var vertexStart = subMeshIndex * VerticesPerViewPart;
      var indexStart = subMeshIndex * IndicesPerViewPart;

      var uvs = MapUtils.UVTemplate.RotateRight(isCeiling ? tile.CeilingRotation : tile.FloorRotation);

      for (int corner = 0; corner < 4; ++corner) {
        int cornerHeight = isCeiling ? tile.CeilingCornerHeight(corner) : tile.FloorCornerHeight(corner);
        vertices[vertexStart + corner] = new Vertex {
          pos = MapUtils.VerticeTemplate[corner] + float3(0f, (float)cornerHeight / (float)levelInfo.HeightDivisor, 0f),
          uv = uvs[corner],
          light = isCeiling ? 1f : 0f
        };
      }

      for (int corner = 4; corner < VerticesPerViewPart; ++corner)
        vertices[vertexStart + corner] = default;

      ushort[] indicesTemplate = MapUtils.faceIndices[(int)tile.TileType];
      if (isCeiling) // Reverses index order in ceiling
        for (int i = 0; i < indicesTemplate.Length; ++i) indices[indexStart + i] = (ushort)(indicesTemplate[indicesTemplate.Length - 1 - i] + vertexStart);
      else
        for (int i = 0; i < indicesTemplate.Length; ++i) indices[indexStart + i] = (ushort)(indicesTemplate[i] + vertexStart);

      mesh.SetSubMesh(subMeshIndex, new SubMeshDescriptor(indexStart, indicesTemplate.Length, MeshTopology.Triangles));
      textureIndices[subMeshIndex] = isCeiling ? tile.CeilingTexture : tile.FloorTexture;

      return 1;
    }

    [BurstCompile]
    private int CreateWall(in MapElement tile, in Mesh.MeshData mesh, ref NativeSlice<byte> textureIndices, int subMeshIndex, int leftCorner, int rightCorner, ref MapElement adjacent, int adjacentLeftCorner, int adjacentRightCorner, bool flip, params TileType[] ignoreTypes) {
      var vertices = mesh.GetVertexData<Vertex>();
      var index = mesh.GetIndexData<ushort>();

      var vertexStart = subMeshIndex * VerticesPerViewPart;
      var indexStart = subMeshIndex * IndicesPerViewPart;
      
      var indicesTemplate = MapUtils.faceIndices[1];
      var wallVertices = new float3[] {
        MapUtils.VerticeTemplate[leftCorner], // Lower
        MapUtils.VerticeTemplate[leftCorner], // Upper
        MapUtils.VerticeTemplate[rightCorner], // Upper
        MapUtils.VerticeTemplate[rightCorner] // Lower
      };

      var lightblend = new float[] {
        0f,
        1f,
        1f,
        0f
      };

      var uvs = (half2[])(flip ? MapUtils.UVTemplateFlipped : MapUtils.UVTemplate).Clone();

      bool isSolidWall = adjacent.TileType == TileType.Solid;
      for (int i = 0; i < ignoreTypes.Length; ++i)
        isSolidWall |= adjacent.TileType == ignoreTypes[i];

      if (ignoreTypes.Length == 0) // Special case, diagonal walls have no ignore types
        isSolidWall = true;

      isSolidWall |= tile.FloorCornerHeight(leftCorner) > adjacent.CeilingCornerHeight(adjacentLeftCorner) &&
                      tile.FloorCornerHeight(rightCorner) > adjacent.CeilingCornerHeight(adjacentRightCorner);

      isSolidWall |= tile.CeilingCornerHeight(leftCorner) < adjacent.FloorCornerHeight(adjacentLeftCorner) &&
                      tile.CeilingCornerHeight(rightCorner) < adjacent.FloorCornerHeight(adjacentRightCorner);

      float mapScale = 1f / (float)levelInfo.HeightDivisor;
      float textureVerticalOffset = tile.TextureOffset * mapScale;

      if (isSolidWall) { // Add solid wall
        wallVertices[0].y = (float)tile.FloorCornerHeight(leftCorner) * mapScale;
        wallVertices[1].y = (float)tile.CeilingCornerHeight(leftCorner) * mapScale;
        wallVertices[2].y = (float)tile.CeilingCornerHeight(rightCorner) * mapScale;
        wallVertices[3].y = (float)tile.FloorCornerHeight(rightCorner) * mapScale;

        uvs[0].y = (half)(wallVertices[0].y - textureVerticalOffset);
        uvs[1].y = (half)(wallVertices[1].y - textureVerticalOffset);
        uvs[2].y = (half)(wallVertices[2].y - textureVerticalOffset);
        uvs[3].y = (half)(wallVertices[3].y - textureVerticalOffset);

        for (int vertex = 0; vertex < wallVertices.Length; ++vertex) {
          vertices[vertexStart + vertex] = new Vertex {
            pos = wallVertices[vertex],
            uv = uvs[vertex],
            light = lightblend[vertex]
          };
        }

        for (int vertex = wallVertices.Length; vertex < VerticesPerViewPart; ++vertex)
          vertices[vertexStart + vertex] = default;

        for (int i = 0; i < indicesTemplate.Length; ++i) index[indexStart + i] = (ushort)(indicesTemplate[i] + vertexStart);
        
        mesh.SetSubMesh(subMeshIndex, new SubMeshDescriptor(indexStart, indicesTemplate.Length, MeshTopology.Triangles));
        textureIndices[subMeshIndex] = tile.UseAdjacentTexture ? adjacent.WallTexture : tile.WallTexture;
        return 1;
      } else { // Possibly two part wall
        int[] portalPoints = new int[] {
          Mathf.Max(tile.FloorCornerHeight(leftCorner), adjacent.FloorCornerHeight(adjacentLeftCorner)),
          Mathf.Min(tile.CeilingCornerHeight(leftCorner), adjacent.CeilingCornerHeight(adjacentLeftCorner)),
          Mathf.Min(tile.CeilingCornerHeight(rightCorner), adjacent.CeilingCornerHeight(adjacentRightCorner)),
          Mathf.Max(tile.FloorCornerHeight(rightCorner), adjacent.FloorCornerHeight(adjacentRightCorner))
        };

        bool floorAboveCeiling = portalPoints[0] > portalPoints[1] ^ portalPoints[3] > portalPoints[2]; // Other corner of ceiling is above and other below floor

        var originalIndexStart = indexStart;
        var indiceCount = 0;

        // Upper portal border is below ceiling
        if (Mathf.Min(portalPoints[1], portalPoints[2]) < Mathf.Max(tile.CeilingCornerHeight(leftCorner), tile.CeilingCornerHeight(rightCorner))) {
          wallVertices[0].y = (floorAboveCeiling ? portalPoints[1] : Mathf.Max(portalPoints[1], tile.FloorCornerHeight(leftCorner))) * mapScale;
          wallVertices[1].y = tile.CeilingCornerHeight(leftCorner) * mapScale;
          wallVertices[2].y = tile.CeilingCornerHeight(rightCorner) * mapScale;
          wallVertices[3].y = (floorAboveCeiling ? portalPoints[2] : Mathf.Max(portalPoints[2], tile.FloorCornerHeight(leftCorner))) * mapScale;

          lightblend[0] = wallVertices[0].y / wallVertices[1].y;
          lightblend[1] = 1f;
          lightblend[2] = 1f;
          lightblend[3] = wallVertices[3].y / wallVertices[2].y;

          uvs[0].y = (half)(wallVertices[0].y - textureVerticalOffset);
          uvs[1].y = (half)(wallVertices[1].y - textureVerticalOffset);
          uvs[2].y = (half)(wallVertices[2].y - textureVerticalOffset);
          uvs[3].y = (half)(wallVertices[3].y - textureVerticalOffset);

          for (int vertex = 0; vertex < wallVertices.Length; ++vertex) {
            vertices[vertexStart + vertex] = new Vertex {
              pos = wallVertices[vertex],
              uv = uvs[vertex],
              light = lightblend[vertex]
            };
          }

          for (int i = 0; i < indicesTemplate.Length; ++i) index[indexStart + i] = (ushort)(indicesTemplate[i] + vertexStart);

          indiceCount += indicesTemplate.Length;

          vertexStart += wallVertices.Length;
          indexStart += indicesTemplate.Length;
        }

        // Lower border is above floor
        if (Mathf.Max(portalPoints[0], portalPoints[3]) > Mathf.Min(tile.FloorCornerHeight(leftCorner), tile.FloorCornerHeight(rightCorner))) {
          wallVertices[0].y = tile.FloorCornerHeight(leftCorner) * mapScale;
          wallVertices[1].y = Mathf.Min(portalPoints[0], Mathf.Max(tile.CeilingCornerHeight(leftCorner), tile.CeilingCornerHeight(rightCorner))) * mapScale;
          wallVertices[2].y = Mathf.Min(portalPoints[3], Mathf.Max(tile.CeilingCornerHeight(leftCorner), tile.CeilingCornerHeight(rightCorner))) * mapScale;
          wallVertices[3].y = tile.FloorCornerHeight(rightCorner) * mapScale;

          lightblend[0] = 0f;
          lightblend[1] = wallVertices[1].y / (tile.CeilingCornerHeight(leftCorner) * mapScale);
          lightblend[2] = wallVertices[2].y / (tile.CeilingCornerHeight(rightCorner) * mapScale);
          lightblend[3] = 0f;

          uvs[0].y = (half)(wallVertices[0].y - textureVerticalOffset);
          uvs[1].y = (half)(wallVertices[1].y - textureVerticalOffset);
          uvs[2].y = (half)(wallVertices[2].y - textureVerticalOffset);
          uvs[3].y = (half)(wallVertices[3].y - textureVerticalOffset);

          for (int vertex = 0; vertex < wallVertices.Length; ++vertex) {
            vertices[vertexStart + vertex] = new Vertex {
              pos = wallVertices[vertex],
              uv = uvs[vertex],
              light = lightblend[vertex]
            };
          }

          for (int i = 0; i < indicesTemplate.Length; ++i) index[indexStart + i] = (ushort)(indicesTemplate[i] + vertexStart);

          indiceCount += indicesTemplate.Length;

          vertexStart += wallVertices.Length;
        }

        for (int vertex = vertexStart; vertex < VerticesPerViewPart; ++vertex)
          vertices[vertexStart + vertex] = default;

        if (indiceCount > 0) {
          mesh.SetSubMesh(subMeshIndex, new SubMeshDescriptor(originalIndexStart, indiceCount, MeshTopology.Triangles));
          textureIndices[subMeshIndex] = tile.UseAdjacentTexture ? adjacent.WallTexture : tile.WallTexture;
          return 1;
        }
      }

      for (int vertex = vertexStart; vertex < VerticesPerViewPart; ++vertex)
          vertices[vertexStart + vertex] = default;

      return 0;
    }
  }

  internal static class MapUtils {
    public static bool[,] slopeAffectsCorner = new bool[,] {
      { false, false, false, false },
      { false, false, false, false },

      { false, false, false, false },
      { false, false, false, false },
      { false, false, false, false },
      { false, false, false, false },

      { false,  true,  true, false },
      { false, false,  true,  true },
      {  true, false, false,  true },
      {  true,  true, false, false },

      {  true,  true,  true, false },
      { false,  true,  true,  true },
      {  true, false,  true,  true },
      {  true,  true, false,  true },

      { false, false, false,  true },
      {  true, false, false, false },
      { false,  true, false, false },
      { false, false,  true, false }
    };

    public static ushort[][] faceIndices = new ushort[][] {
      new ushort[] { },
      new ushort[] { 0, 1, 2, 2, 3, 0 },

      new ushort[] { 2, 3, 0 },
      new ushort[] { 0, 1, 3 },
      new ushort[] { 0, 1, 2 },
      new ushort[] { 1, 2, 3 },

      new ushort[] { 0, 1, 2, 2, 3, 0 },
      new ushort[] { 0, 1, 2, 2, 3, 0 },
      new ushort[] { 0, 1, 2, 2, 3, 0 },
      new ushort[] { 0, 1, 2, 2, 3, 0 },

      new ushort[] { 0, 1, 3, 1, 2, 3 },
      new ushort[] { 0, 1, 2, 2, 3, 0 },
      new ushort[] { 0, 1, 3, 1, 2, 3 },
      new ushort[] { 0, 1, 2, 2, 3, 0 },

      new ushort[] { 0, 1, 3, 1, 2, 3 },
      new ushort[] { 0, 1, 2, 2, 3, 0 },
      new ushort[] { 0, 1, 3, 1, 2, 3 },
      new ushort[] { 0, 1, 2, 2, 3, 0 }
    };

    public static half2[] UVTemplate = new half2[] {
      half2(half(0f), half(0f)),
      half2(half(0f), half(1f)),
      half2(half(1f), half(1f)),
      half2(half(1f), half(0f))
    };

    public static half2[] UVTemplateFlipped = new half2[] {
      half2(half(1f), half(0f)), 
      half2(half(1f), half(1f)),
      half2(half(0f), half(1f)),
      half2(half(0f), half(0f))
    };

    public static float3[] VerticeTemplate = new float3[] {
      float3(0f, 0f, 0f),
      float3(0f, 0f, 1f),
      float3(1f, 0f, 1f),
      float3(1f, 0f, 0f)
    };
  }
}