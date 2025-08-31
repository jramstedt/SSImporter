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
    private EntityQuery animatedSpriteQuery;
    private EntityQuery removedSpriteQuery;

    private EntityArchetype viewPartArchetype;
    
    private ComponentLookup<LocalToWorld> localToWorldRO;
    private BufferLookup<Child> childLookupRO;

    private BlobAssetReference<ObjectPropertiesBlob> objectProperties;
    private RenderMeshDescription renderMeshDescription;

    private NativeArray<ushort> spriteBase;
    private NativeArray<ushort> spriteIndices;
    private NativeArray<SpriteMesh> spriteMeshes;

    protected override async void OnCreate() {
      base.OnCreate();

      RequireForUpdate<AsyncLoadTag>();

      localToWorldRO = GetComponentLookup<LocalToWorld>(true);
      childLookupRO = GetBufferLookup<Child>(true);

      spriteBase = new NativeArray<ushort>(Base.NUM_OBJECT, Allocator.Persistent);
      spriteIndices = new NativeArray<ushort>(Base.NUM_OBJECT * 8, Allocator.Persistent);
      spriteMeshes = new NativeArray<SpriteMesh>(Base.NUM_OBJECT * 8, Allocator.Persistent);

      newSpriteQuery = new EntityQueryBuilder(Allocator.Temp)
        .WithAll<SpriteInfo, ObjectInstance>()
        .WithNone<SpriteAddedTag>()
        .Build(this);

      animatedSpriteQuery = new EntityQueryBuilder(Allocator.Temp)
        .WithAll<SpriteInfo, ObjectInstance, SpriteAddedTag, AnimatedTag>()
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

        for (var frameIndex = 0; frameIndex < frameCount; ++frameIndex) {
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

    private void CalculateSprite(in ObjectInstance instanceData, out SpriteMesh spriteMesh, out float radius, out float scale) {
      var currentFrame = instanceData.Info.CurrentFrame != -1 ? instanceData.Info.CurrentFrame : 0;
      var startIndex = spriteBase[objectProperties.Value.BasePropertyIndex(instanceData)];
      spriteMesh = spriteMeshes[startIndex + currentFrame];
      var baseData = objectProperties.Value.BasePropertyData(instanceData);
          
      scale = (float)(2048 / 3) / ushort.MaxValue;
          
      if (spriteMesh.AnchorPoint.x > 0 || spriteMesh.AnchorPoint.y > 0)
        radius = 0f;
      else
        radius = (float)baseData.Radius / Base.PHYSICS_RADIUS_UNIT;

      if (baseData.IsDoubleSize)
        scale *= 2f;
    }

    protected override void OnUpdate() {
      childLookupRO.Update(this);
      
      var ecbSystem = World.GetExistingSystemManaged<EndVariableRateSimulationEntityCommandBufferSystem>();
      var commandBuffer = ecbSystem.CreateCommandBuffer();

      {
        var animatedEntities = animatedSpriteQuery.ToEntityArray(WorldUpdateAllocator);
        var instanceDatas = animatedSpriteQuery.ToComponentDataArray<ObjectInstance>(WorldUpdateAllocator);

        for (var index = 0; index < animatedEntities.Length; ++index) {
          var entity = animatedEntities[index];
          
          CalculateSprite(instanceDatas[index], out var spriteMesh, out var radius, out var scale);

          if (childLookupRO.TryGetBuffer(entity, out DynamicBuffer<Child> children)) {
            // TODO should use AddComponents to make sure bounds and other stuff is updated?

            var viewPart = children[0].Value;
            commandBuffer.SetComponent(viewPart, new MaterialMeshInfo { // TODO CACHE THIS IN SPRITE DATA?
              MeshID = spriteMesh.Mesh,
              MaterialID = spriteMesh.Material,
              SubMesh = 0
            });
            commandBuffer.SetComponent(viewPart, new SpritePart { Radius = radius, Scale = scale });
            commandBuffer.SetComponentEnabled<AnimatedTag>(entity, false);
          }
        }
      }
      
      {
        var newEntities = newSpriteQuery.ToEntityArray(WorldUpdateAllocator);
        var instanceDatas = newSpriteQuery.ToComponentDataArray<ObjectInstance>(WorldUpdateAllocator);
        
        for (var index = 0; index < newEntities.Length; ++index) {
          var entity = newEntities[index];
          var instanceData = instanceDatas[index];
          
          CalculateSprite(instanceData, out var spriteMesh, out var radius, out var scale);
          
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
          commandBuffer.SetComponent(viewPart, new SpritePart { Radius = radius, Scale = scale });
          
          if (instanceData.Class == ObjectClass.Animating)
            commandBuffer.AddComponent<AnimatedTag>(entity);
        }
        commandBuffer.AddComponent<SpriteAddedTag>(newEntities);
      }

      if (Camera.main != null) {
        var towardsCameraRotation = Unity.Mathematics.quaternion.LookRotation(Camera.main.transform.forward, Vector3.up);
        localToWorldRO.Update(this);
        Dependency = new RotateSpritesJob {
          LocalToWorldRO = localToWorldRO,
          TowardsCameraRotation = towardsCameraRotation
        }.ScheduleParallel(Dependency);
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
    private partial struct RotateSpritesJob : IJobEntity {
      [ReadOnly] public ComponentLookup<LocalToWorld> LocalToWorldRO;
      [ReadOnly] public quaternion TowardsCameraRotation;

      private void Execute(ref LocalTransform localTransform, in SpritePart spritePart, in Parent parent) {
        if (parent.Value == Entity.Null) return;
        
        var parentTransformRef = LocalToWorldRO.GetRefRO(parent.Value);
        localTransform.Position = float3(0f, -spritePart.Radius, 0f);
        localTransform.Rotation = mul(inverse(parentTransformRef.ValueRO.Rotation), TowardsCameraRotation);
        localTransform.Scale = spritePart.Scale;
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

  public struct SpritePart : IComponentData {
    public float Radius;
    public float Scale;
  }

  internal struct SpriteAddedTag : ICleanupComponentData { }
}
