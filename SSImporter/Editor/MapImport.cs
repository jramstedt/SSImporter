using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using SystemShock;
using SystemShock.Object;
using SystemShock.Resource;
using InstanceObjects = SystemShock.InstanceObjects;

namespace SSImporter.Resource {
    public static class MapImport {
        [MenuItem("Assets/System Shock/10. Import Maps", false, 1010)]
        public static void Init() {
            CreateMapAssets();
        }

        [MenuItem("Assets/System Shock/10. Import Maps", true)]
        public static bool Validate() {
            return PlayerPrefs.HasKey(@"SSHOCKRES");
        }

        private static void CreateMapAssets() {
            string filePath = PlayerPrefs.GetString(@"SSHOCKRES");

            string mapLibraryPath = filePath + @"\DATA\ARCHIVE.DAT";

            if (!File.Exists(mapLibraryPath))
                return;
            
            ResourceFile mapLibrary = new ResourceFile(mapLibraryPath);

            LoadLevel(KnownChunkId.Level1Start, mapLibrary);
        }

        private static LevelInfo levelInfo;
        private static SystemShock.LevelInfo runtimeLevelInfo;
        private static ushort[] textureMap;

        private static TextureLibrary textureLibrary;

        private static void LoadLevel(KnownChunkId mapId, ResourceFile mapLibrary) {
            int levelGeometryLayer = LayerMask.NameToLayer(@"Level Geometry");

            if (levelGeometryLayer == -1)
                throw new Exception(@"No 'Level Geometry' Layer set.");

            float progress = 0f;
            float progressStep = 1f / 11f;

            EditorUtility.DisplayProgressBar(@"Map Import", "Reading res files", progress);

            levelInfo = ReadChunk<LevelInfo>(mapId + 0x0004, mapLibrary);
            textureMap = ReadTextureList(mapId, mapLibrary);
            Tile[,] tileMap = ReadTiles(mapId, mapLibrary, levelInfo);
            TileMesh[,] tileMeshes = new TileMesh[levelInfo.Width, levelInfo.Height];

            LevelVariables levelVariables = ReadChunk<LevelVariables>(mapId + 0x002D, mapLibrary);

            #region Read class tables
            IClassData[][] instanceDatas = new IClassData[][] {
                mapLibrary.ReadArrayOf<ObjectInstance.Weapon>(mapId + 0x000A),
                mapLibrary.ReadArrayOf<ObjectInstance.Ammunition>(mapId + 0x000B),
                mapLibrary.ReadArrayOf<ObjectInstance.Projectile>(mapId + 0x000C),
                mapLibrary.ReadArrayOf<ObjectInstance.Explosive>(mapId + 0x000D),
                mapLibrary.ReadArrayOf<ObjectInstance.DermalPatch>(mapId + 0x000E),
                mapLibrary.ReadArrayOf<ObjectInstance.Hardware>(mapId + 0x000F),
                mapLibrary.ReadArrayOf<ObjectInstance.SoftwareAndLog>(mapId + 0x0010),
                mapLibrary.ReadArrayOf<ObjectInstance.Decoration>(mapId + 0x0011),
                mapLibrary.ReadArrayOf<ObjectInstance.Item>(mapId + 0x0012),
                mapLibrary.ReadArrayOf<ObjectInstance.Interface>(mapId + 0x0013),
                mapLibrary.ReadArrayOf<ObjectInstance.DoorAndGrating>(mapId + 0x0014),
                mapLibrary.ReadArrayOf<ObjectInstance.Animated>(mapId + 0x0015),
                mapLibrary.ReadArrayOf<ObjectInstance.Trigger>(mapId + 0x0016),
                mapLibrary.ReadArrayOf<ObjectInstance.Container>(mapId + 0x0017),
                mapLibrary.ReadArrayOf<ObjectInstance.Enemy>(mapId + 0x0018)
            };
            #endregion

            IClassData[][] classDataTemplates = new IClassData[][] {
                mapLibrary.ReadArrayOf<ObjectInstance.Weapon>(mapId + 0x0019),
                mapLibrary.ReadArrayOf<ObjectInstance.Ammunition>(mapId + 0x001A),
                mapLibrary.ReadArrayOf<ObjectInstance.Projectile>(mapId + 0x001B),
                mapLibrary.ReadArrayOf<ObjectInstance.Explosive>(mapId + 0x0001C),
                mapLibrary.ReadArrayOf<ObjectInstance.DermalPatch>(mapId + 0x001D),
                mapLibrary.ReadArrayOf<ObjectInstance.Hardware>(mapId + 0x001E),
                mapLibrary.ReadArrayOf<ObjectInstance.SoftwareAndLog>(mapId + 0x001F),
                mapLibrary.ReadArrayOf<ObjectInstance.Decoration>(mapId + 0x0020),
                mapLibrary.ReadArrayOf<ObjectInstance.Item>(mapId + 0x0021),
                mapLibrary.ReadArrayOf<ObjectInstance.Interface>(mapId + 0x0022),
                mapLibrary.ReadArrayOf<ObjectInstance.DoorAndGrating>(mapId + 0x0023),
                mapLibrary.ReadArrayOf<ObjectInstance.Animated>(mapId + 0x0024),
                mapLibrary.ReadArrayOf<ObjectInstance.Trigger>(mapId + 0x0025),
                mapLibrary.ReadArrayOf<ObjectInstance.Container>(mapId + 0x0026),
                mapLibrary.ReadArrayOf<ObjectInstance.Enemy>(mapId + 0x0027)
            };

            #region Find moving tiles
            int[,][] movingFloorHeightRange = new int[levelInfo.Width, levelInfo.Height][]; // Min and max height
            int[,][] movingCeilingHeightRange = new int[levelInfo.Width, levelInfo.Height][]; // Min and max height

            for (uint y = 0; y < levelInfo.Width; ++y) {
                for (uint x = 0; x < levelInfo.Height; ++x) {
                    Tile tile = tileMap[x, y];
                    movingFloorHeightRange[x, y] = new int[] { tile.FloorHeight, tile.FloorHeight };
                    movingCeilingHeightRange[x, y] = new int[] { tile.CeilingHeight, tile.CeilingHeight };
                }
            }

            foreach (ObjectInstance.Trigger trigger in instanceDatas[(byte)ObjectClass.Trigger]) {
                if (trigger.ActionType == ActionType.MovePlatform) {
                    ObjectInstance.Trigger.MovePlatform movingPlatform = trigger.Data.Read<ObjectInstance.Trigger.MovePlatform>();

                    if (movingPlatform.TargetFloorHeight <= Tile.MAX_HEIGHT) {
                        int[] floorRange = movingFloorHeightRange[movingPlatform.TileX, movingPlatform.TileY];
                        floorRange[0] = Mathf.Min(floorRange[0], movingPlatform.TargetFloorHeight);
                        floorRange[1] = Mathf.Max(floorRange[1], movingPlatform.TargetFloorHeight);
                    }

                    if (movingPlatform.TargetCeilingHeight <= Tile.MAX_HEIGHT) {
                        int[] ceilingRange = movingCeilingHeightRange[movingPlatform.TileX, movingPlatform.TileY];
                        ceilingRange[0] = Mathf.Min(ceilingRange[0], movingPlatform.TargetCeilingHeight);
                        ceilingRange[1] = Mathf.Max(ceilingRange[1], movingPlatform.TargetCeilingHeight);
                    }
                }
            }

            foreach (ObjectInstance.Interface @interface in instanceDatas[(byte)ObjectClass.Interface]) {
                if (@interface.ActionType == ActionType.MovePlatform) {
                    ObjectInstance.Trigger.MovePlatform movingPlatform = @interface.Data.Read<ObjectInstance.Trigger.MovePlatform>();

                    if (movingPlatform.TargetFloorHeight <= Tile.MAX_HEIGHT) {
                        int[] floorRange = movingFloorHeightRange[movingPlatform.TileX, movingPlatform.TileY];
                        floorRange[0] = Mathf.Min(floorRange[0], movingPlatform.TargetFloorHeight);
                        floorRange[1] = Mathf.Max(floorRange[1], movingPlatform.TargetFloorHeight);
                    }

                    if (movingPlatform.TargetCeilingHeight <= Tile.MAX_HEIGHT) {
                        int[] ceilingRange = movingCeilingHeightRange[movingPlatform.TileX, movingPlatform.TileY];
                        ceilingRange[0] = Mathf.Min(ceilingRange[0], movingPlatform.TargetCeilingHeight);
                        ceilingRange[1] = Mathf.Max(ceilingRange[1], movingPlatform.TargetCeilingHeight);
                    }
                }
            }
            #endregion

            progress += progressStep;

            try {
                #region Create LevelInfo
                EditorUtility.DisplayProgressBar(@"Map Import", "Creating level info object", progress);

                GameObject levelInfoGO = new GameObject(@"LevelInfo");
                runtimeLevelInfo = levelInfoGO.AddComponent<SystemShock.LevelInfo>();
                runtimeLevelInfo.Type = levelInfo.Flags == LevelInfo.LevelInfoFlags.Cyberspace ? SystemShock.LevelInfo.LevelType.Cyberspace : SystemShock.LevelInfo.LevelType.Normal;
                runtimeLevelInfo.HeightFactor = (float)Tile.MAX_HEIGHT / ((1 << (int)levelInfo.HeightShift) * 256f);
                runtimeLevelInfo.MapScale = 1f / (float)(1 << (int)levelInfo.HeightShift);
                runtimeLevelInfo.TextureMap = textureMap;
                runtimeLevelInfo.Tiles = new SystemShock.LevelInfo.Tile[levelInfo.Width, levelInfo.Height];
                runtimeLevelInfo.Radiation = levelVariables.Radiation * 0.5f;
                runtimeLevelInfo.BioContamination = levelVariables.BioIsGravity == 0 ? levelVariables.BioContamination * 0.5f : 0f;
                runtimeLevelInfo.Gravity = levelVariables.BioIsGravity != 0 ? levelVariables.BioContamination * 0.5f : 0f;
                runtimeLevelInfo.TextureAnimations = new List<TextureAnimation>(mapLibrary.ReadArrayOf<TextureAnimation>(mapId + 0x002A));
                runtimeLevelInfo.ClassDataTemplates = new IClassData[classDataTemplates.Length];
                
                for (int classTemplateIndex = 0; classTemplateIndex < classDataTemplates.Length; ++classTemplateIndex) {
                    IClassData classData = classDataTemplates[classTemplateIndex][0];  
                    runtimeLevelInfo.ClassDataTemplates[classTemplateIndex] = classData;
                }
                
                progress += progressStep;
                #endregion

                #region Loop Configurations
                EditorUtility.DisplayProgressBar(@"Map Import", "Adding loop configurations", progress);

                LoopConfiguration[] loopConfigurations = mapLibrary.ReadArrayOf<LoopConfiguration>(mapId + 0x0033);
                foreach (LoopConfiguration loopConfiguration in loopConfigurations) {
                    if (loopConfiguration.ObjectId != 0)
                        runtimeLevelInfo.LoopConfigurations[loopConfiguration.ObjectId] = loopConfiguration;
                }

                progress += progressStep;
                #endregion

                textureLibrary = TextureLibrary.GetLibrary(@"texture.res");

                float stepPercentage = progressStep / (levelInfo.Width * levelInfo.Height);

                #region Create Tiles
                for (uint y = 0; y < levelInfo.Width; ++y) {
                    for (uint x = 0; x < levelInfo.Height; ++x) {
                        EditorUtility.DisplayProgressBar(@"Map Import", "Creating tiles", progress);
                        progress += stepPercentage;

                        tileMeshes[x, y] = new TileMesh(levelInfo, tileMap[x, y], x, y, movingFloorHeightRange[x, y], movingCeilingHeightRange[x, y]);
                    }
                }
                #endregion

                #region Construct Tiles
                for (uint y = 0; y < levelInfo.Width; ++y) {
                    for (uint x = 0; x < levelInfo.Height; ++x) {
                        EditorUtility.DisplayProgressBar(@"Map Import", "Creating tiles", progress);
                        progress += stepPercentage;

                        Tile tile = tileMap[x, y];

                        if (tile.Type != TileType.Solid) {
                            TileMesh tileMesh = tileMeshes[x, y];

                            GameObject tileGO = CreateGameObject(CombineTile(tileMesh, tileMeshes), @"Tile " + x.ToString() + " " + y.ToString());
                            tileGO.layer = levelGeometryLayer;
                            
                            SystemShock.LevelInfo.Tile liTile = new SystemShock.LevelInfo.Tile() {
                                Ceiling = tile.CeilingHeight,
                                Floor = tile.FloorHeight,
                                GameObject = tileGO
                            };

                            runtimeLevelInfo.Tiles[x, y] = liTile;

                            /*
                            byte shadeUpper = (byte)(((int)(tile.Flags & Tile.FlagMask.ShadeUpper) >> 24) & 0x0F);
                            byte shadeLower = (byte)(((int)(tile.Flags & Tile.FlagMask.ShadeLower) >> 16) & 0x0F);
                        
                            MaterialPropertyBlock materialPropertyBlock = new MaterialPropertyBlock();
                            meshRenderer.GetPropertyBlock(materialPropertyBlock);
                            materialPropertyBlock.SetColor(@"_EmissionColor", Color.white * Mathf.LinearToGammaSpace((float)(0x0F - (shadeUpper + shadeLower) / 2) / 15f));
                            meshRenderer.SetPropertyBlock(materialPropertyBlock);
                            */

                            /*
                            byte shadeUpper = (byte)(((int)(tile.Flags & Tile.FlagMask.ShadeUpper) >> 24) & 0x0F);
                            byte shadeLower = (byte)(((int)(tile.Flags & Tile.FlagMask.ShadeLower) >> 16) & 0x0F);
                        
                            Material floorMaterial = meshRenderer.materials[0];
                            floorMaterial.SetColor(@"_EmissionColor", Color.white * Mathf.LinearToGammaSpace((float)(0x0F - shadeLower) / 15f));
                            floorMaterial.SetColor(@"_EmissionColorUI", Color.white);
                            floorMaterial.SetColor(@"_EmissionColorWithMapUI", Color.white);
                            floorMaterial.SetFloat(@"_EmissionScaleUI", (float)(0x0F - shadeLower) / 15f);

                            Material ceilingMaterial = meshRenderer.materials[1];
                            ceilingMaterial.SetColor(@"_EmissionColor", Color.white * Mathf.LinearToGammaSpace((float)(0x0F - shadeUpper) / 15f));
                            ceilingMaterial.SetColor(@"_EmissionColorUI", Color.white);
                            ceilingMaterial.SetColor(@"_EmissionColorWithMapUI", Color.white);
                            ceilingMaterial.SetFloat(@"_EmissionScaleUI", (float)(0x0F - shadeUpper) / 15f);
                            */

                            if (tileMesh.FloorMoving) {
                                MovingTileMesh movingFloor = new MovingTileMesh(MovingTileMesh.Type.Floor, levelInfo, tile, x, y, movingFloorHeightRange[x, y], movingCeilingHeightRange[x, y]);

                                GameObject floorGo = CreateGameObject(CombineTile(movingFloor, tileMeshes, true), "Moving floor", true);
                                floorGo.transform.SetParent(tileGO.transform, false);
                                floorGo.layer = levelGeometryLayer;

                                Rigidbody rigidbody = floorGo.AddComponent<Rigidbody>();
                                rigidbody.isKinematic = true;

                                MovablePlatform platform = floorGo.AddComponent<MovableFloor>();
                                platform.OriginHeight = movingFloorHeightRange[x, y][1];
                                platform.Height = tile.FloorHeight;
                            }

                            if (tileMesh.CeilingMoving) {
                                MovingTileMesh movingCeiling = new MovingTileMesh(MovingTileMesh.Type.Ceiling, levelInfo, tile, x, y, movingFloorHeightRange[x, y], movingCeilingHeightRange[x, y]);

                                GameObject ceilingGo = CreateGameObject(CombineTile(movingCeiling, tileMeshes, true), "Moving ceiling", true);
                                ceilingGo.transform.SetParent(tileGO.transform, false);
                                ceilingGo.layer = levelGeometryLayer;

                                Rigidbody rigidbody = ceilingGo.AddComponent<Rigidbody>();
                                rigidbody.isKinematic = true;

                                MovablePlatform platform = ceilingGo.AddComponent<MovableCeiling>();
                                platform.OriginHeight = movingCeilingHeightRange[x, y][0];
                                platform.Height = tile.CeilingHeight;
                            }

                            tileGO.transform.localPosition = new Vector3(x, 0, y); // start from bottom left

                            EditorUtility.SetDirty(tileGO);
                        }
                    }
                }
                #endregion

                #region Light probes
                GameObject lightProbesGameObject = new GameObject(@"Light probes");
                lightProbesGameObject.isStatic = true;
                LightProbeGroup lightProbeGroub = lightProbesGameObject.AddComponent<LightProbeGroup>();
                List<Vector3> lightProbes = new List<Vector3>();
                for (uint y = 0; y < levelInfo.Width; ++y) {
                    for (uint x = 0; x < levelInfo.Height; ++x) {
                        EditorUtility.DisplayProgressBar(@"Map Import", "Creating light probes", progress);
                        progress += stepPercentage;

                        TileMesh tileMesh = tileMeshes[x, y];

                        if (tileMesh.tile.Type != TileType.Solid) {
                            float xOffset = 0.5f, yOffset = 0.15f, zOffset = 0.5f;

                            if (tileMesh.tile.Type == TileType.OpenDiagonalNE) {
                                zOffset = 0.75f;
                                xOffset = 0.75f;
                            } else if (tileMesh.tile.Type == TileType.OpenDiagonalNW) {
                                zOffset = 0.75f;
                                xOffset = 0.25f;
                            } else if (tileMesh.tile.Type == TileType.OpenDiagonalSE) {
                                zOffset = 0.25f;
                                xOffset = 0.75f;
                            } else if (tileMesh.tile.Type == TileType.OpenDiagonalSW) {
                                zOffset = 0.25f;
                                xOffset = 0.25f;
                            }

                            if (!tileMesh.FloorMoving)
                                lightProbes.Add(new Vector3(x + xOffset, tileMesh.FloorHeightMiddle / (float)(1 << (int)levelInfo.HeightShift) + yOffset, y + zOffset));

                            if (!tileMesh.CeilingMoving)
                                lightProbes.Add(new Vector3(x + xOffset, tileMesh.CeilingHeightMiddle / (float)(1 << (int)levelInfo.HeightShift) - yOffset, y + zOffset));
                        }
                    }
                }
                lightProbeGroub.probePositions = lightProbes.ToArray();
                #endregion

                #region Reflection probes
                //lightProbesGameObject.AddComponent<ReflectionProbe>();
                EditorUtility.DisplayProgressBar(@"Map Import", "Creating reflection probes", progress);
                progress += progressStep;

                #endregion

                ObjectInstance[] objectInstances = mapLibrary.ReadArrayOf<ObjectInstance>(mapId + 0x0008);

                #region Surveillance nodes
                ushort[] surveillanceNodeIndices = mapLibrary.ReadArrayOf<ushort>(mapId + 0x002B);

                stepPercentage = progressStep / surveillanceNodeIndices.Length;

                List<SystemShock.LevelInfo.SurveillanceCamera> surveillanceCamera = new List<SystemShock.LevelInfo.SurveillanceCamera>(surveillanceNodeIndices.Length);
                for (int nodeIndex = 0; nodeIndex < surveillanceNodeIndices.Length; ++nodeIndex) {
                    EditorUtility.DisplayProgressBar(@"Map Import", "Creating surveillance nodes", progress);

                    ushort instanceIndex = surveillanceNodeIndices[nodeIndex];

                    if (instanceIndex == 0)
                        surveillanceCamera.Add(null);
                    else
                        surveillanceCamera.Add(CreateCamera(objectInstances[instanceIndex]));

                    progress += stepPercentage;
                }

                runtimeLevelInfo.SurveillanceCameras = surveillanceCamera.ToArray();
                #endregion

                #region Text screen
                EditorUtility.DisplayProgressBar(@"Map Import", "Creating text screen renderer", progress);
                {
                    FontLibrary fontLibrary = FontLibrary.GetLibrary(@"gamescr.res");

                    GameObject textScreenRendererGO = new GameObject(@"Text Screen Renderer");
                    textScreenRendererGO.layer = LayerMask.NameToLayer(@"UI");
                    runtimeLevelInfo.TextScreenRenderer = textScreenRendererGO.AddComponent<TextScreenRenderer>();

                    GameObject cameraGO = new GameObject(@"Camera");
                    cameraGO.layer = LayerMask.NameToLayer(@"UI");
                    cameraGO.transform.SetParent(textScreenRendererGO.transform, false);
                    Camera camera = cameraGO.AddComponent<Camera>();
                    camera.backgroundColor = Color.black;
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.orthographic = true;
                    camera.orthographicSize = 0.5f;
                    camera.nearClipPlane = 0.1f;
                    camera.farClipPlane = 2f;
                    camera.cullingMask = LayerMask.GetMask(@"UI");
                    camera.useOcclusionCulling = false;
                    camera.enabled = false;

                    GameObject canvasGO = new GameObject(@"Canvas");
                    canvasGO.layer = LayerMask.NameToLayer(@"UI");
                    canvasGO.transform.SetParent(textScreenRendererGO.transform, false);
                    Canvas canvas = canvasGO.AddComponent<Canvas>();
                    canvas.referencePixelsPerUnit = 64f;
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.planeDistance = 1f;
                    canvas.worldCamera = camera;
                    CanvasScaler canvasScaler = canvasGO.AddComponent<CanvasScaler>();
                    canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                    canvasScaler.scaleFactor = 1f;

                    GameObject textGO = new GameObject(@"Text");
                    textGO.layer = LayerMask.NameToLayer(@"UI");
                    textGO.transform.SetParent(canvasGO.transform, false);
                    Text text = textGO.AddComponent<Text>();
                    text.rectTransform.anchorMin = Vector2.zero;
                    text.rectTransform.anchorMax = Vector2.one;
                    text.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    text.rectTransform.offsetMin = Vector2.zero;
                    text.rectTransform.offsetMax = Vector2.zero;
                    text.alignment = TextAnchor.MiddleLeft;
                    text.horizontalOverflow = HorizontalWrapMode.Overflow;
                    text.verticalOverflow = VerticalWrapMode.Overflow;
                    text.supportRichText = false;
                    text.color = Color.red; //TODO Get color from palette
                    text.font = fontLibrary.GetFont((KnownChunkId)605);
                    text.text = "GENERAL\nSYSTEM\nSTATUS";
                }
                progress += progressStep;
                #endregion

                #region Objects
                ObjectFactory objectFactory = ObjectFactory.GetController();
                objectFactory.UpdateLevelInfo();

                stepPercentage = progressStep / objectInstances.Length;
                
                for (ushort instanceIndex = 0; instanceIndex < objectInstances.Length; ++instanceIndex) {
                    EditorUtility.DisplayProgressBar(@"Map Import", "Creating object instances", progress);
                    progress += stepPercentage;

                    ObjectInstance objectInstance = objectInstances[instanceIndex];

                    if (objectInstance.InUse == 0)
                        continue;

                    IClassData instanceData = instanceDatas[(byte)objectInstance.Class][objectInstance.ClassTableIndex];

                    if (objectInstance.Class == ObjectClass.Decoration && levelInfo.Flags == LevelInfo.LevelInfoFlags.Cyberspace) {
                        ObjectInstance.Decoration decoration = (ObjectInstance.Decoration)instanceData;
                        ObjectInstance.Decoration.SoftwareAndLogExtra data = decoration.Data.Read<ObjectInstance.Decoration.SoftwareAndLogExtra>();

                        objectInstance.Class = ObjectClass.SoftwareAndLog;
                        objectInstance.SubClass = (byte)data.Subclass;
                        objectInstance.Type = (byte)data.Type;

                        instanceData = new ObjectInstance.SoftwareAndLog() {
                            ObjectId = instanceData.ObjectId,
                            Version = (byte)data.Version,
                            LogIndex = 0,
                            LevelIndex = (byte)data.LevelIndex
                        };
                    }

                    SystemShockObject ssObject = objectFactory.Instantiate(objectInstance, instanceData);

                    if (ssObject == null)
                        continue;

                    EditorUtility.SetDirty(ssObject.gameObject);
                }
                
                #endregion

                #region Surveillance camera deathwatch object
                ushort[] surveillanceDeathWatchNodeIndices = mapLibrary.ReadArrayOf<ushort>(mapId + 0x002C);
                for (int nodeIndex = 0; nodeIndex < surveillanceDeathWatchNodeIndices.Length; ++nodeIndex) {
                    ushort objectIndex = surveillanceDeathWatchNodeIndices[nodeIndex];
                    if(runtimeLevelInfo.Objects.ContainsKey(objectIndex))
                        runtimeLevelInfo.SurveillanceCameras[nodeIndex].DeathwatchObject = runtimeLevelInfo.Objects[objectIndex];
                }
                #endregion

                EditorUtility.DisplayProgressBar(@"Map Import", "Calculating occlusion culling", progress);

                StaticOcclusionCulling.smallestOccluder = 0.5f;
                StaticOcclusionCulling.smallestHole = runtimeLevelInfo.HeightFactor;
                //StaticOcclusionCulling.Compute();

                progress += progressStep;
            } catch(Exception e) {
                EditorUtility.ClearProgressBar();
                Debug.LogException(e);
            } finally {
                EditorUtility.ClearProgressBar();
                EditorApplication.SaveAssets();
            }

            AssetDatabase.Refresh();
        }

