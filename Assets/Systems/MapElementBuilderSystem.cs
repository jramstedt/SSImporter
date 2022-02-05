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
    // private EndSimulationEntityCommandBufferSystem ecbSystem;
    private EntityQuery mapElementquery;

    protected override void OnCreate() {
      base.OnCreate();

      mapElementquery = GetEntityQuery(new EntityQueryDesc {
        All = new ComponentType[] {
          ComponentType.ReadOnly<TileLocation>(),
          ComponentType.ReadOnly<MapElement>(),
          ComponentType.ReadWrite<LocalToWorld>(),
          // ComponentType.ReadOnly<TileNeighbour>(),
          ComponentType.ReadOnly<NeedsRebuildTag>()
        }
      });
    }

    protected override void OnUpdate() {
      var ecbSystem = World.GetExistingSystem<EndInitializationEntityCommandBufferSystem>();
      var commandBuffer = ecbSystem.CreateCommandBuffer();

      var entityCount = this.mapElementquery.CalculateEntityCount();
      var meshDataArray = Mesh.AllocateWritableMeshData(entityCount);

      var buildJob = new BuildMapElementMeshJob {
        entityTypeHandle = GetEntityTypeHandle(),
        tileLocationTypeHandle = GetComponentTypeHandle<TileLocation>(true),
        mapElementTypeHandle = GetComponentTypeHandle<MapElement>(true),
        localToWorldTypeHandle = GetComponentTypeHandle<LocalToWorld>(false),
        // tileNeighbourBufferTypeHandle = GetBufferTypeHandle<TileNeighbour>(true),
        allMapElements = GetComponentDataFromEntity<MapElement>(true),

        levelInfo = GetSingleton<LevelInfo>(),
        CommandBuffer = commandBuffer.AsParallelWriter(),
        meshDataArray = meshDataArray,
      };

      var buildMapElements = buildJob.ScheduleParallel(mapElementquery, dependsOn: Dependency);
      buildMapElements.Complete();

      ecbSystem.AddJobHandleForProducer(buildMapElements);
      Dependency = buildMapElements;

      var meshes = new Mesh[entityCount];
      for (var i = 0; i < entityCount; ++i)
        meshes[i] = new Mesh();

      Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, meshes);

      var mat = new Material(Shader.Find("Unlit/Color"));
      mat.enableInstancing = true;

      var entities = this.mapElementquery.ToEntityArray(Allocator.Temp);
      for (int i = 0; i < entityCount; ++i) {
        var entity = entities[i];

        var mesh = meshes[i];
        mesh.RecalculateNormals();
        // mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        mesh.UploadMeshData(true);

        // commandBuffer.AddComponent(entity, new LocalToWorld { Value = Unity.Mathematics.float4x4.Translate(float3(0f, 0f, 0f)) });
        commandBuffer.AddComponent(entity, default(RenderBounds));
        commandBuffer.AddSharedComponent(entity, new RenderMesh {
          mesh = mesh,
          material = mat,
          subMesh = 0,
          layer = 0,
          castShadows = ShadowCastingMode.On,
          receiveShadows = true,
          needMotionVectorPass = false
        });
      }
      entities.Dispose();
    }
  }

  struct BuildMapElementMeshJob : IJobEntityBatchWithIndex {
    [ReadOnly] public EntityTypeHandle entityTypeHandle;

    [ReadOnly] public ComponentTypeHandle<TileLocation> tileLocationTypeHandle;
    [ReadOnly] public ComponentTypeHandle<MapElement> mapElementTypeHandle;
    public ComponentTypeHandle<LocalToWorld> localToWorldTypeHandle;
    // [ReadOnly] public BufferTypeHandle<TileNeighbour> tileNeighbourBufferTypeHandle;
    [ReadOnly] public ComponentDataFromEntity<MapElement> allMapElements;

    [ReadOnly] public LevelInfo levelInfo;
    [WriteOnly] public EntityCommandBuffer.ParallelWriter CommandBuffer;
    public Mesh.MeshDataArray meshDataArray;

    [BurstCompile]
    public void Execute(ArchetypeChunk batchInChunk, int batchIndex, int indexOfFirstEntityInQuery) {
      var entities = batchInChunk.GetNativeArray(entityTypeHandle);
      var tileLocations = batchInChunk.GetNativeArray(tileLocationTypeHandle);
      var localToWorld = batchInChunk.GetNativeArray(localToWorldTypeHandle);
      var mapElements = batchInChunk.GetNativeArray(mapElementTypeHandle);

      // var neighbourEntitiesVector = batchInChunk.GetBufferAccessor(tileNeighbourBufferTypeHandle);

      // Mesh.ApplyAndDisposeWritableMeshData()

      for (int i = 0; i < batchInChunk.Count; ++i) {
        var entity = entities[i];
        var meshData = meshDataArray[indexOfFirstEntityInQuery + i];
        
        var tileLocation = tileLocations[i];
        var mapElement = mapElements[i];

        // var neighbourEntities = neighbourEntitiesVector[i];

        // var test = 0f;
        // for (int n = 0; n < neighbourEntities.Length; ++n) {
        //   var neighbourMapElement = allMapElements[neighbourEntities[n].Entity];
        //   test += neighbourMapElement.FloorHeight;
        // }

        localToWorld[i] = new LocalToWorld { Value = Unity.Mathematics.float4x4.Translate(float3(tileLocation.X, 0f, tileLocation.Y)) };

        BuildMesh(ref levelInfo, ref mapElement, ref meshData);

/*
        CommandBuffer.SetComponent(batchIndex, entities[i], new RenderMesh {

        })
*/
        CommandBuffer.RemoveComponent<NeedsRebuildTag>(batchIndex, entity);
      }
    }

    private void BuildMesh (ref LevelInfo levelInfo, ref MapElement tile, ref Mesh.MeshData mesh) {
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

      this.CreatePlane(ref levelInfo, ref tile, ref mesh, 0, false);
      this.CreatePlane(ref levelInfo, ref tile, ref mesh, 1, true);

      // TODO WALLS!
      /*
      Wall
        - Entity A
        - Entity B
        - allMapElements = GetComponentDataFromEntity<MapElement>(true)
        - rebuildElements = GetComponentDataFromEntity<NeedsRebuildTag>(true)
      DiagonalWall
        - Entity A
        - allMapElements = GetComponentDataFromEntity<MapElement>(true)
        - rebuildElements = GetComponentDataFromEntity<NeedsRebuildTag>(true)
      Floor
        - Entity A
        - allMapElements = GetComponentDataFromEntity<MapElement>(true)
        - rebuildElements = GetComponentDataFromEntity<NeedsRebuildTag>(true)
      Ceiling
        - Entity A
        - allMapElements = GetComponentDataFromEntity<MapElement>(true)
        - rebuildElements = GetComponentDataFromEntity<NeedsRebuildTag>(true)
      */
    }

    private void CreatePlane (ref LevelInfo levelInfo, ref MapElement tile, ref Mesh.MeshData mesh, int subMeshIndex, bool isCeiling) {
      var pos = mesh.GetVertexData<float3>(0);
      var uv = mesh.GetVertexData<half2>(1);
      var index = mesh.GetIndexData<ushort>();

      var vertexStart = subMeshIndex * 4;
      var indexStart = subMeshIndex * 6;

      for (int corner = 0; corner < 4; ++corner) {
        int cornerHeight = isCeiling ? tile.FloorCornerHeight(corner) : tile.FloorCornerHeight(corner);
        pos[vertexStart + corner] = MapUtils.VerticeTemplate[corner] + float3(0f, cornerHeight / levelInfo.HeightDivisor, 0f);
      }

      var uvs = MapUtils.UVTemplate.RotateRight(isCeiling ? tile.CeilingRotation : tile.FloorRotation);
      NativeArray<half2>.Copy(uvs, 0, uv, vertexStart, uvs.Length);

      for (int i = indexStart; i < indexStart+6; ++i)
        index[i] = 0;

      short[] indicesTemplate = MapUtils.faceIndices[(int)tile.TileType];
      if (isCeiling) // Reverses index order in ceiling
        for (int i = 0; i < indicesTemplate.Length; ++i) index[indexStart + i] = (ushort)(indicesTemplate[indicesTemplate.Length - 1 - i] + vertexStart);
      else
        for (int i = 0; i < indicesTemplate.Length; ++i) index[indexStart + i] = (ushort)(indicesTemplate[i] + vertexStart);

      mesh.SetSubMesh(subMeshIndex, new SubMeshDescriptor(indexStart, indicesTemplate.Length, MeshTopology.Triangles));
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

    public static short[][] faceIndices = new short[][] {
      new short[] { },
      new short[] { 0, 1, 2, 2, 3, 0 },

      new short[] { 2, 3, 0 },
      new short[] { 0, 1, 3 },
      new short[] { 0, 1, 2 },
      new short[] { 1, 2, 3 },

      new short[] { 0, 1, 2, 2, 3, 0 },
      new short[] { 0, 1, 2, 2, 3, 0 },
      new short[] { 0, 1, 2, 2, 3, 0 },
      new short[] { 0, 1, 2, 2, 3, 0 },

      new short[] { 0, 1, 3, 1, 2, 3 },
      new short[] { 0, 1, 2, 2, 3, 0 },
      new short[] { 0, 1, 3, 1, 2, 3 },
      new short[] { 0, 1, 2, 2, 3, 0 },

      new short[] { 0, 1, 3, 1, 2, 3 },
      new short[] { 0, 1, 2, 2, 3, 0 },
      new short[] { 0, 1, 3, 1, 2, 3 },
      new short[] { 0, 1, 2, 2, 3, 0 }
    };

    public static half2[] UVTemplate = new half2[] {
        half2(half(0f), half(0f)),
        half2(half(0f), half(1f)),
        half2(half(1f), half(1f)),
        half2(half(1f), half(0f))
    };

    public static float2[] UVTemplateFlipped = new float2[] {
        float2(1f, 0f), float2(1f, 1f), float2(0f, 1f), float2(0f, 0f)
    };

    public static float3[] VerticeTemplate = new float3[] {
      float3(0f, 0f, 0f),
      float3(0f, 0f, 1f),
      float3(1f, 0f, 1f),
      float3(1f, 0f, 0f)
    };
  }
}