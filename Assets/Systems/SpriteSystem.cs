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
using static SS.MeshUtils;
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
    
    private ComponentLookup<LocalToWorld> localToWorldRO;

    private BlobAssetReference<ObjectDatas> objectProperties;
    private RenderMeshDescription renderMeshDescription;

    private NativeArray<ushort> spriteBase;
    private NativeArray<ushort> spriteIndices;
    private NativeArray<SpriteMesh> spriteMeshes;

    protected override async void OnCreate() {
      base.OnCreate();

      RequireForUpdate<AsyncLoadTag>();

      localToWorldRO = GetComponentLookup<LocalToWorld>(true);

      spriteBase = new NativeArray<ushort>(Base.NUM_OBJECT, Allocator.Persistent);
      spriteIndices = new NativeArray<ushort>(Base.NUM_OBJECT * 8, Allocator.Persistent);
      spriteMeshes = new NativeArray<SpriteMesh>(Base.NUM_OBJECT * 8, Allocator.Persistent);

      newSpriteQuery = new EntityQueryBuilder(Allocator.Temp)
        .WithAll<SpriteInfo, ObjectInstance>()
        .WithNone<SpriteAddedTag>()
        .Build(this);

      activeSpriteQuery = new EntityQueryBuilder(Allocator.Temp)
        .WithAll<SpriteInfo, SpriteAddedTag>()
        .Build(this);

      removedSpriteQuery = new EntityQueryBuilder(Allocator.Temp)
        .WithAll<SpriteAddedTag>()
        .WithNone<SpriteInfo>()
        .Build(this);

      viewPartArchetype = World.EntityManager.CreateArchetype(stackalloc[] {
        ComponentType.ReadWrite<SpritePart>(),
        
        ComponentType.ReadWrite<LocalTransform>(),
        ComponentType.ReadWrite<Parent>(),
        
        ComponentType.ReadWrite<LocalToWorld>(),
        ComponentType.ReadWrite<RenderBounds>(),
      });

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
          var materialID = materialProviderSystem.GetMaterial(ArtResourceIdBase, artIndex, true, false, true);
          var bitmapDesc = await materialProviderSystem.GetBitmapDesc(materialID);

          var mesh = new Mesh();

          spriteMeshes[bitmapIndex] = new SpriteMesh {
            Material = materialID,
            Mesh = entitiesGraphicsSystem.RegisterMesh(mesh),
            AnchorPoint = bitmapDesc.AnchorPoint
          };

          spriteIndices[bitmapIndex] = artIndex;

          BuildPlaneMesh(mesh, bitmapDesc, 1f, false, false);

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
      
      {
        var commandBuffer = ecbSystem.CreateCommandBuffer();
        
        using var newEntities = newSpriteQuery.ToEntityArray(Allocator.Temp);
        using var instanceDatas = newSpriteQuery.ToComponentDataArray<ObjectInstance>(Allocator.Temp);
        for (var index = 0; index < newEntities.Length; ++index) {
          var entity = newEntities[index];
          var instanceData = instanceDatas[index];
          
          var currentFrame = instanceData.Info.CurrentFrame != -1 ? instanceData.Info.CurrentFrame : 0;
          var startIndex = spriteBase[objectProperties.Value.BasePropertyIndex(instanceData)];
          var spriteMesh = spriteMeshes[startIndex + currentFrame];
          var baseData = objectProperties.Value.BasePropertyData(instanceData);

          var scale = (float)(2048 / 3) / ushort.MaxValue;
          var radius = (float)baseData.Radius / Base.PHYSICS_RADIUS_UNIT;

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

          commandBuffer.SetComponent(viewPart, new Parent { Value = entity });
          commandBuffer.SetComponent(viewPart, LocalTransform.FromPositionRotationScale(float3(0f, -radius, 0f), Unity.Mathematics.quaternion.identity, scale));
        }
        commandBuffer.AddComponent<SpriteAddedTag>(newEntities);
      }

      if (Camera.main != null) {
        var towardsCameraRotation = Unity.Mathematics.quaternion.LookRotation(Camera.main.transform.forward, Vector3.up);
        localToWorldRO.Update(this);
        new RotateSpritesJob { LocalToWorldRO = localToWorldRO, TowardsCameraRotation = towardsCameraRotation }.ScheduleParallel();
      }
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
    
    [BurstCompile]
    [WithAll(typeof(SpritePart), typeof(LocalTransform), typeof(Parent))]
    private partial struct RotateSpritesJob : IJobEntity {
      [ReadOnly] public ComponentLookup<LocalToWorld> LocalToWorldRO;
      [ReadOnly] public quaternion TowardsCameraRotation;

      private void Execute(ref LocalTransform localTransform, in Parent parent) {
        if (parent.Value == Entity.Null) return;

        var parentTransformRef = LocalToWorldRO.GetRefRO(parent.Value);
        localTransform.Rotation = mul(TowardsCameraRotation, inverse(parentTransformRef.ValueRO.Rotation));
      }
    }

    private struct AsyncLoadTag : IComponentData { }
  }

  public struct SpriteMesh {
    public BatchMaterialID Material;
    public BatchMeshID Mesh;
    public Vector2Int AnchorPoint;
  }

  public struct SpriteInfo : IComponentData { }

  public struct SpritePart : IComponentData { }

  internal struct SpriteAddedTag : ICleanupComponentData { }
}