        private static GameObject CreateGameObject(CombinedTileMesh combinedTileMesh, string name, bool moving = false) {
            GameObject gameObject = new GameObject();
            gameObject.name = name;

            if (!moving)
                GameObjectUtility.SetStaticEditorFlags(gameObject,  StaticEditorFlags.LightmapStatic |
                                                                    StaticEditorFlags.OccluderStatic |
                                                                    StaticEditorFlags.NavigationStatic |
                                                                    StaticEditorFlags.OccludeeStatic |
                                                                    StaticEditorFlags.OffMeshLinkGeneration |
                                                                    StaticEditorFlags.ReflectionProbeStatic);

            MeshFilter meshFilter = gameObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = combinedTileMesh.Mesh;

            MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterials = combinedTileMesh.Materials;

            MeshCollider meshCollider = gameObject.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = combinedTileMesh.Mesh;

            #region Check and build texture animations
            Dictionary<TextureProperties, List<int>> materialAnimations = new Dictionary<TextureProperties, List<int>>();
            for (int i = 0; i < combinedTileMesh.Materials.Length; ++i) {
                TextureProperties textureProperties = textureLibrary.GetTextureProperties(combinedTileMesh.Materials[i]);
                if(textureProperties.AnimationGroup > 0) {
                    List<int> indices;
                    if (!materialAnimations.TryGetValue(textureProperties, out indices))
                        materialAnimations.Add(textureProperties, indices = new List<int>());

                    indices.Add(i);
                }
            }

            if (materialAnimations.Count > 0) {
                AnimateMaterial animate = gameObject.AddComponent<AnimateMaterial>();
                foreach (KeyValuePair<TextureProperties, List<int>> materialAnimation in materialAnimations) {
                    TextureProperties textureProperties = materialAnimation.Key;
                    TextureAnimation textureAnimation = runtimeLevelInfo.TextureAnimations[textureProperties.AnimationGroup];

                    Material[] frames = textureLibrary.GetMaterialAnimation(textureProperties.AnimationGroup);
                    animate.AddAnimation(materialAnimation.Value.ToArray(), frames, textureAnimation);
                }
            }
            #endregion

            return gameObject;
        }

