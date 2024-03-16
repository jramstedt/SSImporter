using SS.Data;
using SS.ObjectProperties;
using SS.Resources;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;
using static SS.TextureUtils;
using static Unity.Mathematics.math;

namespace SS.System {
  [BurstCompile]
  [CreateAfter(typeof(MaterialProviderSystem))]
  [UpdateInGroup(typeof(VariableRateSimulationSystemGroup))]
  public partial class SpriteSystem : SystemBase {
    private EntityQuery newSpriteQuery;
    private EntityQuery activeSpriteQuery;
    private EntityQuery removedSpriteQuery;

    private EntityArchetype viewPartArchetype;

    private BlobAssetReference<ObjectDatas> objectProperties;
    private RenderMeshDescription renderMeshDescription;

    private NativeArray<ushort> spriteBase;
    private NativeArray<ushort> spriteIndices;
    private NativeArray<SpriteMesh> spriteMeshes;

    protected override async void OnCreate() {
      base.OnCreate();

      RequireForUpdate<AsyncLoadTag>();

      spriteBase = new NativeArray<ushort>(Base.NUM_OBJECT, Allocator.Persistent);
      spriteIndices = new NativeArray<ushort>(Base.NUM_OBJECT * 8, Allocator.Persistent);
      spriteMeshes = new NativeArray<SpriteMesh>(Base.NUM_OBJECT * 8, Allocator.Persistent);

      newSpriteQuery = new EntityQueryBuilder(Allocator.Temp)
        .WithAll<SpriteInfo>()
        .WithNone<SpriteAddedTag>()
        .Build(this);

      activeSpriteQuery = new EntityQueryBuilder(Allocator.Temp)
        .WithAll<SpriteInfo, SpriteAddedTag>()
        .Build(this);

      removedSpriteQuery = new EntityQueryBuilder(Allocator.Temp)
        .WithAll<SpriteAddedTag>()
        .WithNone<SpriteInfo>()
        .Build(this);

      viewPartArchetype = World.EntityManager.CreateArchetype(
        typeof(SpritePart),

        typeof(LocalTransform),
        typeof(Parent),

        typeof(LocalToWorld),
        typeof(RenderBounds)
      );

      renderMeshDescription = new RenderMeshDescription(
        shadowCastingMode: ShadowCastingMode.Off,
        receiveShadows: false,
        staticShadowCaster: false
      );

      objectProperties = (await Services.ObjectProperties).ObjectDatasBlobAsset;

      var entitiesGraphicsSystem = World.GetOrCreateSystemManaged<EntitiesGraphicsSystem>();
      var materialProviderSystem = World.GetOrCreateSystemManaged<MaterialProviderSystem>();

      ushort bitmapIndex = 1;
      ushort artIndex = 1;
      for (var i = 0; i < Base.NUM_OBJECT; ++i) {
        var baseData = objectProperties.Value.BasePropertyData(i);
        var frameCount = baseData.BitmapFrameCount + 1;

        spriteBase[i] = bitmapIndex;

        ++artIndex; // Skip 2D icon

        for (var j = 0; j < frameCount; ++j) {
          var materialID = materialProviderSystem.GetMaterial(ArtResourceIdBase, artIndex, true, false);
          var bitmapDesc = await materialProviderSystem.GetBitmapDesc(materialID);

          var mesh = new Mesh();

          spriteMeshes[bitmapIndex] = new SpriteMesh {
            Material = materialID,
            Mesh = entitiesGraphicsSystem.RegisterMesh(mesh),
            AnchorPoint = bitmapDesc.AnchorPoint
          };

          spriteIndices[bitmapIndex] = artIndex;

          BuildSpriteMesh(mesh, bitmapDesc);

          ++artIndex;
          ++bitmapIndex;
        }

        ++artIndex; // Skip editor icon
      }

      EntityManager.AddComponent<AsyncLoadTag>(SystemHandle);
    }

    protected override void OnDestroy() {
      base.OnDestroy();

      spriteBase.Dispose();
      spriteIndices.Dispose();
      spriteMeshes.Dispose();
    }

