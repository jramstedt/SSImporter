using SS.ObjectProperties;
using SS.System;
using System.IO;
using SS.Physics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.GraphicsIntegration;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using static Unity.Physics.Material;
using static SS.MeshUtils;
using BoxCollider = Unity.Physics.BoxCollider;
using CylinderCollider = Unity.Physics.CylinderCollider;
using SphereCollider = Unity.Physics.SphereCollider;

namespace SS.Resources {
  public static class SaveLoader {
    private const ushort SaveGameResourceIdBase = 4000;
    private const ushort ModelResourceIdBase = 2300;
    private const ushort NumResourceIdsPerLevel = 100;

    private static ushort ResourceIdFromLevel(byte level) => (ushort)(SaveGameResourceIdBase + (level * NumResourceIdsPerLevel));
    
    public static async Awaitable<World> LoadMap(byte mapId, string dataPath, string saveGameFile) {
      var saveData = await Res.Open($"{dataPath}\\{saveGameFile}");

      var palette = await Services.Palette;
      var shadetable = await Services.ShadeTable;
      var allTextureProperties = await Services.TextureProperties;
      var objectProperties = await Services.ObjectProperties;

      ushort resourceId = ResourceIdFromLevel(mapId);
      var hackerState = saveData.GetResourceData<Hacker>((ushort)(SaveGameResourceIdBase + 1));
      var levelInfo = saveData.GetResourceData<LevelInfo>((ushort)(0x0004 + resourceId));
      var tileMap = ReadMapElements(saveData.GetResourceData((ushort)(0x0005 + resourceId), 0), levelInfo);
      var schedules = saveData.GetResourceDataArray<ScheduleEvent>((ushort)(0x0006 + resourceId));
      var textureMap = saveData.GetResourceData<TextureMap>((ushort)(0x0007 + resourceId));
      
      var objectInstances = saveData.GetResourceDataArray<ObjectInstance>((ushort)(0x008 + resourceId));
      var crossReferenceTable = saveData.GetResourceDataArray<ObjectReference>((ushort)(0x009 + resourceId));

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
      var animatingInstances = saveData.GetResourceDataArray<ObjectInstance.Animating>((ushort)(0x0015 + resourceId));
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
      var animatedTemplates = saveData.GetResourceDataArray<ObjectInstance.Animating>((ushort)(0x0024 + resourceId));
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

      var heightSemaphores = saveData.GetResourceData<HeightSemaphore>((ushort)(0x0035 + resourceId));
      
      // Game schedule, level independent.
      // TODO Load only when loading from save, not for new game.
      //var gameSchedulerInfo = saveData.GetResourceData<SchedulerInfo>(SchedulerInfo.SCHEDULE_BASE_ID + 0);
      //var gameSchedules = saveData.GetResourceDataArray<ScheduleEvent>(SchedulerInfo.SCHEDULE_BASE_ID + 1);
      // TODO Implement game scheduler

      // Initialize systems
      var defaultSystems = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default);

      var world = World.DefaultGameObjectInjectionWorld;
      //var world = new World($"map{mapId:D}");
      var entityManager = world.EntityManager;

      var entitiesGraphicsSystem = world.GetOrCreateSystemManaged<EntitiesGraphicsSystem>();

      // world.GetOrCreateSystem<PaletteEffectSystem>();

      var clutTexture = await Services.ColorLookupTableTexture;
      var lightmap = await Services.LightmapTexture;
      lightmap.Reinitialize(levelInfo.Width, levelInfo.Height);

      var textureAnimationArchetype = entityManager.CreateArchetype(stackalloc[] { ComponentType.ReadWrite<TextureAnimationData>() });
      var textureAnimationEntities = entityManager.CreateEntity(textureAnimationArchetype, textureAnimation.Length, Allocator.Temp);

      for (int i = 0; i < textureAnimation.Length; ++i)
        entityManager.SetComponentData(textureAnimationEntities[i], textureAnimation[i]);
      