        private static void IsFlipped(uint tileX, uint tileY, Tile tile, out bool flipX, out bool flipY) {
            flipX = false; 
            flipY = false;
            if (tile.TextureAlternate) {
                flipX = ((tileX & 1) == 1) ^ ((tileY & 1) == 0);
                flipY = !flipX;
            }

            if (tile.TextureFlip) {
                flipX = !flipX;
                flipY = !flipY;
            }
        }

        private static CombinedTileMesh CombineTile(BaseMesh tileMesh, TileMesh[,] tileMeshes, bool invertWalls = false) {
            List<CombineInstance> tileParts = new List<CombineInstance>();
            List<Material> tileMaterials = new List<Material>();

            Tile tile = tileMesh.tile;
            uint x = tileMesh.tileX;
            uint y = tileMesh.tileY;

            if (tileMesh.HasFloor) {
                tileParts.Add(new CombineInstance() { mesh = tileMesh.CreateFloor(tile.FloorOrientation) });
                tileMaterials.Add(textureLibrary.GetMaterial(textureMap[tile.FloorTexture]));
            }

            if (tileMesh.HasCeiling) {
                tileParts.Add(new CombineInstance() { mesh = tileMesh.CreateCeiling(tile.CeilingOrientation) });
                tileMaterials.Add(textureLibrary.GetMaterial(textureMap[tile.CeilingTexture]));
            }

            bool flipX = false, flipY = false;

            // North wall
            TileMesh northTileMesh = tileMeshes[x, y + 1];
            Mesh northWall = null;

            IsFlipped(x, y, invertWalls ? northTileMesh.tile : tile, out flipX, out flipY);

            if (tile.Type != TileType.OpenDiagonalSE && tile.Type != TileType.OpenDiagonalSW)
                northWall = tileMesh.CreateWall(1, 2, northTileMesh, 0, 3, flipY, TileType.OpenDiagonalNE, TileType.OpenDiagonalNW);
            else if (tile.Type == TileType.OpenDiagonalSE)
                northWall = tileMesh.CreateWall(0, 2, northTileMesh, 0, 2, flipY);
            else if (tile.Type == TileType.OpenDiagonalSW)
                northWall = tileMesh.CreateWall(1, 3, northTileMesh, 1, 3, flipY);

            if (northWall != null) {
                tileParts.Add(new CombineInstance() { mesh = northWall });

                if (invertWalls) {
                    if (northTileMesh.tile.UseAdjacentTexture)
                        tileMaterials.Add(textureLibrary.GetMaterial(textureMap[tile.WallTexture]));
                    else
                        tileMaterials.Add(textureLibrary.GetMaterial(textureMap[northTileMesh.tile.WallTexture]));
                } else {
                    if (tile.UseAdjacentTexture)
                        tileMaterials.Add(textureLibrary.GetMaterial(textureMap[northTileMesh.tile.WallTexture]));
                    else
                        tileMaterials.Add(textureLibrary.GetMaterial(textureMap[tile.WallTexture]));
                }

            }

            // East wall
            if (tile.Type != TileType.OpenDiagonalSW && tile.Type != TileType.OpenDiagonalNW) {
                TileMesh eastTileMesh = tileMeshes[x + 1, y];

                IsFlipped(x, y, invertWalls ? eastTileMesh.tile : tile, out flipX, out flipY);

                Mesh eastWall = tileMesh.CreateWall(2, 3, eastTileMesh, 1, 0, flipX, TileType.OpenDiagonalNE, TileType.OpenDiagonalSE);

                if (eastWall != null) {
                    tileParts.Add(new CombineInstance() { mesh = eastWall });

                    if (invertWalls) {
                        if (eastTileMesh.tile.UseAdjacentTexture)
                            tileMaterials.Add(textureLibrary.GetMaterial(textureMap[tile.WallTexture]));
                        else
                            tileMaterials.Add(textureLibrary.GetMaterial(textureMap[eastTileMesh.tile.WallTexture]));
                    } else {
                        if (tile.UseAdjacentTexture)
                            tileMaterials.Add(textureLibrary.GetMaterial(textureMap[eastTileMesh.tile.WallTexture]));
                        else
                            tileMaterials.Add(textureLibrary.GetMaterial(textureMap[tile.WallTexture]));
                    }
                }
            }

            // South wall
            TileMesh southTileMesh = tileMeshes[x, y - 1];
            Mesh southWall = null;

            IsFlipped(x, y, invertWalls ? southTileMesh.tile : tile, out flipX, out flipY);

            if (tile.Type != TileType.OpenDiagonalNE && tile.Type != TileType.OpenDiagonalNW)
                southWall = tileMesh.CreateWall(3, 0, southTileMesh, 2, 1, flipY, TileType.OpenDiagonalSE, TileType.OpenDiagonalSW);
            else if (tile.Type == TileType.OpenDiagonalNE)
                southWall = tileMesh.CreateWall(3, 1, southTileMesh, 3, 1, flipY);
            else if (tile.Type == TileType.OpenDiagonalNW)
                southWall = tileMesh.CreateWall(2, 0, southTileMesh, 2, 0, flipY);

            if (southWall != null) {
                tileParts.Add(new CombineInstance() { mesh = southWall });

                if (invertWalls) {
                    if (southTileMesh.tile.UseAdjacentTexture)
                        tileMaterials.Add(textureLibrary.GetMaterial(textureMap[tile.WallTexture]));
                    else
                        tileMaterials.Add(textureLibrary.GetMaterial(textureMap[southTileMesh.tile.WallTexture]));
                } else {
                    if (tile.UseAdjacentTexture)
                        tileMaterials.Add(textureLibrary.GetMaterial(textureMap[southTileMesh.tile.WallTexture]));
                    else
                        tileMaterials.Add(textureLibrary.GetMaterial(textureMap[tile.WallTexture]));
                }
            }

            // West wall
            if (tile.Type != TileType.OpenDiagonalSE && tile.Type != TileType.OpenDiagonalNE) {
                TileMesh westTileMesh = tileMeshes[x - 1, y];

                IsFlipped(x, y, invertWalls ? westTileMesh.tile : tile, out flipX, out flipY);

                Mesh westWall = tileMesh.CreateWall(0, 1, westTileMesh, 3, 2, flipX, TileType.OpenDiagonalNW, TileType.OpenDiagonalSW);

                if (westWall != null) {
                    tileParts.Add(new CombineInstance() { mesh = westWall });

                    if (invertWalls) {
                        if (westTileMesh.tile.UseAdjacentTexture)
                            tileMaterials.Add(textureLibrary.GetMaterial(textureMap[tile.WallTexture]));
                        else
                            tileMaterials.Add(textureLibrary.GetMaterial(textureMap[westTileMesh.tile.WallTexture]));
                    } else {
                        if (tile.UseAdjacentTexture)
                            tileMaterials.Add(textureLibrary.GetMaterial(textureMap[westTileMesh.tile.WallTexture]));
                        else
                            tileMaterials.Add(textureLibrary.GetMaterial(textureMap[tile.WallTexture]));
                    }
                }
            }

            Mesh mesh = new Mesh();

            mesh.CombineMeshes(tileParts.ToArray(), false, false);

            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.Optimize();
            mesh.RecalculateBounds();

            return new CombinedTileMesh() {
                Mesh = mesh,
                Materials = tileMaterials.ToArray()
            };
        }