    protected override void OnUpdate() {
      var ecbSystem = World.GetExistingSystemManaged<EndVariableRateSimulationEntityCommandBufferSystem>();

      var objectProperties = this.objectProperties;
      var spriteBase = this.spriteBase;
      var spriteMeshes = this.spriteMeshes;
      var commandBuffer = ecbSystem.CreateCommandBuffer().AsParallelWriter();

      Entities
        .WithAll<SpriteInfo, ObjectInstance>()
        .WithNone<SpriteAddedTag>()
        .WithReadOnly(spriteBase)
        .WithReadOnly(spriteMeshes)
        .ForEach((Entity entity, int entityInQueryIndex, in ObjectInstance instanceData) => {
          var currentFrame = instanceData.Info.CurrentFrame != -1 ? instanceData.Info.CurrentFrame : 0;
          var startIndex = spriteBase[objectProperties.Value.BasePropertyIndex(instanceData)];
          var spriteMesh = spriteMeshes[startIndex + currentFrame];
          var baseData = objectProperties.Value.BasePropertyData(instanceData);

          var scale = (float)(2048 / 3) / ushort.MaxValue;
          var radius = (float)baseData.Radius / MapElement.PHYSICS_RADIUS_UNIT;

          if (spriteMesh.AnchorPoint.x > 0 || spriteMesh.AnchorPoint.y > 0)
            radius = 0f;

          if (baseData.IsDoubleSize)
            scale *= 2f;

          var viewPart = EntityManager.CreateEntity(viewPartArchetype);
          RenderMeshUtility.AddComponents(
            viewPart,
            EntityManager,
            renderMeshDescription,
            new MaterialMeshInfo {
              MeshID = spriteMesh.Mesh,
              MaterialID = spriteMesh.Material,
              SubMesh = 0
            }
          );

          commandBuffer.SetComponent(entityInQueryIndex, viewPart, new SpritePart { CurrentFrame = currentFrame });
          commandBuffer.SetComponent(entityInQueryIndex, viewPart, new Parent { Value = entity });
          commandBuffer.SetComponent(entityInQueryIndex, viewPart, LocalTransform.FromPositionRotationScale(float3(0f, -radius, 0f), Unity.Mathematics.quaternion.identity, scale));

          commandBuffer.AddComponent<SpriteAddedTag>(entityInQueryIndex, entity);
        })
        .WithStructuralChanges()
        .Run();

      var towardsCameraRotation = Unity.Mathematics.quaternion.LookRotation(-Camera.main.transform.forward, Vector3.up);

      Entities
        .WithAll<SpritePart, LocalTransform, Parent>()
        .ForEach((ref LocalTransform localTransform, in Parent parent) => {
          if (parent.Value == Entity.Null) return;

          var parentTransform = SystemAPI.GetComponent<LocalToWorld>(parent.Value);
          localTransform.Rotation = mul(towardsCameraRotation, inverse(parentTransform.Rotation));
        })
        .ScheduleParallel();
    }

    [BurstCompile]
    public ushort GetSpriteIndex(Triple triple, int frame = 0) {
      var startIndex = spriteBase[objectProperties.Value.BasePropertyIndex(triple)];
      return spriteIndices[startIndex + frame];
    }

    [BurstCompile]
    public SpriteMesh GetSprite(Triple triple, int frame = 0) {
      var startIndex = spriteBase[objectProperties.Value.BasePropertyIndex(triple)];
      return spriteMeshes[startIndex + frame];
    }

    // TODO almost identical to one in FlatTextureSystem
    private void BuildSpriteMesh(Mesh mesh, BitmapDesc bitmapDescription) {
      var pivot = bitmapDescription.AnchorPoint;

      var width = bitmapDescription.Size.x;
      var height = bitmapDescription.Size.y;

      if (pivot.x <= 0 && pivot.y <= 0) {
        pivot.x = width >> 1;
        pivot.y = height - 1;
      }

      mesh.SetVertexBufferParams(4,
        new VertexAttributeDescriptor(VertexAttribute.Position),
        new VertexAttributeDescriptor(VertexAttribute.Normal),
        new VertexAttributeDescriptor(VertexAttribute.Tangent),
        new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float16, 2),
        new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 1)
      );

      mesh.SetVertexBufferData(new[] {
        new Vertex { pos = float3(-pivot.x, pivot.y, 0f),                 uv = half2(half(0f), half(1f)), light = 1f },
        new Vertex { pos = float3(-pivot.x, -(height-pivot.y), 0f),       uv = half2(half(0f), half(0f)), light = 0f },
        new Vertex { pos = float3(width-pivot.x, -(height-pivot.y), 0f),  uv = half2(half(1f), half(0f)), light = 0f },
        new Vertex { pos = float3(width-pivot.x, pivot.y, 0f),            uv = half2(half(1f), half(1f)), light = 1f },
      }, 0, 0, 4);

      mesh.subMeshCount = 1;

      mesh.SetIndexBufferParams(6, IndexFormat.UInt16);
      mesh.SetIndexBufferData(new ushort[] { 0, 1, 2, 2, 3, 0 }, 0, 0, 6);
      mesh.SetSubMesh(0, new SubMeshDescriptor(0, 6, MeshTopology.Triangles));

      mesh.RecalculateNormals();
      // mesh.RecalculateTangents();
      mesh.RecalculateBounds();
      mesh.UploadMeshData(true);
    }

    private struct AsyncLoadTag : IComponentData { }
  }

  public struct SpriteMesh {
    public BatchMaterialID Material;
    public BatchMeshID Mesh;
    public Vector2Int AnchorPoint;
  }

  public struct SpriteInfo : IComponentData { }

  public struct SpritePart : IComponentData {
    public int CurrentFrame;
  }

  internal struct SpriteAddedTag : ICleanupComponentData { }
}
