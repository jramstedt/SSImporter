using System;
using System.Collections.Generic;
using SS.Resources;
using Unity.Burst;
using Unity.Collections;
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
          ComponentType.ReadOnly<NeedsRebuildTag>()
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

      //var entityCount = this.mapElementQuery.CalculateEntityCount();
      var entities = this.mapElementQuery.ToEntityArray(Allocator.TempJob);
      var entityCount = entities.Length;

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

        levelInfo = levelInfo,
        CommandBuffer = commandBuffer.AsParallelWriter(),
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

    [ReadOnly] public LevelInfo levelInfo;
    [WriteOnly] public EntityCommandBuffer.ParallelWriter CommandBuffer;
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

        BuildMesh(ref tileLocation, ref mapElement, ref meshData, ref textureIndices);

        CommandBuffer.RemoveComponent<NeedsRebuildTag>(batchIndex, entity);
      }
    }

    [BurstCompile]
    private bool2 IsWallTextureFlipped (ref TileLocation tileLocation, ref MapElement texturing) {
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

    [BurstCompile]
    private void BuildMesh (ref TileLocation tileLocation, ref MapElement tile, ref Mesh.MeshData mesh, ref NativeSlice<byte> textureIndices) {
      if (tile.TileType == TileType.Solid) {
        mesh.subMeshCount = 0;
        return;
      }

      mesh.subMeshCount = 2; // TODO dynamic value based on what needs to be created

      mesh.SetIndexBufferParams(6 * mesh.subMeshCount, IndexFormat.UInt16);
      mesh.SetVertexBufferParams(
        4 * mesh.subMeshCount,
        new VertexAttributeDescriptor(VertexAttribute.Position, stream: 0),
        new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float16, 2, 1),
        new VertexAttributeDescriptor(VertexAttribute.Normal, stream: 2),
        new VertexAttributeDescriptor(VertexAttribute.Tangent, stream: 3)
      );

      //for (int i = 0; i < mesh.subMeshCount; ++i)
      //  mesh.SetSubMesh(i, new SubMeshDescriptor(i * 6, 6, MeshTopology.Triangles));

      this.CreatePlane(ref tile, ref mesh, ref textureIndices, 0, false);
      this.CreatePlane(ref tile, ref mesh, ref textureIndices, 1, true);
    }

    private void CreatePlane (ref MapElement tile, ref Mesh.MeshData mesh, ref NativeSlice<byte> textureIndices, int subMeshIndex, bool isCeiling) {
      var pos = mesh.GetVertexData<float3>(0);
      var uv = mesh.GetVertexData<half2>(1);
      var index = mesh.GetIndexData<ushort>();

      var vertexStart = subMeshIndex * 4;
      var indexStart = subMeshIndex * 6;

      for (int corner = 0; corner < 4; ++corner) {
        int cornerHeight = isCeiling ? tile.CeilingCornerHeight(corner) : tile.FloorCornerHeight(corner);
        pos[vertexStart + corner] = MapUtils.VerticeTemplate[corner] + float3(0f, (float)cornerHeight / (float)levelInfo.HeightDivisor, 0f);
      }

      var uvs = MapUtils.UVTemplate.RotateRight(isCeiling ? tile.CeilingRotation : tile.FloorRotation);
      NativeArray<half2>.Copy(uvs, 0, uv, vertexStart, uvs.Length);

      for (int i = indexStart; i < indexStart+6; ++i)
        index[i] = 0;

      ushort[] indicesTemplate = MapUtils.faceIndices[(int)tile.TileType];
      if (isCeiling) // Reverses index order in ceiling
        for (int i = 0; i < indicesTemplate.Length; ++i) index[indexStart + i] = (ushort)(indicesTemplate[indicesTemplate.Length - 1 - i] + vertexStart);
      else
        for (int i = 0; i < indicesTemplate.Length; ++i) index[indexStart + i] = (ushort)(indicesTemplate[i] + vertexStart);

      mesh.SetSubMesh(subMeshIndex, new SubMeshDescriptor(indexStart, indicesTemplate.Length, MeshTopology.Triangles));
      textureIndices[subMeshIndex] = isCeiling ? tile.CeilingTexture : tile.FloorTexture;
    }

    private void CreateWall(ref Mesh.MeshData mesh, ref NativeSlice<byte> textureIndices, int subMeshIndex, ref MapElement tile, int leftCorner, int rightCorner, ref MapElement adjacent, int adjacentLeftCorner, int adjacentRightCorner, bool flip, params TileType[] ignoreTypes) {
      var pos = mesh.GetVertexData<float3>(0);
      var uv = mesh.GetVertexData<half2>(1);
      var index = mesh.GetIndexData<ushort>();

      var vertexStart = subMeshIndex * 4;
      var indexStart = subMeshIndex * 6;
      
      var indicesTemplate = MapUtils.faceIndices[1];
      var vertices = new float3[] {
        MapUtils.VerticeTemplate[leftCorner],
        MapUtils.VerticeTemplate[leftCorner],
        MapUtils.VerticeTemplate[rightCorner],
        MapUtils.VerticeTemplate[rightCorner]
      };

      var uvs = flip ? MapUtils.UVTemplateFlipped : MapUtils.UVTemplate;

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
        vertices[0].y = (float)tile.FloorCornerHeight(leftCorner) * mapScale;
        vertices[1].y = (float)tile.CeilingCornerHeight(leftCorner) * mapScale;
        vertices[2].y = (float)tile.CeilingCornerHeight(rightCorner) * mapScale;
        vertices[3].y = (float)tile.FloorCornerHeight(rightCorner) * mapScale;

        uvs[0].y = (half)(vertices[0].y - textureVerticalOffset);
        uvs[1].y = (half)(vertices[1].y - textureVerticalOffset);
        uvs[2].y = (half)(vertices[2].y - textureVerticalOffset);
        uvs[3].y = (half)(vertices[3].y - textureVerticalOffset);

        NativeArray<float3>.Copy(vertices, 0, pos, vertexStart, vertices.Length);
        NativeArray<half2>.Copy(uvs, 0, uv, vertexStart, uvs.Length);
        NativeArray<ushort>.Copy(indicesTemplate, 0, index, indexStart, indicesTemplate.Length);
      } else { // Possibly two part wall
/*
        List<CombineInstance> partInstances = new List<CombineInstance>();

        int[] portalPoints = new int[] {
                Mathf.Max(floorCornerHeight[leftCorner], otherTile.floorCornerHeight[otherLeftCorner]),
                Mathf.Min(ceilingCornerHeight[leftCorner], otherTile.ceilingCornerHeight[otherLeftCorner]),
                Mathf.Min(ceilingCornerHeight[rightCorner], otherTile.ceilingCornerHeight[otherRightCorner]),
                Mathf.Max(floorCornerHeight[rightCorner], otherTile.floorCornerHeight[otherRightCorner])
            };

        bool floorAboveCeiling = portalPoints[0] > portalPoints[1] ^ portalPoints[3] > portalPoints[2]; // Other corner of ceiling is above and other below floor

        // Upper portal border is below ceiling
        if (!ceilingMoving && Mathf.Min(portalPoints[1], portalPoints[2]) < Mathf.Max(ceilingCornerHeight[leftCorner], ceilingCornerHeight[rightCorner])) {
          vertices[0].y = (floorAboveCeiling ? portalPoints[1] : Mathf.Max(portalPoints[1], floorCornerHeight[leftCorner])) * mapScale;
          vertices[1].y = ceilingCornerHeight[leftCorner] * mapScale;
          vertices[2].y = ceilingCornerHeight[rightCorner] * mapScale;
          vertices[3].y = (floorAboveCeiling ? portalPoints[2] : Mathf.Max(portalPoints[2], floorCornerHeight[leftCorner])) * mapScale;

          uvs[0].y = vertices[0].y - textureVerticalOffset;
          uvs[1].y = vertices[1].y - textureVerticalOffset;
          uvs[2].y = vertices[2].y - textureVerticalOffset;
          uvs[3].y = vertices[3].y - textureVerticalOffset;

          Mesh topMesh = new Mesh();
          topMesh.vertices = vertices;
          topMesh.uv = uvs;
          topMesh.triangles = triangles;

          partInstances.Add(new CombineInstance() {
            mesh = topMesh,
            subMeshIndex = 0,
            transform = Matrix4x4.identity
          });
        }

        // Lower border is above floor
        if (!floorMoving && Mathf.Max(portalPoints[0], portalPoints[3]) > Mathf.Min(floorCornerHeight[leftCorner], floorCornerHeight[rightCorner])) {
          vertices[0].y = floorCornerHeight[leftCorner] * mapScale;
          vertices[1].y = Mathf.Min(portalPoints[0], Mathf.Max(ceilingCornerHeight[leftCorner], ceilingCornerHeight[rightCorner])) * mapScale;
          vertices[2].y = Mathf.Min(portalPoints[3], Mathf.Max(ceilingCornerHeight[leftCorner], ceilingCornerHeight[rightCorner])) * mapScale;
          vertices[3].y = floorCornerHeight[rightCorner] * mapScale;

          uvs[0].y = vertices[0].y - textureVerticalOffset;
          uvs[1].y = vertices[1].y - textureVerticalOffset;
          uvs[2].y = vertices[2].y - textureVerticalOffset;
          uvs[3].y = vertices[3].y - textureVerticalOffset;

          Mesh bottomMesh = new Mesh();
          bottomMesh.vertices = vertices;
          bottomMesh.uv = uvs;
          bottomMesh.triangles = triangles;

          partInstances.Add(new CombineInstance() {
            mesh = bottomMesh,
            subMeshIndex = 0,
            transform = Matrix4x4.identity
          });
        }

        if (partInstances.Count > 0)
          mesh.CombineMeshes(partInstances.ToArray(), true, false);
*/
      }

//      return mesh.vertices.Length > 0 ? mesh : null;
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