        private static SystemShock.LevelInfo.SurveillanceCamera CreateCamera(ObjectInstance objectInstance) {
            GameObject cameraGO = new GameObject();
            cameraGO.name = "Surveillance Camera";

            float yFactor = (float)Tile.MAX_HEIGHT / ((1 << (int)levelInfo.HeightShift) * 256f);

            cameraGO.transform.localPosition = new Vector3(objectInstance.X / 256f, objectInstance.Z * yFactor, objectInstance.Y / 256f);
            cameraGO.transform.localRotation = Quaternion.Euler(-objectInstance.Pitch / 256f * 360f, objectInstance.Yaw / 256f * 360f, -objectInstance.Roll / 256f * 360f);

            Camera camera = cameraGO.AddComponent<Camera>();
            camera.targetTexture = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            camera.fieldOfView = 80f;
            camera.nearClipPlane = 0.1f;
            camera.enabled = false;

            return new SystemShock.LevelInfo.SurveillanceCamera() {
                Camera = camera
            };
        }
        
        private static T ReadChunk<T>(KnownChunkId chunkId, ResourceFile mapLibrary) {
            ChunkInfo chunkInfo = mapLibrary.GetChunkInfo(chunkId);
            using (MemoryStream ms = new MemoryStream(mapLibrary.GetChunkData(chunkInfo))) {
                BinaryReader msbr = new BinaryReader(ms);
                return msbr.Read<T>();
            }
        }

