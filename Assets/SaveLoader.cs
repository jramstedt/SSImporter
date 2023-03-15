using SS.ObjectProperties;
using SS.System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Authoring;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace SS.Resources {
  public static class SaveLoader {
    private const ushort SaveGameResourceIdBase = 4000;
    private const ushort ModelResourceIdBase = 2300;
    private const ushort NumResourceIdsPerLevel = 100;

    private static ushort ResourceIdFromLevel(byte level) => (ushort)(SaveGameResourceIdBase + (level * NumResourceIdsPerLevel));

    public static async Task<World> LoadMap(byte mapId, string dataPath, string saveGameFile) {
      var loadOp = Addressables.ResourceManager.ProvideResource<ResourceFile>(new ResourceLocationBase(@"savegame", $"{dataPath}\\{saveGameFile}", typeof(ResourceFileProvider).FullName, typeof(ResourceFile)));
      var saveData = await loadOp.Task;

      var palette = await Services.Palette.Task;
      var shadetable = await Services.ShadeTable.Task;
      var allTextureProperties = await Services.TextureProperties.Task;
      var objectProperties = await Services.ObjectProperties.Task;

      ushort resourceId = ResourceIdFromLevel(mapId);
      var hackerState = saveData.GetResourceData<Hacker>((ushort)(SaveGameResourceIdBase + 1));
      var levelInfo = saveData.GetResourceData<LevelInfo>((ushort)(0x0004 + resourceId));
      var tileMap = ReadMapElements(saveData.GetResourceData((ushort)(0x0005 + resourceId), 0), levelInfo);
      var schedules = saveData.GetResourceData<ScheduleEvent>((ushort)(0x0006 + resourceId));
      var textureMap = saveData.GetResourceData<TextureMap>((ushort)(0x0007 + resourceId));

      var objectInstances = saveData.GetResourceDataArray<ObjectInstance>((ushort)(0x008 + resourceId));
      // 0x0009 Cross reference table

      var weaponInstances = saveData.GetResourceDataArray<ObjectInstance.Weapon>((ushort)(0x000A + resourceId));
      var ammunitionInstances = saveData.GetResourceDataArray<ObjectInstance.Ammunition>((ushort)(0x000B + resourceId));
      var projectileInstances = saveData.GetResourceDataArray<ObjectInstance.Projectile>((ushort)(0x000C + resourceId));
      var explosiveInstances = saveData.GetResourceDataArray<ObjectInstance.Explosive>((ushort)(0x000D + resourceId));
      var dermalInstances = saveData.GetResourceDataArray<ObjectInstance.DermalPatch>((ushort)(0x000E + resourceId));
      var hardwareInstances = saveData.GetResourceDataArray<ObjectInstance.Hardware>((ushort)(0x000F + resourceId));
      var softwareLogInstances = saveData.GetResourceDataArray<ObjectInstance.SoftwareAndLog>((ushort)(0x0010 + resourceId));
      var decorationInstances = saveData.GetResourceDataArray<ObjectInstance.Decoration>((ushort)(0x0011 + resourceId));
      var itemInstances = saveData.GetResourceDataArray<ObjectInstance.Item>((ushort)(0x0012 + resourceId));
      var interfaceInstances = saveData.GetResourceDataArray<ObjectInstance.Interface>((ushort)(0x0013 + resourceId));
      var doorGratingInstances = saveData.GetResourceDataArray<ObjectInstance.DoorAndGrating>((ushort)(0x0014 + resourceId));
      var animatedInstances = saveData.GetResourceDataArray<ObjectInstance.Animated>((ushort)(0x0015 + resourceId));
      var triggerInstances = saveData.GetResourceDataArray<ObjectInstance.Trigger>((ushort)(0x0016 + resourceId));
      var containerInstances = saveData.GetResourceDataArray<ObjectInstance.Container>((ushort)(0x0017 + resourceId));
      var enemyInstances = saveData.GetResourceDataArray<ObjectInstance.Enemy>((ushort)(0x0018 + resourceId));

      var weaponTemplates = saveData.GetResourceDataArray<ObjectInstance.Weapon>((ushort)(0x0019 + resourceId));
      var ammunitionTemplates = saveData.GetResourceDataArray<ObjectInstance.Ammunition>((ushort)(0x001A + resourceId));
      var projectileTemplates = saveData.GetResourceDataArray<ObjectInstance.Projectile>((ushort)(0x001B + resourceId));
      var explosiveTemplates = saveData.GetResourceDataArray<ObjectInstance.Explosive>((ushort)(0x0001C + resourceId));
      var dermalTemplates = saveData.GetResourceDataArray<ObjectInstance.DermalPatch>((ushort)(0x001D + resourceId));
      var hardwareTemplates = saveData.GetResourceDataArray<ObjectInstance.Hardware>((ushort)(0x001E + resourceId));
      var softwareLogTemplates = saveData.GetResourceDataArray<ObjectInstance.SoftwareAndLog>((ushort)(0x001F + resourceId));
      var decorationTemplates = saveData.GetResourceDataArray<ObjectInstance.Decoration>((ushort)(0x0020 + resourceId));
      var itemTemplates = saveData.GetResourceDataArray<ObjectInstance.Item>((ushort)(0x0021 + resourceId));
      var interfaceTemplates = saveData.GetResourceDataArray<ObjectInstance.Interface>((ushort)(0x0022 + resourceId));
      var doorGratingTemplates = saveData.GetResourceDataArray<ObjectInstance.DoorAndGrating>((ushort)(0x0023 + resourceId));
      var animatedTemplates = saveData.GetResourceDataArray<ObjectInstance.Animated>((ushort)(0x0024 + resourceId));
      var triggerTemplates = saveData.GetResourceDataArray<ObjectInstance.Trigger>((ushort)(0x0025 + resourceId));
      var containerTemplates = saveData.GetResourceDataArray<ObjectInstance.Container>((ushort)(0x0026 + resourceId));
      var enemyTemplates = saveData.GetResourceDataArray<ObjectInstance.Enemy>((ushort)(0x0027 + resourceId));

      var textureAnimation = saveData.GetResourceDataArray<TextureAnimationData>((ushort)(0x002A + resourceId));

      var surveillanceSources = saveData.GetResourceDataArray<ushort>((ushort)(0x002B + resourceId)); // obj ids
      // 0x002C surveillance surrogates

      // 0x002D level variables
      // 0x002E map notes
      // 0x002F map notes pointers
      // 0x0030 Player physic state, unused
      var pathfindingPaths = saveData.GetResourceDataArray<Path>((ushort)(0x0031 + resourceId));
      var pathfindingPathsUsed = saveData.GetResourceData<ushort>((ushort)(0x0032 + resourceId));

      var animationData = saveData.GetResourceDataArray<AnimationData>((ushort)(0x0033 + resourceId));
      var animationCounter = saveData.GetResourceData<ushort>((ushort)(0x0034 + resourceId));

      var heightSemaphores = saveData.GetResourceData<HeightSemaphore>((ushort)(0x0034 + resourceId));

      // Initialize systems
      var defaultSystems = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default);

      var world = World.DefaultGameObjectInjectionWorld;
      //var world = new World($"map{mapId:D}");
      var entityManager = world.EntityManager;

      var entitiesGraphicsSystem = world.GetOrCreateSystemManaged<EntitiesGraphicsSystem>();

      // world.GetOrCreateSystem<PaletteEffectSystem>();

      var clutTexture = await Services.ColorLookupTableTexture.Task;
      var lightmap = await Services.LightmapTexture.Task;
      lightmap.Reinitialize(levelInfo.Width, levelInfo.Height);

      // TODO FIXME move material creations to own system with signleton entity access to material IDs

      var textures = new BitmapSet[TextureMap.NUM_LOADED_TEXTURES];
      var textureProperties = new TextureProperties[TextureMap.NUM_LOADED_TEXTURES];
      var materials = new Dictionary<ushort, Material>(TextureMap.NUM_LOADED_TEXTURES);
      var materialIds = new NativeHashMap<ushort, BatchMaterialID>(TextureMap.NUM_LOADED_TEXTURES, Allocator.Persistent);
      for (ushort i = 0; i < TextureMap.NUM_LOADED_TEXTURES; ++i) {
        if (materialIds.ContainsKey(i)) continue;

        ushort textureIndex = 0;
        unsafe {
          textureIndex = textureMap.blockIndex[i];
        }

        textureProperties[i] = allTextureProperties[textureIndex];

        var bitmapSet = await CreateMipmapTexture(textureIndex); // TODO instead of await, run parallel
        textures[i] = bitmapSet;

        var material = new Material(Shader.Find("Universal Render Pipeline/System Shock/CLUT"));
        material.SetTexture(Shader.PropertyToID(@"_BaseMap"), bitmapSet.Texture);
        material.DisableKeyword(ShaderKeywordStrings._ALPHAPREMULTIPLY_ON);
        material.DisableKeyword(ShaderKeywordStrings._SURFACE_TYPE_TRANSPARENT);
        material.DisableKeyword(ShaderKeywordStrings._ALPHAMODULATE_ON);

        material.EnableKeyword(@"LIGHTGRID");
        if (bitmapSet.Description.Transparent) {
          material.SetFloat("_AlphaClip", 1);
          material.EnableKeyword(ShaderKeywordStrings._ALPHATEST_ON);
          material.renderQueue = (int)RenderQueue.AlphaTest;
        } else {
          material.SetFloat("_AlphaClip", 0);
          material.DisableKeyword(ShaderKeywordStrings._ALPHATEST_ON);
          material.renderQueue = (int)RenderQueue.Geometry;
        }

        material.SetFloat(@"_BlendOp", (float)BlendOp.Add);
        material.SetFloat(@"_SrcBlend", (float)BlendMode.One);
        material.SetFloat(@"_DstBlend", (float)BlendMode.Zero);
        material.enableInstancing = true;

        materials.Add(i, material);
        materialIds.Add(i, entitiesGraphicsSystem.RegisterMaterial(material));
      }

      var mapSystem = world.GetOrCreateSystemManaged<MapElementBuilderSystem>();
      mapSystem.mapMaterial = materialIds.AsReadOnly(); // TODO FIXME Ugly

      var specialMeshSystem = world.GetOrCreateSystemManaged<SpecialMeshSystem>();
      specialMeshSystem.mapMaterial = materialIds.AsReadOnly(); // TODO FIXME Ugly
      var textureAnimationArchetype = entityManager.CreateArchetype(typeof(TextureAnimationData));
      var textureAnimationEntities = entityManager.CreateEntity(textureAnimationArchetype, textureAnimation.Length, Allocator.Persistent); // Don't dispose.
      var animateTexturesSystem = world.GetOrCreateSystemManaged<AnimateTexturesSystem>();
      animateTexturesSystem.textures = textures;
      animateTexturesSystem.textureProperties = textureProperties;
      animateTexturesSystem.mapMaterial = materials; // TODO FIXME Ugly
      animateTexturesSystem.textureAnimationEntities = textureAnimationEntities;

      for (int i = 0; i < textureAnimation.Length; ++i)
        entityManager.SetComponentData(textureAnimationEntities[i], textureAnimation[i]);

      var animationArchetype = entityManager.CreateArchetype(typeof(AnimationData));
      using var animationEntities = entityManager.CreateEntity(animationArchetype, animationCounter, Allocator.Temp);

      for (int i = 0; i < animationCounter; ++i)
        entityManager.SetComponentData(animationEntities[i], animationData[i]);

      // Create Entities
      var objectInstanceArchetype = entityManager.CreateArchetype(typeof(ObjectInstance), typeof(LocalTransform), typeof(LocalToWorld));
      using var objectInstanceEntities = entityManager.CreateEntity(objectInstanceArchetype, ObjectConstants.NUM_OBJECTS, Allocator.Temp);
      for (int i = 0; i < ObjectConstants.NUM_OBJECTS; ++i) {
        var entity = objectInstanceEntities[i];
        var instanceData = objectInstances[i];
        var instanceClass = instanceData.Class;
        var location = instanceData.Location;

        var baseData = objectProperties.BasePropertyData(instanceData);

        var translation = math.float3(
          math.round(128f * location.X / 256f) / 128f,
          math.round(128f * (location.Z * levelInfo.HeightFactor) / 256f) / 128f,
          math.round(128f * location.Y / 256f) / 128f
        );

        var rotation = quaternion.EulerZXY(-location.Pitch / 256f * math.PI * 2f, location.Yaw / 256f * math.PI * 2f, -location.Roll / 256f * math.PI * 2f);

        entityManager.SetComponentData(entity, instanceData);
        entityManager.SetComponentData(entity, LocalTransform.FromPositionRotation(translation, rotation)); // TODO update in job if instance data changes...

        if (!instanceData.Active || instanceData.SpecIndex == 0) continue;

        _ = instanceClass switch {
          ObjectClass.Weapon => entityManager.AddComponentData(entity, weaponInstances[instanceData.SpecIndex]),
          ObjectClass.Ammunition => entityManager.AddComponentData(entity, ammunitionInstances[instanceData.SpecIndex]),
          ObjectClass.Projectile => entityManager.AddComponentData(entity, projectileInstances[instanceData.SpecIndex]),
          ObjectClass.Explosive => entityManager.AddComponentData(entity, explosiveInstances[instanceData.SpecIndex]),
          ObjectClass.DermalPatch => entityManager.AddComponentData(entity, dermalInstances[instanceData.SpecIndex]),
          ObjectClass.Hardware => entityManager.AddComponentData(entity, hardwareInstances[instanceData.SpecIndex]),
          ObjectClass.SoftwareAndLog => entityManager.AddComponentData(entity, softwareLogInstances[instanceData.SpecIndex]),
          ObjectClass.Decoration => entityManager.AddComponentData(entity, decorationInstances[instanceData.SpecIndex]),
          ObjectClass.Item => entityManager.AddComponentData(entity, itemInstances[instanceData.SpecIndex]),
          ObjectClass.Interface => entityManager.AddComponentData(entity, interfaceInstances[instanceData.SpecIndex]),
          ObjectClass.DoorAndGrating => entityManager.AddComponentData(entity, doorGratingInstances[instanceData.SpecIndex]),
          ObjectClass.Animated => entityManager.AddComponentData(entity, animatedInstances[instanceData.SpecIndex]),
          ObjectClass.Trigger => entityManager.AddComponentData(entity, triggerInstances[instanceData.SpecIndex]),
          ObjectClass.Container => entityManager.AddComponentData(entity, containerInstances[instanceData.SpecIndex]),
          ObjectClass.Enemy => entityManager.AddComponentData(entity, enemyInstances[instanceData.SpecIndex]),
          _ => false,
        };

        if (!instanceData.Active) continue;
        if (instanceData.CrossReferenceTableIndex == 0) continue;

        var radius = (float)baseData.Radius / (float)MapElement.PHYSICS_RADIUS_UNIT;

        #region Physics
        if (baseData.TerrainType != Base.TerrainTypes.Ignore) {
          if (baseData.DrawType == DrawType.FlatPolygon ||
              baseData.DrawType == DrawType.AnimatedPolygon ||
              baseData.DrawType == DrawType.TexturedPolygon ||
              baseData.DrawType == DrawType.Bitmap ||
              baseData.DrawType == DrawType.NoObj) {
            var r = radius / 2f;
            var h = baseData.PhysicsZ != 0 ? (float)baseData.PhysicsZ / (float)MapElement.PHYSICS_RADIUS_UNIT : radius;

            if (instanceData.Triple == 0xc000a) { } // TODO FIXME REPULSOR_TRIPLE
            else if (instanceData.Triple == 0x70507) { // TODO FIXME ENERGY_MINE_TRIPLE
              // TODO update rendering position only...
              // y += h / 2f;
              r = -r;
            }

            // TODO add cylinder collider
          } else if (baseData.DrawType == DrawType.Special) {
            // TODO add cube collider for special types
          } else if (baseData.DrawType == DrawType.TranslucentPolygon ||
                     baseData.DrawType == DrawType.FlatTexture ||
                     baseData.DrawType == DrawType.TerrainPolygon) {

            // TODO add cube collider
          }

        }
        #endregion

        #region Rendering
        // ?? compute_3drep

        var rep = -1;
        if (instanceData.Class == ObjectClass.Decoration &&
            (baseData.DrawType == DrawType.TexturedPolygon || instanceData.Triple == 0x70206 /*SCREEN_TRIPLE*/ || instanceData.Triple == 0x70208 /*SUPERSCREEN_TRIPLE*/ || instanceData.Triple == 0x70209 /*BIGSCREEN_TRIPLE*/) &&
            (decorationInstances[instanceData.SpecIndex].Data2 != 0 /* || is animating */ )) {

          const int INDIRECTED_STUFF_INDICATOR_MASK = 0x1000;
          const int INDIRECTED_STUFF_DATA_MASK = 0xFFF;

          var data = decorationInstances[instanceData.SpecIndex].Data2;
          if ((data & INDIRECTED_STUFF_INDICATOR_MASK) != 0) {
            var newObjId = data & INDIRECTED_STUFF_DATA_MASK;
            var dataObj = objectInstances[newObjId];
            rep = (int)decorationInstances[dataObj.SpecIndex].Data2 + dataObj.Info.CurrentFrame;
          } else {
            rep = (int)decorationInstances[instanceData.SpecIndex].Data2 + instanceData.Info.CurrentFrame;
          }
        } else {
          if (baseData.DrawType == DrawType.TerrainPolygon || baseData.DrawType == DrawType.TexturedPolygon) {
            rep = 0;
          } else {
            rep = (baseData.Bitmap & 0x3FF) + ((baseData.Bitmap & 0x8000) >> 5);
            if (baseData.DrawType != DrawType.Voxel && instanceData.Class != ObjectClass.DoorAndGrating && instanceData.Info.CurrentFrame != -1)
              rep += instanceData.Info.CurrentFrame;
          }
        }

        if (instanceData.Class == ObjectClass.Decoration && instanceData.SubClass == 1 /* BIGSTUFF_SUBCLASS_FURNISHING */ && decorationInstances[instanceData.SpecIndex].Data2 == 0) { // Furniture
          const int SECRET_FURNITURE_DEFAULT_O3DREP = 0x80;
          rep = SECRET_FURNITURE_DEFAULT_O3DREP;
        }

        if (baseData.DrawType == DrawType.TexturedPolygon) {
          var modelOp = Addressables.LoadAssetAsync<MeshInfo>($"{ModelResourceIdBase + baseData.MfdId}");
          modelOp.Completed += op => {
            if (op.Status == AsyncOperationStatus.Succeeded)
              entityManager.AddComponentData(entity, op.Result);
          };
        } else if (baseData.DrawType == DrawType.Bitmap) {
          entityManager.AddComponentData(entity, new SpriteInfo { });
        } else if (baseData.DrawType == DrawType.TerrainPolygon) {
          entityManager.AddComponentData(entity, new FlatTextureInfo { });
        } else if (baseData.DrawType == DrawType.NoObj) {
          // NoOp
        } else if (baseData.DrawType == DrawType.FlatTexture) {
          entityManager.AddComponentData(entity, new FlatTextureInfo { });
        } else if (baseData.DrawType == DrawType.Special) {
          // TODO FIXME move outside somewhere out of the loop
          using var defaults = new NativeParallelHashMap<int, (uint SizeX, uint SizeY, uint SizeZ, uint SideTexture, uint TopBottomTexture)>(8, Allocator.Persistent) {
            [0x70700] = (0x04, 0x04, 0x01, 0x80, 0x80),
            [0x70701] = (0x02, 0x04, 0x01, 0x80, 0x80),
            [0x70706] = (0x02, 0x02, 0xB0, 0x80, 0x81),
            [0x70707] = (0x02, 0x04, 0x01, 0x80, 0x80),
            [0x70709] = (0x00, 0x00, 0x00, 0x00, 0x00), // ??
            [0x80509] = (0x04, 0x01, 0x10, 0x00, 0x00),
            [0xD0000] = (0x08, 0x08, 0x08, 0x0C, 0x0B),
            [0xD0001] = (0x10, 0x10, 0x10, 0x0C, 0x0B),
            [0xD0002] = (0x20, 0x20, 0x20, 0x0A, 0x0A),
          };

          var instanceDefault = defaults[instanceData.Triple];

          if (instanceData.Triple == 0x80509 /* BARRICADE_TRIPLE */) {
            var itemInstanceData = itemInstances[instanceData.SpecIndex];
            var texture = itemInstanceData.Data2;
            /*
            if (tluc_val == 0)
               tluc_val = me_cybcolor_flr(MAP_GET_XY(OBJ_LOC_BIN_X(_fr_cobj->loc), OBJ_LOC_BIN_Y(_fr_cobj->loc)));
            */

            var SizeX = (itemInstanceData.SizeX != 0 ? itemInstanceData.SizeX : instanceDefault.SizeX) << 13;
            var SizeY = (itemInstanceData.SizeY != 0 ? itemInstanceData.SizeY : instanceDefault.SizeY) << 13;
            var SizeZ = (itemInstanceData.SizeZ != 0 ? itemInstanceData.SizeZ : instanceDefault.SizeZ) << 10;
            var SideTexture = (itemInstanceData.SideTexture != 0 ? itemInstanceData.SideTexture : instanceDefault.SideTexture);
            var TopBottomTexture = (itemInstanceData.TopBottomTexture != 0 ? itemInstanceData.TopBottomTexture : instanceDefault.TopBottomTexture);

            // TODO FIXME handle current_frame animating in system (obj_model_hack)

            // entityManager.AddComponentData(entity, new CyberCube { });
          } else if (instanceData.Triple == 0x70707 /* FORCE_BRIJ_TRIPLE */ || instanceData.Triple == 0x70709 /* FORCE_BRIJ2_TRIPLE */) {
            var decorationInstanceData = decorationInstances[instanceData.SpecIndex];

            entityManager.AddComponentData(entity, new TransparentCuboid {
              SizeX = ((decorationInstanceData.SizeX != 0 ? decorationInstanceData.SizeX : instanceDefault.SizeX) << 13) * 1f / 65536f, // TODO FIXME correct scaling!
              SizeY = ((decorationInstanceData.SizeY != 0 ? decorationInstanceData.SizeY : instanceDefault.SizeY) << 13) * 1f / 65536f, // TODO FIXME correct scaling!
              SizeZ = ((decorationInstanceData.SizeZ != 0 ? decorationInstanceData.SizeZ : instanceDefault.SizeZ) << 10) * 1f / 65536f, // TODO FIXME correct scaling!
              Color = decorationInstanceData.Data2,
              Offset = (float)baseData.Radius / (float)MapElement.PHYSICS_RADIUS_UNIT
            });
          } else if (instanceData.Triple == 0x70700 /* BRIDGE_TRIPLE */ || instanceData.Triple == 0x70701 /* CATWALK_TRIPLE */ || instanceData.Triple == 0x70706 /* PILLAR_TRIPLE */) {
            var decorationInstanceData = decorationInstances[instanceData.SpecIndex];

            entityManager.AddComponentData(entity, new TexturedCuboid {
              SizeX = ((decorationInstanceData.SizeX != 0 ? decorationInstanceData.SizeX : instanceDefault.SizeX) << 13) * 1f / 65536f, // TODO FIXME correct scaling!
              SizeY = ((decorationInstanceData.SizeY != 0 ? decorationInstanceData.SizeY : instanceDefault.SizeY) << 13) * 1f / 65536f, // TODO FIXME correct scaling!
              SizeZ = ((decorationInstanceData.SizeZ != 0 ? decorationInstanceData.SizeZ : instanceDefault.SizeZ) << 10) * 1f / 65536f, // TODO FIXME correct scaling!
              Offset = 0,
              SideTexture = (byte)(decorationInstanceData.SideTexture != 0 ? decorationInstanceData.SideTexture : instanceDefault.SideTexture),
              TopBottomTexture = (byte)(decorationInstanceData.TopBottomTexture != 0 ? decorationInstanceData.TopBottomTexture : instanceDefault.TopBottomTexture)
            });
          } else if (instanceData.Triple == 0xD0000 /* SML_CRT_TRIPLE */ || instanceData.Triple == 0xD0001 /* LG_CRT_TRIPLE */ || instanceData.Triple == 0xD0002 /* SECURE_CONTR_TRIPLE */) {
            var containerInstanceData = containerInstances[instanceData.SpecIndex];

            entityManager.AddComponentData(entity, new TexturedCuboid {
              SizeX = ((containerInstanceData.SizeX != 0 ? containerInstanceData.SizeX : instanceDefault.SizeX) << 10) * 1f / 65536f, // TODO FIXME correct scaling!
              SizeY = ((containerInstanceData.SizeY != 0 ? containerInstanceData.SizeY : instanceDefault.SizeY) << 10) * 1f / 65536f, // TODO FIXME correct scaling!
              SizeZ = ((containerInstanceData.SizeZ != 0 ? containerInstanceData.SizeZ : instanceDefault.SizeZ) << 10) * 1f / 65536f, // TODO FIXME correct scaling!
              Offset = (float)baseData.Radius / (float)MapElement.PHYSICS_RADIUS_UNIT,
              SideTexture = (byte)(containerInstanceData.SideTexture != 0 ? containerInstanceData.SideTexture : instanceDefault.SideTexture),
              TopBottomTexture = (byte)(containerInstanceData.TopBottomTexture != 0 ? containerInstanceData.TopBottomTexture : instanceDefault.TopBottomTexture)
            });
          } else {
            Debug.LogWarning($"Unsupported special type {instanceData.Triple}.");
          }
        } else {
          Debug.LogWarning($"Unsupported draw type {baseData.DrawType}.");
        }
        #endregion
      }

      var surveillanceSourceEntities = new NativeArray<Entity>(TextureUtils.NUM_HACK_CAMERAS, Allocator.Temp);
      for (var i = 0; i < surveillanceSourceEntities.Length; ++i) {
        //var objIndex = surveillanceSources[i];
        //var obj = objectInstances[i];
        //var entity = obj.Active ? objectInstanceEntities[objIndex] : Entity.Null;

        var entity = objectInstanceEntities[surveillanceSources[i]];
        surveillanceSourceEntities[i] = entity;

        entityManager.AddComponentData(entity, new SurveillanceSource() {
          CameraIndex = i
        });
      }

      var paletteEffectArchetype = entityManager.CreateArchetype(typeof(PaletteEffect));
      using (var paletteEffects = entityManager.CreateEntity(paletteEffectArchetype, 6, Allocator.Temp)) {
        entityManager.AddComponentData(paletteEffects[0], new PaletteEffect { First = 0x03, Last = 0x07, FrameTime = 68, TimeRemaining = 0 });
        entityManager.AddComponentData(paletteEffects[1], new PaletteEffect { First = 0x0B, Last = 0x0F, FrameTime = 40, TimeRemaining = 0 });
        entityManager.AddComponentData(paletteEffects[2], new PaletteEffect { First = 0x10, Last = 0x14, FrameTime = 20, TimeRemaining = 0 });
        entityManager.AddComponentData(paletteEffects[3], new PaletteEffect { First = 0x15, Last = 0x17, FrameTime = 108, TimeRemaining = 0 });
        entityManager.AddComponentData(paletteEffects[4], new PaletteEffect { First = 0x18, Last = 0x1A, FrameTime = 84, TimeRemaining = 0 });
        entityManager.AddComponentData(paletteEffects[5], new PaletteEffect { First = 0x1B, Last = 0x1F, FrameTime = 64, TimeRemaining = 0 });
      }

      var levelInfoArchetype = entityManager.CreateArchetype(typeof(LevelInfo), typeof(Level));
      var levelInfoEntity = entityManager.CreateEntity(levelInfoArchetype);
      entityManager.SetComponentData(levelInfoEntity, levelInfo);

      var level = new Level {
        Id = mapId,
        TextureMap = textureMap,
        ObjectInstances = BuildBlob(objectInstanceEntities),
        SurveillanceCameras = BuildBlob(surveillanceSourceEntities)
      };

      var mapElementArchetype = entityManager.CreateArchetype(typeof(TileLocation), typeof(LocalTransform), typeof(MapElement), typeof(LocalToWorld));
      using (var mapElementEntities = entityManager.CreateEntity(mapElementArchetype, levelInfo.Width * levelInfo.Height, Allocator.Temp)) {
        for (int x = 0; x < levelInfo.Width; ++x) {
          for (int y = 0; y < levelInfo.Height; ++y) {
            var rowIndex = y * levelInfo.Width;

            var entity = mapElementEntities[rowIndex + x];
            entityManager.SetComponentData(entity, new TileLocation { X = (byte)x, Y = (byte)y });
            entityManager.SetComponentData(entity, LocalTransform.FromPosition(x, 0f, y));
            entityManager.SetComponentData(entity, tileMap[x, y]);

            entityManager.AddComponentData(entity, default(LevelViewPartRebuildTag));
            entityManager.AddComponentData(entity, default(LightmapRebuildTag));
          }
        }

        level.TileMap = BuildBlob(mapElementEntities);
      }

      entityManager.SetComponentData(levelInfoEntity, level);

      var hackerArchetype = entityManager.CreateArchetype(typeof(Hacker));
      var hackerEntity = entityManager.CreateEntity(hackerArchetype);
      hackerState.Initialize(); // TODO FIXME only on new game
      hackerState.currentLevel = mapId;
      unsafe {
        if (hackerState.initialShodanSecurityLevels[hackerState.currentLevel] == -1)
          hackerState.initialShodanSecurityLevels[hackerState.currentLevel] = hackerState.GetQuestVar(Shodan.GetShodanQuestVar(hackerState.currentLevel));
      }

      entityManager.SetComponentData(hackerEntity, hackerState);

      var physicsConfigEntity = entityManager.CreateEntity();
      entityManager.AddComponentData(physicsConfigEntity, new PhysicsDebugDisplayData {
        DrawColliders = 0,
        DrawColliderEdges = 0,
        DrawColliderAabbs = 0,
        DrawBroadphase = 0,
        DrawMassProperties = 0,
        DrawContacts = 0,
        DrawCollisionEvents = 0,
        DrawTriggerEvents = 0,
        DrawJoints = 0
      });

      //DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, defaultSystems);
      //ScriptBehaviourUpdateOrder.AddWorldToCurrentPlayerLoop(world);

      return world;
    }

    private static unsafe BlobAssetReference<BlobArray<Entity>> BuildBlob(in NativeArray<Entity> entities) {
      using (BlobBuilder blobBuilder = new BlobBuilder(Allocator.Temp)) {
        ref var tileArrayAsset = ref blobBuilder.ConstructRoot<BlobArray<Entity>>();
        var tileArray = blobBuilder.Allocate(ref tileArrayAsset, entities.Length);
        UnsafeUtility.MemCpy(tileArray.GetUnsafePtr(), entities.GetUnsafeReadOnlyPtr(), entities.Length * UnsafeUtility.SizeOf<Entity>());
        return blobBuilder.CreateBlobAssetReference<BlobArray<Entity>>(Allocator.Persistent);
      }
    }

    private static MapElement[,] ReadMapElements(byte[] rawData, in LevelInfo levelInfo) {
      using (MemoryStream ms = new MemoryStream(rawData)) {
        BinaryReader msbr = new BinaryReader(ms);

        MapElement[,] mapElements = new MapElement[levelInfo.Width, levelInfo.Height];

        for (uint y = 0; y < levelInfo.Height; ++y)
          for (uint x = 0; x < levelInfo.Width; ++x)
            mapElements[x, y] = msbr.Read<MapElement>();

        return mapElements;
      }
    }

    private static async Task<BitmapSet> CreateMipmapTexture(ushort textureIndex) {
      var tex128x128 = Addressables.LoadAssetAsync<BitmapSet>($"{0x03E8 + textureIndex}");
      var tex64x64 = Addressables.LoadAssetAsync<BitmapSet>($"{0x02C3 + textureIndex}");
      var tex32x32 = Addressables.LoadAssetAsync<BitmapSet>($"{0x004D}:{textureIndex}");
      var tex16x16 = Addressables.LoadAssetAsync<BitmapSet>($"{0x004C}:{textureIndex}");

      await Task.WhenAll(tex128x128.Task, tex64x64.Task, tex32x32.Task, tex16x16.Task);

      Texture2D complete = new Texture2D(128, 128, tex128x128.Result.Texture.format, 4, true);
      complete.filterMode = tex128x128.Result.Texture.filterMode;
      complete.wrapMode = tex128x128.Result.Texture.wrapMode;

      if (SystemInfo.copyTextureSupport.HasFlag(CopyTextureSupport.Basic)) {
        Graphics.CopyTexture(tex128x128.Result.Texture, 0, 0, complete, 0, 0);
        Graphics.CopyTexture(tex64x64.Result.Texture, 0, 0, complete, 0, 1);
        Graphics.CopyTexture(tex32x32.Result.Texture, 0, 0, complete, 0, 2);
        Graphics.CopyTexture(tex16x16.Result.Texture, 0, 0, complete, 0, 3);
      } else {
        complete.SetPixelData(tex128x128.Result.Texture.GetPixelData<byte>(0), 0);
        complete.SetPixelData(tex64x64.Result.Texture.GetPixelData<byte>(0), 1);
        complete.SetPixelData(tex32x32.Result.Texture.GetPixelData<byte>(0), 2);
        complete.SetPixelData(tex16x16.Result.Texture.GetPixelData<byte>(0), 3);
      }
      complete.Apply(false, true);

      var result = new BitmapSet {
        Texture = complete,
        Description = tex128x128.Result.Description
      };

      Addressables.Release(tex128x128);
      Addressables.Release(tex64x64);
      Addressables.Release(tex32x32);
      Addressables.Release(tex16x16);

      return result;
    }
  }
}