      // Create Entities
      var objectInstanceArchetype = entityManager.CreateArchetype(stackalloc[] { ComponentType.ReadWrite<ObjectInstance>(), ComponentType.ReadWrite<LocalTransform>(), ComponentType.ReadWrite<LocalToWorld>() });
      var objectInstanceEntities = entityManager.CreateEntity(objectInstanceArchetype, ObjectConstants.NUM_OBJECTS, Allocator.Persistent);
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
          ObjectClass.Animating => entityManager.AddComponentData(entity, animatingInstances[instanceData.SpecIndex]),
          ObjectClass.Trigger => entityManager.AddComponentData(entity, triggerInstances[instanceData.SpecIndex]),
          ObjectClass.Container => entityManager.AddComponentData(entity, containerInstances[instanceData.SpecIndex]),
          ObjectClass.Enemy => entityManager.AddComponentData(entity, enemyInstances[instanceData.SpecIndex]),
          _ => false,
        };

        if (!instanceData.Active) continue;
        if (instanceData.CrossReferenceTableIndex == 0) continue;

        if (instanceClass == ObjectClass.Trigger) {
          entityManager.AddComponent<TriggerActivateTag>(entity);
          entityManager.SetComponentEnabled<TriggerActivateTag>(entity, false);
        }

        #region Physics
        if (baseData.TerrainType != Base.TerrainTypes.Ignore) {
          var radius = (float)baseData.Radius / (float)Base.PHYSICS_RADIUS_UNIT;

          if (baseData.DrawType is DrawType.FlatPolygon or DrawType.AnimatedPolygon or DrawType.TexturedPolygon or DrawType.Bitmap or DrawType.NoObj) {
            BlobAssetReference<Unity.Physics.Collider> collider;

            var physicsTranslation = float3.zero;

            var r = radius / 2f;
            var h = baseData.PhysicsZ != 0 ? (float)baseData.PhysicsZ / (float)Base.PHYSICS_RADIUS_UNIT : radius;

            if (instanceData.Triple == 0xc000a /* REPULSOR_TRIPLE */) {
              // TODO Update BoxGeometry if triggerInstance changes

              var trigger = triggerInstances[instanceData.SpecIndex];

              // if (ComparatorCheck(trigger.Comparator, Entity.Null, out byte specialCode))

              /*
               * TODO use these in physics calculations
              var topLimit = trigger.RepulsorTop;
              var targetHeight = trigger.RepulsorBottom;

              if (trigger.RepulsorDirection == ObjectInstance.Trigger.Direction.Up) {
                if (topLimit == 0) targetHeight = 7453f;
                else targetHeight = trigger.RepulsorTop;
              }
              */

              var repulsorHeight = trigger.RepulsorTop - trigger.RepulsorBottom;
              var repulsorCenter = trigger.RepulsorBottom + (repulsorHeight / 2);

              physicsTranslation = math.float3(-location.FineX / 256f, levelInfo.HeightFactor * -location.Z / 256f, -location.FineY / 256f) + math.float3(0.5f, repulsorCenter, 0.5f);

              collider = BoxCollider.Create(
                new BoxGeometry {
                  BevelRadius = float.Epsilon,
                  Center = physicsTranslation,
                  Orientation = quaternion.identity,
                  Size = math.float3(1f, repulsorHeight, 1f)
                },
                CollisionFilter.Default,
                new Unity.Physics.Material {
                  FrictionCombinePolicy = CombinePolicy.GeometricMean,
                  RestitutionCombinePolicy = CombinePolicy.GeometricMean,
                  Friction = 0.5f,
                  Restitution = 0f,
                  CollisionResponse = CollisionResponsePolicy.RaiseTriggerEvents
                }
              );
            } else if (instanceData.Triple == 0x70507) { // TODO FIXME ENERGY_MINE_TRIPLE
              physicsTranslation.y += h / 2f;

              // Sphere collider??
              // r = -r;

              collider = SphereCollider.Create(
                new SphereGeometry {
                  Center = physicsTranslation,
                  Radius = r
                },
                CollisionFilter.Default,
                new Unity.Physics.Material {
                  FrictionCombinePolicy = CombinePolicy.GeometricMean,
                  RestitutionCombinePolicy = CombinePolicy.GeometricMean,
                  Friction = 0.5f,
                  Restitution = 0f,
                  CollisionResponse = CollisionResponsePolicy.RaiseTriggerEvents
                }
              );
            } else {
              // Debug.Log($"h {h} r {r} e {entity}");
              
              // TODO Add configurable option to use mesh collider if DrawType.TexturedPolygon. And to fix collider position/size on DrawType.Bitmap
              
              physicsTranslation.y += h / 2f;

              collider = CylinderCollider.Create(
                new CylinderGeometry {
                  BevelRadius = 0f,
                  Center = physicsTranslation,
                  Height = math.max(h, float.Epsilon),
                  Orientation = quaternion.EulerXZY(math.PIHALF, 0f, 0f),
                  Radius = math.max(r, float.Epsilon),
                  SideCount = 8 // TODO
                }
              );
            }

            entityManager.AddSharedComponentManaged(entity, new PhysicsWorldIndex { Value = 0 });
            entityManager.AddComponentData(entity, new PhysicsCollider { Value = collider });
          } else if (baseData.DrawType == DrawType.Special) {
            BlobAssetReference<Unity.Physics.Collider> collider = default;
            
            if (instanceData.Triple == 0x70707 /* FORCE_BRIJ_TRIPLE */ || instanceData.Triple == 0x70709 /* FORCE_BRIJ2_TRIPLE */ ||
                instanceData.Triple == 0x70700 /* BRIDGE_TRIPLE */ || instanceData.Triple == 0x70701 /* CATWALK_TRIPLE */ || instanceData.Triple == 0x70702 ||
                instanceData.Triple == 0x70706 /* PILLAR_TRIPLE */ || instanceData.Triple == 0x80509 /* BARRICADE_TRIPLE */)
            {
              var Size = GetModelData(instanceData);
              
              collider = BoxCollider.Create(
                new BoxGeometry {
                  BevelRadius = float.Epsilon,
                  Center = new float3(0f, Size.z, 0f) / 0xFFFF,
                  Orientation = quaternion.identity,
                  Size = new float3(Size.xzy * 2) / 0xFFFF
                }
              );
            }

            if (collider.IsCreated) {
              entityManager.AddSharedComponentManaged(entity, new PhysicsWorldIndex { Value = 0 });
              entityManager.AddComponentData(entity, new PhysicsCollider { Value = collider });
            }
          } else if (baseData.DrawType is DrawType.TerrainPolygon or DrawType.FlatTexture or DrawType.TranslucentPolygon) {
            var collider = PolygonCollider.CreateQuad(
              new float3(-.5f, -.5f, 0f),
              new float3(.5f, -.5f, 0f),
              new float3(.5f, .5f, 0f),
              new float3(-.5f, .5f, 0f)
            );
            
            // entityManager.AddSharedComponentManaged(entity, new PhysicsWorldIndex { Value = 0 });
            entityManager.AddComponentData(entity, new PhysicsCollider { Value = collider });
          }
        }
        #endregion