        private static ushort[] ReadTextureList(KnownChunkId mapId, ResourceFile mapLibrary) {
            ChunkInfo chunkInfo = mapLibrary.GetChunkInfo(mapId + 0x0007);
            using (MemoryStream ms = new MemoryStream(mapLibrary.GetChunkData(chunkInfo))) {
                BinaryReader msbr = new BinaryReader(ms);

                uint amount = (uint)ms.Length / sizeof(ushort);

                ushort[] textureMap = new ushort[Math.Max(amount, 64)];

                for (uint i = 0; i < amount; ++i)
                    textureMap[i] = msbr.ReadUInt16();

                return textureMap;
            }
        }

        private static Tile[,] ReadTiles(KnownChunkId mapId, ResourceFile mapLibrary, LevelInfo levelInfo) {
            ChunkInfo chunkInfo = mapLibrary.GetChunkInfo(mapId + 0x0005);

            if (chunkInfo.info.ContentType != ContentType.Map)
                throw new ArgumentException("Chunk is not map.");

            using (MemoryStream ms = new MemoryStream(mapLibrary.GetChunkData(chunkInfo))) {
                BinaryReader msbr = new BinaryReader(ms);

                Tile[,] tiles = new Tile[levelInfo.Width, levelInfo.Height];

                for (uint y = 0; y < levelInfo.Width; ++y)
                    for (uint x = 0; x < levelInfo.Height; ++x)
                        tiles[x, y] = msbr.Read<Tile>();

                return tiles;
            }
        }
    }

