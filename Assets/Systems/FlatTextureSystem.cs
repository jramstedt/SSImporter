using System.Collections.Generic;
using SS.ObjectProperties;
using SS.Resources;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Rendering;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace SS.System {
  [UpdateInGroup(typeof(LateSimulationSystemGroup))]
  public partial class FlatTextureSystem : SystemBase {
    private const ushort ArtResourceIdBase = 1350;
    private const ushort DoorResourceIdBase = 2400;
    private const ushort CustomTextureIdBase = 75;
    private const ushort IconResourceIdBase = 78;
    private const ushort GraffitiResourceIdBase = 79;
    private const ushort RepulsorResourceIdBase = 80;
    private const ushort SmallTextureIdBase = 321;

    private Dictionary<string, AsyncOperationHandle<BitmapSet>> bitmapLoaders = new();

    private EntityQuery newFlatTextureQuery;
    private EntityQuery activeFlatTextureQuery;
    private EntityQuery removedFlatTextureQuery;

    private EntityArchetype viewPartArchetype;

    private Resources.ObjectProperties objectProperties;
    private SpriteLibrary spriteLibrary;
    private Texture2D clutTexture;
    private bool Ready = false;

    protected override async void OnCreate() {
      base.OnCreate();

      newFlatTextureQuery = GetEntityQuery(new EntityQueryDesc {
        All = new ComponentType[] { ComponentType.ReadOnly<FlatTextureInfo>() },
        None = new ComponentType[] { ComponentType.ReadOnly<FlatTextureAddedTag>() },
      });

      activeFlatTextureQuery = GetEntityQuery(new EntityQueryDesc {
        All = new ComponentType[] {
          ComponentType.ReadOnly<FlatTextureInfo>(),
          ComponentType.ReadOnly<FlatTextureAddedTag>()
        }
      });

      removedFlatTextureQuery = GetEntityQuery(new EntityQueryDesc {
        All = new ComponentType[] { ComponentType.ReadOnly<FlatTextureAddedTag>() },
        None = new ComponentType[] { ComponentType.ReadOnly<FlatTextureInfo>() },
      });

      viewPartArchetype = World.EntityManager.CreateArchetype(
        typeof(FlatTexturePart),
        typeof(Parent),
        typeof(LocalToWorld),
        typeof(LocalToParent),
        typeof(RenderBounds),
        typeof(RenderMesh)
      );

      objectProperties = await Services.ObjectProperties;
      spriteLibrary = await Services.SpriteLibrary;
      clutTexture = await Services.ColorLookupTableTexture;

      Ready = true;
    }

    private enum ScreenType {
      Alt,
      Custom,
      Text,
      ScrollText
    }

    private string parseScreenData (int data) {
      const int DATA_MASK = 0xFFF;

      const int INDEX_MASK = 0x007F;
      const int TYPE_MASK = 0x0180;
      const int SCALE_MASK = 0x0600;
      const int STYLE_MASK = 0x0800;

      const int RANDOM_TEXT_MAGIC_COOKIE = 0x7F;
      const int REGULAR_STATIC_MAGIC_COOKIE = 0x77;
      const int SHODAN_STATIC_MAGIC_COOKIE = 0x76;

      const int NUM_HACK_CAMERAS = 8;
      const int FIRST_CAMERA_TMAP = 0x78;

      const int NUM_AUTOMAP_MAGIC_COOKIES = 6;
      const int FIRST_AUTOMAP_MAGIC_COOKIE = 0x70;

      data &= DATA_MASK;

      var index = data & INDEX_MASK;
      var type = (ScreenType)((data & TYPE_MASK) >> 7);
      var scale = (data & SCALE_MASK) >> 9;
      var style = (data & STYLE_MASK) == STYLE_MASK ? 2 : 3;

      Debug.Log($"parseScreenData {type}");

      if (type == ScreenType.Alt) {
        return $"{SmallTextureIdBase + index}";
      } else if (type == ScreenType.Custom) {
        if (index >= FIRST_CAMERA_TMAP && index <= (FIRST_CAMERA_TMAP + NUM_HACK_CAMERAS)) {
          var cameraIndex = index - FIRST_CAMERA_TMAP;
          return $"{CustomTextureIdBase}"; // TODO FIXME PLACEHOLDER

          // if (hasCamera(cameraIndex))
          //  ret camera
          // else
          //  ret static
        } else if (index == REGULAR_STATIC_MAGIC_COOKIE || index == SHODAN_STATIC_MAGIC_COOKIE) {
          return $"{CustomTextureIdBase}"; // TODO FIXME PLACEHOLDER
          // ret static
        }

        // if (!HasRes(CustomTextureIdBase + index)) {
        //   ret static
        // } else {
          return $"{CustomTextureIdBase + index}"; 
        // }
      } else if (type == ScreenType.Text) {
        if (index == RANDOM_TEXT_MAGIC_COOKIE) {
          // TODO randomize text
          // TODO DRAW TEXT CANVAS
        } else {
          // TODO DRAW TEXT CANVAS
        }
      } else if (type == ScreenType.ScrollText) {
        // TODO DRAW TEXT CANVAS
      }

      return null;
    }

    protected override void OnUpdate() {
      if (!Ready) return;

      var ecbSystem = World.GetExistingSystem<EndInitializationEntityCommandBufferSystem>();
      var commandBuffer = ecbSystem.CreateCommandBuffer();

      Entities
        .WithAll<FlatTextureInfo, ObjectInstance>()
        .WithNone<FlatTextureAddedTag>()
        .ForEach((Entity entity, in ObjectInstance instanceData) => {
          var baseProperties = objectProperties.BasePropertyData(instanceData);

          string res = null;
          float refWidth = 64f;

          var lightmapped = true;

          if (baseProperties.DrawType == DrawType.TerrainPolygon) {
            const int DESTROYED_SCREEN_ANIM_BASE = 0x1B;
            const int INDIRECTED_STUFF_INDICATOR_MASK = 0x1000;
            const int INDIRECTED_STUFF_DATA_MASK = 0xFFF;

            if (instanceData.Class == ObjectClass.Decoration) {
              var decorationInstance = GetComponent<ObjectInstance.Decoration>(entity);
              var level = GetSingleton<Level>();
              ref var objectInstances = ref level.ObjectInstances.Value;

              var lookupIndex = 0;
              var data = decorationInstance.Data2;
              if (data != 0 /*|| animlist.ObjectIndex == objindex*/) {
                if ((data & INDIRECTED_STUFF_INDICATOR_MASK) != 0) {
                  var dataEntity = objectInstances[(int)data & INDIRECTED_STUFF_DATA_MASK];
                  var databObjectInstance = GetComponent<ObjectInstance>(dataEntity);
                  var dataDecorationInstance = GetComponent<ObjectInstance.Decoration>(dataEntity);

                  lookupIndex = (int)dataDecorationInstance.Data2 + databObjectInstance.Info.CurrentFrame;
                } else {
                  lookupIndex = (int)decorationInstance.Data2 + instanceData.Info.CurrentFrame;
                }
              } else if (data == 0 && instanceData.SubClass == 1) {
                lookupIndex = 0x80;
              }

              if (instanceData.Triple == 0x70207) { // TMAP_TRIPLE
                unsafe {
                  res = $"{0x03E8 + level.TextureMap.blockIndex[lookupIndex]}";
                }

                refWidth = 128f;
              } else if (instanceData.Triple == 0x70208) { // SUPERSCREEN_TRIPLE
                res = parseScreenData(lookupIndex);
                Debug.Log($"SUPERSCREEN_TRIPLE {res}");

                refWidth = 64f;

                if(decorationInstance.Data2 != DESTROYED_SCREEN_ANIM_BASE + 3)
                  lightmapped = false; // screen is full bright
              } else if (instanceData.Triple == 0x70209) { // BIGSCREEN_TRIPLE
                res = parseScreenData(lookupIndex);
                Debug.Log($"BIGSCREEN_TRIPLE  {res}");

                refWidth = 32f;

                if(decorationInstance.Data2 != DESTROYED_SCREEN_ANIM_BASE + 3)
                  lightmapped = false; // screen is full bright
              } else if (instanceData.Triple == 0x70206) { // SCREEN_TRIPLE
                res = parseScreenData(lookupIndex);
                Debug.Log($"SCREEN_TRIPLE  {res}");

                refWidth = 64f;

                if(decorationInstance.Data2 != DESTROYED_SCREEN_ANIM_BASE + 3)
                  lightmapped = false; // screen is full bright
              }
            }
          } else if (baseProperties.DrawType == DrawType.FlatTexture) {
            if (instanceData.Class == ObjectClass.Decoration) {
              if (instanceData.Triple == 0x70203) { // WORDS_TRIPLE
                return;
              } else if (instanceData.Triple == 0x70201) { // ICON_TRIPLE
                res = $"{IconResourceIdBase}:{instanceData.Info.CurrentFrame}";
              } else if (instanceData.Triple == 0x70202) { // GRAF_TRIPLE
                res = $"{GraffitiResourceIdBase}:{instanceData.Info.CurrentFrame}";
              } else if (instanceData.Triple == 0x7020a) { // REPULSWALL_TRIPLE
                res = $"{RepulsorResourceIdBase}:{instanceData.Info.CurrentFrame}";
              }
            } else if (instanceData.Class == ObjectClass.DoorAndGrating) {
              Debug.Log($"{DoorResourceIdBase} {objectProperties.ClassPropertyIndex(instanceData)} : {instanceData.Info.CurrentFrame}");
              res = $"{DoorResourceIdBase + objectProperties.ClassPropertyIndex(instanceData)}:{instanceData.Info.CurrentFrame}";
            }
          }

          if (res == null) {
            var currentFrame = instanceData.Info.CurrentFrame != byte.MaxValue ? instanceData.Info.CurrentFrame : 0;
            var spriteIndex = spriteLibrary.GetSpriteIndex(instanceData, currentFrame);
            res = $"{ArtResourceIdBase}:{spriteIndex}";
          }

          if (!bitmapLoaders.TryGetValue(res, out var loadOp))
            bitmapLoaders.TryAdd(res, loadOp = Addressables.LoadAssetAsync<BitmapSet>(res));

          if (!loadOp.IsDone) return; // Retry on next OnUpdate

          var bitmapSet = loadOp.Result;
          
          var material = new Material(Shader.Find("Universal Render Pipeline/System Shock/CLUT"));
          material.SetTexture(Shader.PropertyToID(@"_BaseMap"), bitmapSet.Texture);
          material.SetTexture(Shader.PropertyToID(@"_CLUT"), clutTexture);
          material.DisableKeyword(@"_SPECGLOSSMAP");
          material.DisableKeyword(@"_SPECULAR_COLOR");
          material.DisableKeyword(@"_GLOSSINESS_FROM_BASE_ALPHA");
          material.DisableKeyword(@"_ALPHAPREMULTIPLY_ON");

          material.EnableKeyword(@"LINEAR");
          if (bitmapSet.Transparent) {
            material.EnableKeyword(@"TRANSPARENCY_ON");
            material.EnableKeyword(@"_ALPHATEST_ON");
            material.renderQueue = 2450;
          } else {
            material.DisableKeyword(@"TRANSPARENCY_ON");
            material.DisableKeyword(@"_ALPHATEST_ON");
          }

          material.SetFloat(@"_BlendOp", (float)UnityEngine.Rendering.BlendOp.Add);
          material.SetFloat(@"_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
          material.SetFloat(@"_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
          material.enableInstancing = true;

          var size = new float2(bitmapSet.Texture.width / refWidth, bitmapSet.Texture.height / refWidth);
          var mesh = BuildPlaneMesh(size / 2f, instanceData.Class == ObjectClass.DoorAndGrating);

          var viewPart = commandBuffer.CreateEntity(viewPartArchetype);
          
          commandBuffer.SetComponent(viewPart, new FlatTexturePart { CurrentFrame = 0 }); // TODO FIXME
          commandBuffer.SetComponent(viewPart, default(LocalToWorld));
          commandBuffer.SetComponent(viewPart, new Parent { Value = entity });
          commandBuffer.SetComponent(viewPart, new LocalToParent { Value = float4x4.identity });
          commandBuffer.SetComponent(viewPart, new RenderBounds { Value = new AABB { Center = mesh.bounds.center, Extents = mesh.bounds.extents } });
          commandBuffer.SetSharedComponent(viewPart, new RenderMesh {
            mesh = mesh,
            material = material,
            subMesh = 0,
            layer = 0,
            castShadows = ShadowCastingMode.On,
            receiveShadows = true,
            needMotionVectorPass = false,
            layerMask = uint.MaxValue
          });

          commandBuffer.AddComponent<FlatTextureAddedTag>(entity);
        })
        .WithoutBurst()
        .Run();

      ecbSystem.AddJobHandleForProducer(Dependency);
    }

    private Mesh BuildPlaneMesh (float2 extent, bool doubleSided) {
      Mesh mesh = new Mesh();
      mesh.vertices = new Vector3[] {
        new Vector3(-extent.x, extent.y, 0f),
        new Vector3(-extent.x, -extent.y, 0f),
        new Vector3(extent.x, -extent.y, 0f),
        new Vector3(extent.x, extent.y, 0f)
      };
      mesh.uv = new Vector2[] {
        new Vector2(0.0f, 1.0f),
        new Vector2(0.0f, 0.0f),
        new Vector2(1.0f, 0.0f),
        new Vector2(1.0f, 1.0f)
      };

      mesh.triangles = new int[] { 0, 1, 2, 2, 3, 0, 2, 1, 0, 0, 3, 2 };

      /*
      mesh.triangles = doubleSided
      ? new int[] { 0, 1, 2, 2, 3, 0, 2, 1, 0, 0, 3, 2 }
      : new int[] { 2, 1, 0, 0, 3, 2 };
      */

      mesh.RecalculateNormals();
      // mesh.RecalculateTangents();
      mesh.RecalculateBounds();

      return mesh;
    }

    protected override void OnDestroy() {
      base.OnDestroy();

      //foreach(var loadOp in bitmapLoaders)
      //  Addressables.Release(loadOp);
    }
  }

  public struct FlatTextureInfo : IComponentData {}

  public struct FlatTexturePart : IComponentData {
    public int CurrentFrame;
  }

  internal struct FlatTextureAddedTag : ISystemStateComponentData { }
}