        #region Rendering
        if (baseData.DrawType == DrawType.TexturedPolygon) {
          // TODO parallel?
          var meshInfo = await Res.Load<MeshInfo>((ushort)(ModelResourceIdBase + baseData.MfdId));
          entityManager.AddComponentData(entity, meshInfo);
        } else if (baseData.DrawType == DrawType.Bitmap) {
          entityManager.AddComponentData(entity, new SpriteInfo { });
        } else if (baseData.DrawType == DrawType.TerrainPolygon) {
          entityManager.AddComponentData(entity, new FlatTextureInfo { });
        } else if (baseData.DrawType == DrawType.NoObj) {
          // NoOp
        } else if (baseData.DrawType == DrawType.FlatTexture) {
          entityManager.AddComponentData(entity, new FlatTextureInfo { });
        } else if (baseData.DrawType == DrawType.Special) {
          var instanceDefault = DefaultSpecialMeshParams[instanceData.Triple];
          var Size = GetModelData(instanceData);
          
          if (instanceData.Triple == 0x80509 /* BARRICADE_TRIPLE */) {
            var itemInstanceData = itemInstances[instanceData.SpecIndex];
            var texture = itemInstanceData.Data2;
            /*
            if (tluc_val == 0)
               tluc_val = me_cybcolor_flr(MAP_GET_XY(OBJ_LOC_BIN_X(_fr_cobj->loc), OBJ_LOC_BIN_Y(_fr_cobj->loc)));
            */
            
            var SideTexture = (itemInstanceData.SideTexture != 0 ? itemInstanceData.SideTexture : instanceDefault.SideTexture);
            var TopBottomTexture = (itemInstanceData.TopBottomTexture != 0 ? itemInstanceData.TopBottomTexture : instanceDefault.TopBottomTexture);

            // TODO FIXME handle current_frame animating in system (obj_model_hack)

            // entityManager.AddComponentData(entity, new CyberCube { });
          } else if (instanceData.Triple == 0x70707 /* FORCE_BRIJ_TRIPLE */ || instanceData.Triple == 0x70709 /* FORCE_BRIJ2_TRIPLE */) {
            var decorationInstanceData = decorationInstances[instanceData.SpecIndex];

            entityManager.AddComponentData(entity, new Cuboid {
              Size = new float3(Size) / 0xFFFF,
              Offset = 0,
              SideTexture = (short)-decorationInstanceData.Color,
              TopBottomTexture = (short)-decorationInstanceData.Color,
            });
          } else if (instanceData.Triple == 0x70700 /* BRIDGE_TRIPLE */ || instanceData.Triple == 0x70701 /* CATWALK_TRIPLE */ || instanceData.Triple == 0x70706 /* PILLAR_TRIPLE */) {
            var decorationInstanceData = decorationInstances[instanceData.SpecIndex];

            entityManager.AddComponentData(entity, new Cuboid {
              Size = new float3(Size) / 0xFFFF,
              Offset = 0,
              SideTexture = decorationInstanceData.SideTexture != 0 ? decorationInstanceData.SideTexture : instanceDefault.SideTexture,
              TopBottomTexture = decorationInstanceData.TopBottomTexture != 0 ? decorationInstanceData.TopBottomTexture : instanceDefault.TopBottomTexture
            });
          } else if (instanceData.Triple == 0xD0000 /* SML_CRT_TRIPLE */ || instanceData.Triple == 0xD0001 /* LG_CRT_TRIPLE */ || instanceData.Triple == 0xD0002 /* SECURE_CONTR_TRIPLE */) {
            var containerInstanceData = containerInstances[instanceData.SpecIndex];

            entityManager.AddComponentData(entity, new Cuboid {
              Size = new float3(Size) / 0xFFFF,
              Offset = baseData.Radius / (float)Base.PHYSICS_RADIUS_UNIT,
              SideTexture = containerInstanceData.SideTexture != 0 ? containerInstanceData.SideTexture : instanceDefault.SideTexture,
              TopBottomTexture = containerInstanceData.TopBottomTexture != 0 ? containerInstanceData.TopBottomTexture : instanceDefault.TopBottomTexture
            });
          } else {
            Debug.LogWarning($"Unsupported special type {instanceData.Triple}.");
          }
        } else if (baseData.DrawType == DrawType.TranslucentPolygon) {
          entityManager.AddComponentData(entity, new FlatTextureInfo { });
        } else if (baseData.DrawType == DrawType.DirectionalEnemySprite) {
          entityManager.AddComponentData(entity, new SpriteInfo { });
        } else {
          Debug.LogWarning($"Unsupported draw type {baseData.DrawType}.");
        }
        #endregion
      }
      
      var surveillanceSourceEntities = new NativeArray<Entity>(TextureUtils.NUM_HACK_CAMERAS, Allocator.Persistent);
      for (var i = 0; i < surveillanceSourceEntities.Length; ++i) {
        //var objIndex = surveillanceSources[i];
        //var obj = objectInstances[i];
        //var entity = obj.Active ? objectInstanceEntities[objIndex] : Entity.Null;

        var entity = objectInstanceEntities[surveillanceSources[i]];
        surveillanceSourceEntities[i] = entity;

        entityManager.AddComponentData(entity, new SurveillanceSource() {
          CameraIndex = (byte)i
        });
      }
      
      var paletteEffectArchetype = entityManager.CreateArchetype(stackalloc[] { ComponentType.ReadWrite<PaletteEffect>() });
      using (var paletteEffects = entityManager.CreateEntity(paletteEffectArchetype, 6, Allocator.Temp)) {
        entityManager.AddComponentData(paletteEffects[0], new PaletteEffect { First = 0x03, Last = 0x07, FrameTime = 68, TimeRemaining = 0 });
        entityManager.AddComponentData(paletteEffects[1], new PaletteEffect { First = 0x0B, Last = 0x0F, FrameTime = 40, TimeRemaining = 0 });
        entityManager.AddComponentData(paletteEffects[2], new PaletteEffect { First = 0x10, Last = 0x14, FrameTime = 20, TimeRemaining = 0 });
        entityManager.AddComponentData(paletteEffects[3], new PaletteEffect { First = 0x15, Last = 0x17, FrameTime = 108, TimeRemaining = 0 });
        entityManager.AddComponentData(paletteEffects[4], new PaletteEffect { First = 0x18, Last = 0x1A, FrameTime = 84, TimeRemaining = 0 });
        entityManager.AddComponentData(paletteEffects[5], new PaletteEffect { First = 0x1B, Last = 0x1F, FrameTime = 64, TimeRemaining = 0 });
      }

      var levelInfoEntity = entityManager.CreateEntity(stackalloc[] { ComponentType.ReadWrite<LevelInfo>(), ComponentType.ReadWrite<Level>() });
      entityManager.SetComponentData(levelInfoEntity, levelInfo);
      
      var animList = new NativeList<AnimationData>(AnimateObjectSystem.MAX_ANIMLIST_SIZE, Allocator.Persistent);
      foreach (var data in animationData)
        animList.AddNoResize(data);
      
      animList.Length = animationCounter;
      
      // Assert.AreEqual(animationCounter, animList.Length);
      // TODO if animationCounter == 0 add animations, see do_anims
      
      var level = new Level {
        Id = mapId,
        TextureMap = textureMap,
        TextureAnimations = BuildBlob(textureAnimationEntities),
        ObjectInstances = objectInstanceEntities,
        SurveillanceCameras = surveillanceSourceEntities,
        ObjectReferences = new NativeArray<ObjectReference>(crossReferenceTable, Allocator.Persistent), // TODO make sure it is allocated NUM_REF_OBJECTS, TODO object init
        Animations = animList
      };
      
      var mapElementArchetype = entityManager.CreateArchetype(stackalloc[] { ComponentType.ReadWrite<TileLocation>(), ComponentType.ReadWrite<LocalTransform>(), ComponentType.ReadWrite<MapElement>(), ComponentType.ReadWrite<LocalToWorld>() });
      using (var mapElementEntities = entityManager.CreateEntity(mapElementArchetype, levelInfo.Width * levelInfo.Height, Allocator.Temp)) {
        for (int x = 0; x < levelInfo.Width; ++x) {
          for (int y = 0; y < levelInfo.Height; ++y) {
            var rowIndex = y * levelInfo.Width;

            var entity = mapElementEntities[rowIndex + x];
            entityManager.SetComponentData(entity, new TileLocation { X = (byte)x, Y = (byte)y });
            entityManager.SetComponentData(entity, LocalTransform.FromPosition(x, 0f, y));
            entityManager.SetComponentData(entity, tileMap[x, y]);

            entityManager.AddComponentData(entity, default(MapElementRebuildTag));
            entityManager.AddComponentData(entity, default(LightmapRebuildTag));
          }
        }

        level.TileMap = BuildBlob(mapElementEntities);
      }

      entityManager.SetComponentData(levelInfoEntity, level);
      
      var hackerEntity = entityManager.CreateEntity(stackalloc[] { ComponentType.ReadWrite<LocalTransform>(), ComponentType.ReadWrite<Hacker>(), ComponentType.ReadWrite<HackerControllerComponentData>(), ComponentType.ReadWrite<HackerControllerInternalData>() });
      hackerState.Initialize(); // TODO FIXME only on new game
      hackerState.currentLevel = mapId;
      unsafe {
        if (hackerState.initialShodanSecurityLevels[hackerState.currentLevel] == -1)
          hackerState.initialShodanSecurityLevels[hackerState.currentLevel] = hackerState.GetQuestVar(Shodan.GetShodanQuestVar(hackerState.currentLevel));
      }
      entityManager.SetComponentData(hackerEntity, hackerState);
      
      {
        // TODO FIXME New game only

        const byte INITIAL_PLAYER_X = 0x1E;
        const byte INITIAL_PLAYER_Y = 0x16;

        const int PHYS_MASS_UNIT = 10;
        const int PHYS_MASS_C_NUM = 80;
        const int PHYS_MASS_C_DEN = 33;

        const float STANDARD_MASS = 1f;
        const float STANDARD_HARDNESS = 15f;
        const float STANDARD_ROUGHNESS = 5f;
        const float STANDARD_PEP = 5f;
        const float DEFAULT_SIZE = .5f;
        const float STANDARD_HEIGHT = Hacker.PLAYER_HGT / (float)0xFFFF;
        
        const int HARD_FAC = 6;

        var baseData = objectProperties.BasePropertyData(new Triple { Class = ObjectClass.Enemy, SubClass = 0, Type = 6 });

        // Standards
        var mass = baseData.Mass > 0 ? baseData.Mass / (float)(PHYS_MASS_UNIT * PHYS_MASS_C_NUM / PHYS_MASS_C_DEN) : STANDARD_MASS;
        var size = baseData.Radius > 0 ? baseData.Radius / (float)Base.PHYSICS_RADIUS_UNIT : DEFAULT_SIZE;
        var hardness = baseData.Hardness > 0 ? baseData.Hardness / (float)Base.PHYS_HARDNESS_UNIT : STANDARD_HARDNESS;
        var pep = baseData.Pep > 0 ? baseData.Pep / (float)Base.PHYS_PEP_UNIT : STANDARD_PEP;
        var height = STANDARD_HEIGHT;
        
        // TODO cyberspace
        
        // var hardness = hardness * (mass * HARD_FAC / radius);

        // Sanity
        if (height > 3 * size) height = 3 * size;

        var bodyCollider = new PhysicsCollider {
          Value = SphereCollider.Create(
            new SphereGeometry {
              Radius = size,
              Center = new float3(0f, 0f, 0f)
            },
            new CollisionFilter { BelongsTo = (uint)PhysicsUtils.Layer.Player, CollidesWith = (uint)~PhysicsUtils.Layer.Player, GroupIndex = 0 },
            new Unity.Physics.Material {
              FrictionCombinePolicy = CombinePolicy.Minimum,
              RestitutionCombinePolicy = CombinePolicy.Minimum,
              Friction = 0f,
              Restitution = 1f,
              CollisionResponse = CollisionResponsePolicy.Collide
            })
        };

        var headCollider = new PhysicsCollider {
          Value = SphereCollider.Create(
            new SphereGeometry {
              Radius = size * .75f,
              Center = new float3(0f, 0f, 0f)
            },
            new CollisionFilter { BelongsTo = (uint)PhysicsUtils.Layer.Player, CollidesWith = (uint)~PhysicsUtils.Layer.Player, GroupIndex = 0 },
            new Unity.Physics.Material {
              FrictionCombinePolicy = CombinePolicy.Minimum,
              RestitutionCombinePolicy = CombinePolicy.Minimum,
              Friction = 0f,
              Restitution = 1f,
              CollisionResponse = CollisionResponsePolicy.Collide
            })
        };
        
        var floorHeight = tileMap[INITIAL_PLAYER_X, INITIAL_PLAYER_Y].FloorHeight / (float)levelInfo.HeightDivisor;

        var position = new float3(INITIAL_PLAYER_X + .5f, floorHeight + size + .75f, INITIAL_PLAYER_Y + .5f); // TODO FIXME remove .75f and make sure physics are not run before level is built.
        var rotationAngle = 192f / 256f * math.PI * 2f;
        var rotation = quaternion.EulerZXY(0f, rotationAngle, 0f);
        
        /*
        var headArchetype = entityManager.CreateArchetype(typeof(LocalTransform), typeof(PhysicsCollider), typeof(HackerHead));
        var headEntity = entityManager.CreateEntity(headArchetype);
        entityManager.SetComponentData(headEntity, LocalTransform.FromPositionRotation(position + new float3(0f, height - size, 0f), rotation));
        entityManager.AddSharedComponentManaged(headEntity, new PhysicsWorldIndex { Value = 0 });
        entityManager.SetComponentData(headEntity, headCollider);
        entityManager.SetComponentData(headEntity, new HackerHead { Body = hackerEntity});
        */
        
        entityManager.SetComponentData(hackerEntity, LocalTransform.FromPositionRotation(position, rotation));
        entityManager.SetComponentData(hackerEntity, new HackerControllerComponentData() {
          Gravity = PhysicsStep.Default.Gravity,
          MovementSpeed = 2f,
          MaxMovementSpeed = 5f,
          RotationSpeed = .1f,
          JumpUpwardsSpeed = 2.5f,
          MaxSlope = math.PIHALF,
          MaxIterations = 10,
          CharacterMass = mass,
          CharacterHeight = height - size,
          SkinWidth = 1f / 0xFF,
          ContactTolerance = 2f / 0xFF,
          AffectsPhysicsBodies = true ? 1 : 0,
          RaiseCollisionEvents = false ? 1 : 0,
          RaiseTriggerEvents = false ? 1 : 0,
          
          // Head = headEntity,
          BodyCollider = bodyCollider,
          HeadCollider = headCollider
        });
        entityManager.SetComponentData(hackerEntity, new HackerControllerInternalData() {
          UnsupportedVelocity = float3.zero,
          Velocity = PhysicsVelocity.Zero,

          CurrentRotationAngle = rotationAngle,
          CurrentLeanAngle = 0f,
          CurrentCrouch = 0f,
          
          Entity = hackerEntity,
          
          IsJumping = false,
          Input = new HackerControllerInput {
            Movement = float3.zero,
            Looking = float2.zero,
            LeanAngle = 0f,
            Crouch = 0f,
          },
          
          Height = height - size,
          
          SupportedState = CharacterControllerUtilities.CharacterSupportState.Unsupported,
        });
        entityManager.AddComponent<PhysicsGraphicalSmoothing>(hackerEntity);
        
        entityManager.AddSharedComponentManaged(hackerEntity, new PhysicsWorldIndex { Value = 0 });
        entityManager.AddComponentData(hackerEntity, bodyCollider);
      }
      
      foreach (var schedule in schedules) {
        Debug.Log($"Scheduled {schedule.Timestamp} {schedule.Type}");
        var scheduleEvent = entityManager.CreateEntity(stackalloc[] { ComponentType.ReadWrite<ScheduleEvent>() });
        entityManager.SetComponentData(scheduleEvent, schedule);
      }
      
      /*
      var physicsConfigEntity = entityManager.CreateEntity();
      entityManager.AddComponentData(physicsConfigEntity, new PhysicsDebugDisplayData {
        DrawColliders = 0,
        DrawColliderEdges = 1,
        DrawColliderAabbs = 0,
        DrawBroadphase = 0,
        DrawMassProperties = 0,
        DrawContacts = 0,
        DrawCollisionEvents = 0,
        DrawTriggerEvents = 0,
        DrawJoints = 0
      });
      */

      //DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, defaultSystems);
      //ScriptBehaviourUpdateOrder.AddWorldToCurrentPlayerLoop(world);

      return world;
      
      int3 GetModelData(ObjectInstance instanceData) {
        var instanceDefault = DefaultSpecialMeshParams[instanceData.Triple];
        
        if (instanceData.Class == ObjectClass.Decoration) {
          var decorationInstanceData = decorationInstances[instanceData.SpecIndex];
          
          return new int3(
            (decorationInstanceData.SizeX != 0 ? decorationInstanceData.SizeX : instanceDefault.SizeX) << BIGSTUFF_MODEL_XY_SHF,
            (decorationInstanceData.SizeY != 0 ? decorationInstanceData.SizeY : instanceDefault.SizeY) << BIGSTUFF_MODEL_XY_SHF,
            (decorationInstanceData.SizeZ != 0 ? decorationInstanceData.SizeZ : instanceDefault.SizeZ) << BIGSTUFF_MODEL_Z_SHF
          );
        }
        
        if (instanceData.Class == ObjectClass.Item) {
          var itemInstanceData = itemInstances[instanceData.SpecIndex];
          
          return new int3(
            (itemInstanceData.SizeX != 0 ? itemInstanceData.SizeX : instanceDefault.SizeX) << BIGSTUFF_MODEL_XY_SHF,
            (itemInstanceData.SizeY != 0 ? itemInstanceData.SizeY : instanceDefault.SizeY) << BIGSTUFF_MODEL_XY_SHF, 
            (itemInstanceData.SizeZ != 0 ? itemInstanceData.SizeZ : instanceDefault.SizeZ) << BIGSTUFF_MODEL_Z_SHF
            );
        }

        if (instanceData.Class == ObjectClass.Container) {
          var containerInstanceData = containerInstances[instanceData.SpecIndex];
          
          return new int3(
            (containerInstanceData.SizeX != 0 ? containerInstanceData.SizeX : instanceDefault.SizeX) << CONTAINER_MODEL_SHF,
            (containerInstanceData.SizeY != 0 ? containerInstanceData.SizeY : instanceDefault.SizeY) << CONTAINER_MODEL_SHF,
            (containerInstanceData.SizeZ != 0 ? containerInstanceData.SizeZ : instanceDefault.SizeZ) << CONTAINER_MODEL_SHF
          );
        }

        return int3.zero;
      }
    }

    private static unsafe BlobAssetReference<BlobArray<T>> BuildBlob<T>(in NativeArray<T> array) where T : struct {
      using BlobBuilder blobBuilder = new(Allocator.Temp);
      ref var blobArray = ref blobBuilder.ConstructRoot<BlobArray<T>>();
      var blobBuilderArray = blobBuilder.Allocate(ref blobArray, array.Length);
      UnsafeUtility.MemCpy(blobBuilderArray.GetUnsafePtr(), array.GetUnsafeReadOnlyPtr(), array.Length * UnsafeUtility.SizeOf<T>());
      return blobBuilder.CreateBlobAssetReference<BlobArray<T>>(Allocator.Persistent);
    }

    private static unsafe BlobAssetReference<BlobArray<T>> BuildBlob<T>(in T[] array) where T : struct {
      using BlobBuilder blobBuilder = new(Allocator.Temp);
      ref var blobArray = ref blobBuilder.ConstructRoot<BlobArray<T>>();
      var blobBuilderArray = blobBuilder.Allocate(ref blobArray, array.Length);
      UnsafeUtility.MemCpy(blobBuilderArray.GetUnsafePtr(), UnsafeUtility.PinGCArrayAndGetDataAddress(array, out ulong gcHandle), array.Length * UnsafeUtility.SizeOf<T>());
      UnsafeUtility.ReleaseGCObject(gcHandle);
      return blobBuilder.CreateBlobAssetReference<BlobArray<T>>(Allocator.Persistent);
    }

    private static MapElement[,] ReadMapElements(byte[] rawData, in LevelInfo levelInfo) {
      using MemoryStream ms = new(rawData);
      using BinaryReader msbr = new(ms);

      MapElement[,] mapElements = new MapElement[levelInfo.Width, levelInfo.Height];

      for (uint y = 0; y < levelInfo.Height; ++y)
        for (uint x = 0; x < levelInfo.Width; ++x)
          mapElements[x, y] = msbr.Read<MapElement>();

      return mapElements;
    }
  }
}