    public struct CombinedTileMesh {
        public Mesh Mesh;
        public Material[] Materials;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct LevelInfo {
        public enum LevelInfoFlags : uint {
            Normal,
            Cyberspace
        }

        public uint Width;
        public uint Height;
        public uint LogWidth;
        public uint LogHeight;
        public uint HeightShift;
        public uint Unknown;
        public LevelInfoFlags Flags;

        public override string ToString() {
            return string.Format(@"Width = {0}, Height = {1}, LogWidth = {2}, LogHeight = {3}, HeightShift = {4}, Unknown = {5}, Flags = {6}",
                                 Width, Height, LogWidth, LogHeight, HeightShift, Unknown, Flags);
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct LevelVariables {
        public uint Size;
        public byte Radiation;
        public byte BioContamination;
        public byte BioIsGravity;
        public byte RadiationEnabled;
        public byte BioContaminationEnabled;

        //[MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 85)]
        //public byte[] Unknown;
    }

    public static class MapUtils {
        public static TileType[] invertedTypes = new TileType[] {
            TileType.Solid,
            TileType.Open,
            TileType.OpenDiagonalSW,
            TileType.OpenDiagonalSE,
            TileType.OpenDiagonalNE,
            TileType.OpenDiagonalNW,
            TileType.SlopeNS,
            TileType.SlopeEW,
            TileType.SlopeSN,
            TileType.SlopeWE,
            TileType.RidgeNW_SE,
            TileType.RidgeNE_SW,
            TileType.RidgeSE_NW,
            TileType.RidgeSW_NE,
            TileType.ValleySE_NW,
            TileType.ValleySW_NE,
            TileType.ValleyNW_SE,
            TileType.ValleyNE_SW
        };

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

        public static int[][] faceTriangles = new int[][] {
            new int[] { },
            new int[] { 0, 1, 2, 2, 3, 0 },

            new int[] { 2, 3, 0 },
            new int[] { 0, 1, 3 },
            new int[] { 0, 1, 2 },
            new int[] { 1, 2, 3 },

            new int[] { 0, 1, 2, 2, 3, 0 },
            new int[] { 0, 1, 2, 2, 3, 0 },
            new int[] { 0, 1, 2, 2, 3, 0 },
            new int[] { 0, 1, 2, 2, 3, 0 },

            new int[] { 0, 1, 3, 1, 2, 3 },
            new int[] { 0, 1, 2, 2, 3, 0 },
            new int[] { 0, 1, 3, 1, 2, 3 },
            new int[] { 0, 1, 2, 2, 3, 0 },

            new int[] { 0, 1, 3, 1, 2, 3 },
            new int[] { 0, 1, 2, 2, 3, 0 },
            new int[] { 0, 1, 3, 1, 2, 3 },
            new int[] { 0, 1, 2, 2, 3, 0 }
        };
    }

    public enum TileType : byte {
        Solid,
        Open,
        OpenDiagonalSE,
        OpenDiagonalSW,
        OpenDiagonalNW,
        OpenDiagonalNE,
        SlopeSN,
        SlopeWE,
        SlopeNS,
        SlopeEW,
        ValleySE_NW,
        ValleySW_NE,
        ValleyNW_SE,
        ValleyNE_SW,
        RidgeNW_SE,
        RidgeNE_SW,
        RidgeSE_NW,
        RidgeSW_NE
    };

    public enum CyberspacePull : byte {
        None,
        WeakEast,
        WeakWest,
        WeakNorth,
        WeakSouth,
        MediumEast,
        MediumWest,
        MediumNorth,
        MediumSouth,
        StrongEast,
        StrongWest,
        StrongNorth,
        StrongSouth,
        MediumCeiling,
        MediumFloor,
        StrongCeiling,
    };

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Tile {
        [Flags]
        public enum FlagMask : uint {
            TextureOffset = 0x0000001F,
            TextureFlip = 0x00000060,
            UseAdjacentWallTexture = 0x00000100,
            SpookyMusic = 0x00000200,
            SlopeControl = 0x00000C00,
            Music = 0x0000F000,
            ShadeLower = 0x000F0000,
            ShadeUpper = 0x0F000000,
            TileVisited = 0x80000000,

            CyberspacePull = 0x000F0000,
            CyberspaceGOL = 0x00000060,

            Unknown = 0x70F0F080
        };

        [Flags]
        public enum SlopeControl : uint {
            Normal = 0x00000000,
            CeilingReversed = 0x00000400,
            FloorOnly = 0x00000800,
            CeilingOnly = 0x00000C00
        };

        [Flags]
        public enum TextureInfoMask : ushort {
            WallTexture = 0x003F,
            CeilingTexture = 0x07C0,
            FloorTexture = 0xF800,

            CyberspaceFloor = 0x00FF,
            CyberspaceCeiling = 0xFF00,
        }

        [Flags]
        public enum InfoMask : byte {
            Height = 0x1F,
            Orientation = 0x60,
            Hazard = 0x80
        }

        [Flags]
        public enum FlipMask : byte {
            Flip = 0x01,
            Alternate = 0x02
        }

        public enum Orientation : byte {
            North = 0x00,
            East = 0x20,
            South = 0x40,
            West = 0x60
        }

        public TileType Type;
        public InfoMask FloorInfo;      // bit 0-4 Height from down to top, 5-6 orientation, 7 biohazard
        public InfoMask CeilingInfo;    // bit 0-4 Height from top to down, 5-6 orientation, 7 radiation hazard
        public byte SlopeSteepnessFactor;
        public ushort IndexFirstObject;
        public TextureInfoMask TextureInfo;
        public FlagMask Flags;
        public uint State;

        public const int MAX_HEIGHT = 0x20;

        public int FloorHeight { get { return (int)(FloorInfo & InfoMask.Height); } }
        public Orientation FloorOrientation { get { return (Orientation)(FloorInfo & InfoMask.Orientation); } }
        public bool FloorHazard { get { return (FloorInfo & InfoMask.Hazard) == InfoMask.Hazard; } }
        public ushort FloorTexture { get { return (ushort)((ushort)(TextureInfo & TextureInfoMask.FloorTexture) >> 11); } }

        public int CeilingHeight { get { return MAX_HEIGHT - (int)(CeilingInfo & InfoMask.Height); } }
        public Orientation CeilingOrientation { get { return (Orientation)(CeilingInfo & InfoMask.Orientation); } }
        public bool CeilingHazard { get { return (CeilingInfo & InfoMask.Hazard) == InfoMask.Hazard; } }
        public ushort CeilingTexture { get { return (ushort)((ushort)(TextureInfo & TextureInfoMask.CeilingTexture) >> 6); } }

        public ushort WallTexture { get { return (ushort)(TextureInfo & TextureInfoMask.WallTexture); } }

        public byte TextureOffset { get { return (byte)(Flags & FlagMask.TextureOffset); } }

        public bool UseAdjacentTexture { get { return (Flags & FlagMask.UseAdjacentWallTexture) == FlagMask.UseAdjacentWallTexture; } }

        public bool TextureFlip { get { return ((FlipMask)((byte)(Flags & Tile.FlagMask.TextureFlip) >> 5) & FlipMask.Flip) == FlipMask.Flip; } }

        public bool TextureAlternate { get { return ((FlipMask)((byte)(Flags & Tile.FlagMask.TextureFlip) >> 5) & FlipMask.Alternate) == FlipMask.Alternate; } }
    }
}
