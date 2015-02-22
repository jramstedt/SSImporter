using UnityEngine;
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
        [MenuItem("Assets/System Shock/Import Maps")]
        public static void Init() {
            CreateMapAssets();
        }

        //private static Material spriteMaterial;

        private delegate void ObjectFactoryDelegate(ObjectInstance objectInstance, ObjectData objectData, GameObject gameObject);

        private static void CreateMapAssets() {
            string filePath = @"D:\Users\Janne\Downloads\SYSTEMSHOCK-Portable-v1.2.3\RES";
            string mapLibraryPath = filePath + @"\DATA\archive.dat";

            if (!File.Exists(mapLibraryPath))
                return;
            /*
            spriteMaterial = new Material(Shader.Find(@"Standard"));
            spriteMaterial.color = Color.white;
            spriteMaterial.SetFloat(@"_Mode", 2f); // Transparent
            spriteMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            spriteMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            spriteMaterial.SetInt("_ZWrite", 0);
            spriteMaterial.DisableKeyword("_ALPHATEST_ON");
            spriteMaterial.EnableKeyword("_ALPHABLEND_ON");
            spriteMaterial.renderQueue = 3000;
            */
            ResourceFile mapLibrary = new ResourceFile(mapLibraryPath);

            LoadLevel(KnownChunkId.Level1Start, mapLibrary);
        }

        private static Dictionary<DrawType, ObjectFactoryDelegate> ObjectFactory = new Dictionary<DrawType, ObjectFactoryDelegate> {
            {DrawType.Model, ModelFactory},
            {DrawType.Sprite, SpriteFactory},
            {DrawType.Decal, DecalFactory},
            {DrawType.Special, SpecialFactory},
            {DrawType.ForceDoor, ForceDoorFactory},
        };

        private static Dictionary<uint, ObjectFactoryDelegate> ObjectFactoryOverride = new Dictionary<uint, ObjectFactoryDelegate> {
            {ObjectHash(ObjectClass.Decoration, 2, 0), SignFactory},
            {ObjectHash(ObjectClass.Decoration, 2, 1), IconFactory},
            {ObjectHash(ObjectClass.Decoration, 2, 2), GraffitiFactory},
            {ObjectHash(ObjectClass.Decoration, 2, 3), TextFactory},
            {ObjectHash(ObjectClass.Decoration, 2, 4), SignFactory},
            {ObjectHash(ObjectClass.Decoration, 2, 5), SignFactory},
            {ObjectHash(ObjectClass.Decoration, 2, 6), ScreenFactory},

            {ObjectHash(ObjectClass.Decoration, 2, 8), ScreenFactory},
            {ObjectHash(ObjectClass.Decoration, 2, 9), ScreenFactory},
            {ObjectHash(ObjectClass.Decoration, 2, 10), SignFactory},

            {ObjectHash(ObjectClass.Decoration, 5, 4), CameraFactory},
            {ObjectHash(ObjectClass.Decoration, 4, 5), FurnitureFactory},
            {ObjectHash(ObjectClass.Decoration, 1, 5), FurnitureFactory},
            {ObjectHash(ObjectClass.Decoration, 1, 6), FurnitureFactory},
            {ObjectHash(ObjectClass.Decoration, 1, 7), FurnitureFactory},
            {ObjectHash(ObjectClass.Decoration, 1, 8), FurnitureFactory},

            {ObjectHash(ObjectClass.Decoration, 7, 0), BridgeFactory},
            {ObjectHash(ObjectClass.Decoration, 7, 1), CatwalkFactory},
            {ObjectHash(ObjectClass.Decoration, 7, 7), ForceBridgeFactory},

            {ObjectHash(ObjectClass.Interface, 3, 8), InterfaceFactory},
            {ObjectHash(ObjectClass.Interface, 3, 7), InterfaceFactory},
            {ObjectHash(ObjectClass.Interface, 3, 6), InterfaceFactory},
            {ObjectHash(ObjectClass.Interface, 3, 5), InterfaceFactory},
            {ObjectHash(ObjectClass.Interface, 3, 4), InterfaceFactory},
            {ObjectHash(ObjectClass.Interface, 3, 3), InterfaceFactory},
            {ObjectHash(ObjectClass.Interface, 3, 2), InterfaceFactory},
            {ObjectHash(ObjectClass.Interface, 3, 1), InterfaceFactory},
            {ObjectHash(ObjectClass.Interface, 3, 0), InterfaceFactory},
            {ObjectHash(ObjectClass.Interface, 1, 6), InterfaceFactory},
            {ObjectHash(ObjectClass.Interface, 1, 3), InterfaceFactory},
            {ObjectHash(ObjectClass.Interface, 0, 7), InterfaceFactory},
            {ObjectHash(ObjectClass.Interface, 0, 6), InterfaceFactory},
            {ObjectHash(ObjectClass.Interface, 0, 5), InterfaceFactory},
            {ObjectHash(ObjectClass.Interface, 0, 4), InterfaceFactory},
            {ObjectHash(ObjectClass.Interface, 0, 3), InterfaceFactory},
            {ObjectHash(ObjectClass.Interface, 0, 2), InterfaceFactory},
            {ObjectHash(ObjectClass.Interface, 0, 1), InterfaceFactory},
            {ObjectHash(ObjectClass.Interface, 0, 0), InterfaceFactory},

            {ObjectHash(ObjectClass.Container, 0, 0), SmallCrateFactory},
            {ObjectHash(ObjectClass.Container, 0, 1), LargeCrateFactory},
            {ObjectHash(ObjectClass.Container, 0, 2), SecureCrateFactory},

            {ObjectHash(ObjectClass.Item, 6, 0), StainsFactory},
            {ObjectHash(ObjectClass.Item, 6, 1), StainsFactory},
            {ObjectHash(ObjectClass.Item, 6, 2), StainsFactory},
            {ObjectHash(ObjectClass.Item, 6, 3), StainsFactory},
            {ObjectHash(ObjectClass.Item, 6, 4), StainsFactory},
            {ObjectHash(ObjectClass.Item, 6, 5), StainsFactory},
            {ObjectHash(ObjectClass.Item, 6, 6), StainsFactory},
            {ObjectHash(ObjectClass.Item, 6, 7), StainsFactory},
            {ObjectHash(ObjectClass.Item, 6, 8), StainsFactory},

            {ObjectHash(ObjectClass.Item, 1, 5), StainsFactory},
            {ObjectHash(ObjectClass.Item, 1, 8), StainsFactory},
            {ObjectHash(ObjectClass.Item, 1, 9), StainsFactory},

            {ObjectHash(ObjectClass.Animated, 0, 2), InterfaceFactory},
        };

        private static uint ObjectHash(ObjectClass Class, byte Subclass, byte Type) {
            return (uint)Class << 24 | (uint)Subclass << 16 | Type;
        }

        private static LevelInfo levelInfo;
        private static ushort[] textureMap;
        private static ObjectInstance[] surveillanceNodes;

        private static TextureLibrary textureLibrary;

        private static void LoadLevel(KnownChunkId mapId, ResourceFile mapLibrary) {
            //AssetDatabase.StartAssetEditing();

            levelInfo = ReadLevelInfo(mapId, mapLibrary);
            textureMap = ReadTextureList(mapId, mapLibrary);
            Tile[,] tileMap = ReadTiles(mapId, mapLibrary, levelInfo);
            TileMesh[,] tileMeshes = new TileMesh[levelInfo.Width, levelInfo.Height];

            textureLibrary = AssetDatabase.LoadAssetAtPath(@"Assets/SystemShock/texture.res.asset", typeof(TextureLibrary)) as TextureLibrary;

            #region Read class tables
            object[][] instanceDatas = new object[][] {
                mapLibrary.ReadArrayOf<ObjectInstance.Weapon>(mapId + 0x000A),
                mapLibrary.ReadArrayOf<ObjectInstance.Ammunition>(mapId + 0x000B),
                mapLibrary.ReadArrayOf<ObjectInstance.Projectile>(mapId + 0x000C),
                mapLibrary.ReadArrayOf<ObjectInstance.Explosive>(mapId + 0x000D),
                mapLibrary.ReadArrayOf<ObjectInstance.DermalPatch>(mapId + 0x000E),
                mapLibrary.ReadArrayOf<ObjectInstance.Hardware>(mapId + 0x000F),
                new ObjectInstance.SoftwareAndLog[0],
                new ObjectInstance.Decoration[0],
                mapLibrary.ReadArrayOf<ObjectInstance.Item>(mapId + 0x0012),
                mapLibrary.ReadArrayOf<ObjectInstance.Interface>(mapId + 0x0013),
                mapLibrary.ReadArrayOf<ObjectInstance.DoorAndGrating>(mapId + 0x0014),
                mapLibrary.ReadArrayOf<ObjectInstance.Animated>(mapId + 0x0015),
                mapLibrary.ReadArrayOf<ObjectInstance.Trigger>(mapId + 0x0016),
                mapLibrary.ReadArrayOf<ObjectInstance.Container>(mapId + 0x0017),
                mapLibrary.ReadArrayOf<ObjectInstance.Enemy>(mapId + 0x0018)
            };

            if (mapId == KnownChunkId.ShodanCyberspaceStart || mapId == KnownChunkId.Cyberspace12Start || mapId == KnownChunkId.Cyberspace39Start) {
                ObjectInstance.SoftwareAndLog[] SoftwareAndLogsA = mapLibrary.ReadArrayOf<ObjectInstance.SoftwareAndLog>(mapId + 0x0010);
                ObjectInstance.SoftwareAndLog[] SoftwareAndLogsB = mapLibrary.ReadArrayOf<ObjectInstance.SoftwareAndLog>(mapId + 0x0011);

                object[] SoftwareAndLogs = new ObjectInstance.SoftwareAndLog[SoftwareAndLogsA.Length + SoftwareAndLogsB.Length];
                SoftwareAndLogsA.CopyTo(SoftwareAndLogs, 0);
                SoftwareAndLogsB.CopyTo(SoftwareAndLogs, SoftwareAndLogsA.Length);

                instanceDatas[(byte)ObjectClass.SoftwareAndLog] = SoftwareAndLogs;
            } else {
                instanceDatas[(byte)ObjectClass.SoftwareAndLog] = mapLibrary.ReadArrayOf<ObjectInstance.SoftwareAndLog>(mapId + 0x0010);
                instanceDatas[(byte)ObjectClass.Decoration] = mapLibrary.ReadArrayOf<ObjectInstance.Decoration>(mapId + 0x0011);
            }
            #endregion

            #region Find moving tiles
            int[,][] movingFloorHeightRange = new int[levelInfo.Width, levelInfo.Height][]; // Min and max height
            int[,][] movingCeilingHeightRange = new int[levelInfo.Width, levelInfo.Height][]; // Min and max height

            for (uint y = 0; y < levelInfo.Width; ++y) {
                for (uint x = 0; x < levelInfo.Height; ++x) {
                    Tile tile = tileMap[x, y];
                    movingFloorHeightRange[x, y] = new int[] { tile.FloorHeight, tile.FloorHeight  };
                    movingCeilingHeightRange[x, y] = new int[] { tile.CeilingHeight, tile.CeilingHeight  };
                }
            }

            foreach (ObjectInstance.Trigger trigger in instanceDatas[(byte)ObjectClass.Trigger]) {
                if (trigger.Action == ActionType.MovePlatform) {
                    ObjectInstance.Trigger.MovingPlatform movingPlatform = trigger.Data.Read<ObjectInstance.Trigger.MovingPlatform>();

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
                if (@interface.Action == ActionType.MovePlatform) {
                    ObjectInstance.Trigger.MovingPlatform movingPlatform = @interface.Data.Read<ObjectInstance.Trigger.MovingPlatform>();

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

            #region Create Tiles
            for (uint y = 0; y < levelInfo.Width; ++y) {
                for (uint x = 0; x < levelInfo.Height; ++x) {
                    tileMeshes[x, y] = new TileMesh(levelInfo, tileMap[x, y], x, y, movingFloorHeightRange[x, y], movingCeilingHeightRange[x, y]);
                }
            }
            #endregion

            #region Construct Tiles
            for (uint y = 0; y < levelInfo.Width; ++y) {
                for (uint x = 0; x < levelInfo.Height; ++x) {
                    Tile tile = tileMap[x, y];

                    if (tile.Type != TileType.Solid) {
                        TileMesh tileMesh = tileMeshes[x, y];
                        GameObject tileGO = CreateGameObject(CombineTile(tileMesh, tileMeshes), @"Tile " + x.ToString() + " " + y.ToString());

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

                            GameObject floorGo = CreateGameObject(CombineTile(movingFloor, tileMeshes, true), "Moving floor");
                            floorGo.transform.SetParent(tileGO.transform, false);
                        }

                        if (tileMesh.CeilingMoving) {
                            MovingTileMesh movingCeiling = new MovingTileMesh(MovingTileMesh.Type.Ceiling, levelInfo, tile, x, y, movingFloorHeightRange[x, y], movingCeilingHeightRange[x, y]);

                            GameObject ceilingGo = CreateGameObject(CombineTile(movingCeiling, tileMeshes, true), "Moving ceiling");
                            ceilingGo.transform.SetParent(tileGO.transform, false);
                        }

                        //MeshCollider meshCollider = gameObject.AddComponent<MeshCollider>();
                        //meshCollider.sharedMesh = mesh;

                        tileGO.transform.localPosition = new Vector3(x, 0, y); // start from bottom left

                        EditorUtility.SetDirty(tileGO);
                    }
                }
            }
            #endregion

            #region Light probes
            //GameObject lightProbesGameObject = new GameObject();
            //LightProbeGroup lightProbes = lightProbesGameObject.AddComponent<LightProbeGroup>();
            #endregion

            StringLibrary stringLibrary = AssetDatabase.LoadAssetAtPath(@"Assets/SystemShock/cybstrng.res.asset", typeof(StringLibrary)) as StringLibrary;
            CyberString objectNames = stringLibrary.GetStrings(KnownChunkId.ObjectNames);
            ObjectPropertyLibrary objectPropertyLibrary = AssetDatabase.LoadAssetAtPath(@"Assets/SystemShock/objprop.dat.asset", typeof(ObjectPropertyLibrary)) as ObjectPropertyLibrary;

            float yFactor = (float)Tile.MAX_HEIGHT / ((1 << (int)levelInfo.HeightFactor) * 256f);

            ObjectInstance[] objectInstances = mapLibrary.ReadArrayOf<ObjectInstance>(mapId + 0x0008);

            #region Surveillance nodes
            ushort[] surveillanceNodeIndices = mapLibrary.ReadArrayOf<ushort>(mapId + 0x002B);
            surveillanceNodes = new ObjectInstance[surveillanceNodeIndices.Length];
            for (int nodeIndex = 0; nodeIndex < surveillanceNodes.Length; ++nodeIndex)
                surveillanceNodes[nodeIndex] = objectInstances[surveillanceNodeIndices[nodeIndex]];
            #endregion

            #region Objects
            foreach (ObjectInstance objectInstance in objectInstances) {
                if (objectInstance.InUse == 0)
                    continue;

                int nameIndex = objectPropertyLibrary.GetIndex(objectInstance.Class, objectInstance.SubClass, objectInstance.Type);
                if (nameIndex == -1) {
                    Debug.LogWarning(string.Format(@"Unknown {0},{1} {2}:{3}:{4}", objectInstance.X / 256f, objectInstance.Y / 256f, objectInstance.Class, objectInstance.SubClass, objectInstance.Type));
                    continue;
                }

                string name = objectNames[(uint)nameIndex];

                GameObject gameObject = new GameObject(name);

                gameObject.transform.localScale = new Vector3(1f, 1f, 1f);
                gameObject.transform.localPosition = new Vector3(Mathf.Round(64f * objectInstance.X / 256f) / 64f, objectInstance.Z * yFactor, Mathf.Round(64f * objectInstance.Y / 256f) / 64f);
                gameObject.transform.localRotation = Quaternion.Euler(-objectInstance.Pitch / 256f * 360f, objectInstance.Yaw / 256f * 360f, -objectInstance.Roll / 256f * 360f);

                #region Static properties
                ObjectData objectData = objectPropertyLibrary.GetObject<ObjectData>(objectInstance.Class, objectInstance.SubClass, objectInstance.Type);
                SystemShockObjectProperties properties = gameObject.AddComponent(Type.GetType(objectData.GetType().FullName + @"MonoBehaviour, Assembly-CSharp")) as SystemShockObjectProperties;
                properties.SetProperties(objectData);
                #endregion

                SystemShockObject ssObject = gameObject.AddComponent(Type.GetType(@"SystemShock.InstanceObjects." + objectInstance.Class + @", Assembly-CSharp")) as SystemShockObject;
                ssObject.Class = (SystemShock.Object.ObjectClass)objectInstance.Class;
                ssObject.SubClass = objectInstance.SubClass;
                ssObject.Type = objectInstance.Type;

                ssObject.AIIndex = objectInstance.AIIndex;
                ssObject.Hitpoints = objectInstance.Hitpoints;
                ssObject.AnimationState = objectInstance.AnimationState;

                ssObject.Unknown1 = objectInstance.Unknown1;
                ssObject.Unknown2 = objectInstance.Unknown2;

                ssObject.SetClassData(instanceDatas[(byte)objectInstance.Class][objectInstance.ClassTableIndex]);

                if (objectData.Base.DrawType == DrawType.NoDraw)
                    continue;

                ObjectFactoryDelegate Factory;
                if (!ObjectFactoryOverride.TryGetValue(ObjectHash(objectInstance.Class, objectInstance.SubClass, objectInstance.Type), out Factory))
                    ObjectFactory.TryGetValue(objectData.Base.DrawType, out Factory);

                if (Factory != null)
                    Factory(objectInstance, objectData, gameObject);

                EditorUtility.SetDirty(gameObject);
            }
            #endregion

            StaticOcclusionCulling.smallestOccluder = 0.5f;
            StaticOcclusionCulling.smallestHole = 0.1f;
            StaticOcclusionCulling.Compute();

            AssetDatabase.SaveAssets();
            //AssetDatabase.StopAssetEditing();

            Resources.UnloadUnusedAssets();
        }

        private static GameObject CreateGameObject(CombinedTileMesh combinedTileMesh, string name) {
            GameObject gameObject = new GameObject();
            gameObject.name = name;
            GameObjectUtility.SetStaticEditorFlags(gameObject, StaticEditorFlags.LightmapStatic |
                                                                StaticEditorFlags.OccluderStatic |
                                                                StaticEditorFlags.NavigationStatic |
                                                                StaticEditorFlags.OccludeeStatic |
                                                                StaticEditorFlags.OffMeshLinkGeneration |
                                                                StaticEditorFlags.ReflectionProbeStatic);

            MeshFilter meshFilter = gameObject.AddComponent<MeshFilter>();
            meshFilter.mesh = combinedTileMesh.Mesh;

            MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterials = combinedTileMesh.Materials;

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

        private static void ModelFactory(ObjectInstance objectInstance, ObjectData objectData, GameObject gameObject) {
            ModelLibrary modelLibrary = AssetDatabase.LoadAssetAtPath(@"Assets/SystemShock/obj3d.res.asset", typeof(ModelLibrary)) as ModelLibrary;

            BaseProperties baseProperties = objectData.Base;

            string modelPath = AssetDatabase.GUIDToAssetPath(modelLibrary.GetModelGuid(baseProperties.ModelIndex));
            GameObject modelGO = PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath(modelPath, typeof(GameObject))) as GameObject;
            modelGO.transform.SetParent(gameObject.transform, false);

            bool isStatic = (baseProperties.Flags & (int)Flags.Static) == (int)Flags.Static;

            if (isStatic) {
                GameObjectUtility.SetStaticEditorFlags(modelGO, StaticEditorFlags.BatchingStatic |
                                                                StaticEditorFlags.LightmapStatic |
                                                                StaticEditorFlags.OccluderStatic |
                                                                StaticEditorFlags.NavigationStatic |
                                                                StaticEditorFlags.OccludeeStatic |
                                                                StaticEditorFlags.OffMeshLinkGeneration |
                                                                StaticEditorFlags.ReflectionProbeStatic);
            } else {
                Rigidbody rigidBody = gameObject.AddComponent<Rigidbody>();
                rigidBody.freezeRotation = true;
            }

            if ((baseProperties.Flags & (int)Flags.Solid) == (int)Flags.Solid) {
                MeshFilter meshFilter = gameObject.GetComponentInChildren<MeshFilter>();

                if ((baseProperties.Flags & (int)Flags.MeshCollider) == (int)Flags.MeshCollider) {
                    MeshCollider meshCollider = gameObject.AddComponent<MeshCollider>();
                    meshCollider.sharedMesh = meshFilter.sharedMesh;
                    
                    if(!isStatic)
                        meshCollider.convex = true;
                } else {
                    Bounds bounds = meshFilter.sharedMesh.bounds;
                    BoxCollider boxCollider = gameObject.AddComponent<BoxCollider>();
                    boxCollider.center = bounds.center;
                    boxCollider.size = bounds.size;
                }
            }
        }

        private static void SpriteFactory(ObjectInstance objectInstance, ObjectData objectData, GameObject gameObject) {
            SpriteLibrary objartLibrary = AssetDatabase.LoadAssetAtPath(@"Assets/SystemShock/objart.res.asset", typeof(SpriteLibrary)) as SpriteLibrary;
            ObjectPropertyLibrary objectPropertyLibrary = AssetDatabase.LoadAssetAtPath(@"Assets/SystemShock/objprop.dat.asset", typeof(ObjectPropertyLibrary)) as ObjectPropertyLibrary;

            BaseProperties baseProperties = objectData.Base;

            uint spriteIndex = objectPropertyLibrary.GetSpriteOffset(objectInstance.Class, objectInstance.SubClass, objectInstance.Type);
            spriteIndex += 1; // World sprite.

            SpriteDefinition sprite = objartLibrary.GetSpriteAnimation(0)[spriteIndex];
            Material material = objartLibrary.GetMaterial();

            GameObject spriteGO = new GameObject();
            MeshFilter meshFilter = spriteGO.AddComponent<MeshFilter>();
            meshFilter.mesh = MeshUtils.CreateTwoSidedPlane(
                sprite.Pivot,
                new Vector2(sprite.Rect.width * material.mainTexture.width / 100f, sprite.Rect.height * material.mainTexture.height / 100f),
                sprite.Rect);
            MeshRenderer meshRenderer = spriteGO.AddComponent<MeshRenderer>();

            meshRenderer.sharedMaterial = material;
            spriteGO.transform.SetParent(gameObject.transform, false);

            spriteGO.AddComponent<Billboard>();

            bool isStatic = (baseProperties.Flags & (int)Flags.Static) == (int)Flags.Static;

            if(!isStatic) {
                Rigidbody rigidBody = gameObject.AddComponent<Rigidbody>();
                rigidBody.freezeRotation = true;
            }

            if ((baseProperties.Flags & (int)Flags.Solid) == (int)Flags.Solid) {
                BoxCollider boxCollider = gameObject.AddComponent<BoxCollider>();
                boxCollider.center = gameObject.transform.InverseTransformPoint(meshRenderer.bounds.center);
                boxCollider.size = gameObject.transform.InverseTransformDirection(meshRenderer.bounds.size);
            } else if(!isStatic) {
                SphereCollider sphereCollider = gameObject.AddComponent<SphereCollider>();
                sphereCollider.center = gameObject.transform.InverseTransformPoint(meshRenderer.bounds.center);
                sphereCollider.radius = Mathf.Min(meshRenderer.bounds.size.x, meshRenderer.bounds.size.y, meshRenderer.bounds.size.z) / 2f;
            }
        }

        private static void ArtObjectFactory(SpriteLibrary objartLibrary, ushort spriteIndex, uint frame, ObjectData objectData, GameObject gameObject) {
            SpriteDefinition sprite = objartLibrary.GetSpriteAnimation(spriteIndex)[frame];
            Material material = objartLibrary.GetMaterial();

            GameObject spriteGO = new GameObject();
            MeshFilter meshFilter = spriteGO.AddComponent<MeshFilter>();
            meshFilter.mesh = MeshUtils.CreateTwoSidedPlane(
                sprite.Pivot,
                new Vector2(sprite.Rect.width * material.mainTexture.width / 64f, sprite.Rect.height * material.mainTexture.height / 64f),
                sprite.Rect);
            MeshRenderer meshRenderer = spriteGO.AddComponent<MeshRenderer>();

            meshRenderer.sharedMaterial = material;
            spriteGO.transform.SetParent(gameObject.transform, false);

            if ((objectData.Base.Flags & (int)Flags.Solid) == (int)Flags.Solid) {
                MeshCollider meshCollider = gameObject.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = meshFilter.sharedMesh;
            }

            GameObjectUtility.SetStaticEditorFlags(spriteGO,    StaticEditorFlags.LightmapStatic |
                                                                StaticEditorFlags.OccludeeStatic);

            EditorUtility.SetDirty(spriteGO);
        }

        private static void DecalFactory(ObjectInstance objectInstance, ObjectData objectData, GameObject gameObject) {
            ObjectPropertyLibrary objectPropertyLibrary = AssetDatabase.LoadAssetAtPath(@"Assets/SystemShock/objprop.dat.asset", typeof(ObjectPropertyLibrary)) as ObjectPropertyLibrary;
            int nameIndex = objectPropertyLibrary.GetIndex(objectInstance.Class, objectInstance.SubClass, objectInstance.Type);

            int spriteIndex = 270 + (nameIndex - 299); // 270 = starting index in objart3, 299 = starting index in objproperty

            SpriteLibrary objart3Library = AssetDatabase.LoadAssetAtPath(@"Assets/SystemShock/objart3.res.asset", typeof(SpriteLibrary)) as SpriteLibrary;
            ArtObjectFactory(objart3Library, (ushort)spriteIndex, objectInstance.AnimationState, objectData, gameObject);
        }

        private static void SpecialFactory(ObjectInstance objectInstance, ObjectData objectData, GameObject gameObject) {
            // Special cases are handled by ObjectFactoryOverride
        }

        public static void FurnitureFactory(ObjectInstance objectInstance, ObjectData objectData, GameObject gameObject) {
            ModelFactory(objectInstance, objectData, gameObject);

            InstanceObjects.Decoration decoration = gameObject.GetComponent<InstanceObjects.Decoration>();
            ObjectInstance.Decoration.Furniture furniture = decoration.ClassData.Data.Read<ObjectInstance.Decoration.Furniture>();

            ushort materialOverride = 51;

            if ((furniture.TextureOverride & 0x80) == 0x80)
                materialOverride += (ushort)(furniture.TextureOverride & 0x7F);

            TextureLibrary modelTextureLibrary = AssetDatabase.LoadAssetAtPath(@"Assets/SystemShock/citmat.res.asset", typeof(TextureLibrary)) as TextureLibrary;
            string zeroMaterialGuid = modelTextureLibrary.GetTextureGuid(0);

            MeshRenderer meshRenderer = gameObject.GetComponentInChildren<MeshRenderer>();
            Material[] sharedMaterials = meshRenderer.sharedMaterials;
            for (int i = 0; i < sharedMaterials.Length; ++i) {
                string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(sharedMaterials[i]));
                if (zeroMaterialGuid == guid)
                    sharedMaterials[i] = modelTextureLibrary.GetMaterial(materialOverride);
            }
            meshRenderer.sharedMaterials = sharedMaterials;
        }

        private static void IconFactory(ObjectInstance objectInstance, ObjectData objectData, GameObject gameObject) {
            SpriteLibrary objart3Library = AssetDatabase.LoadAssetAtPath(@"Assets/SystemShock/objart3.res.asset", typeof(SpriteLibrary)) as SpriteLibrary;
            ArtObjectFactory(objart3Library, (ushort)311, objectInstance.AnimationState, objectData, gameObject);
        }

        private static void SignFactory(ObjectInstance objectInstance, ObjectData objectData, GameObject gameObject) {
            SpriteLibrary objartLibrary = AssetDatabase.LoadAssetAtPath(@"Assets/SystemShock/objart.res.asset", typeof(SpriteLibrary)) as SpriteLibrary;

            ObjectPropertyLibrary objectPropertyLibrary = AssetDatabase.LoadAssetAtPath(@"Assets/SystemShock/objprop.dat.asset", typeof(ObjectPropertyLibrary)) as ObjectPropertyLibrary;
            uint spriteIndex = objectPropertyLibrary.GetSpriteOffset(objectInstance.Class, objectInstance.SubClass, objectInstance.Type);

            ArtObjectFactory(objartLibrary, (ushort)0, spriteIndex + objectInstance.AnimationState + 1, objectData, gameObject);
        }

        private static void ScreenFactory(ObjectInstance objectInstance, ObjectData objectData, GameObject gameObject) {
            //SpriteLibrary objartLibrary = AssetDatabase.LoadAssetAtPath(@"Assets/SystemShock/objart.res.asset", typeof(SpriteLibrary)) as SpriteLibrary;
            TextureLibrary animationLibrary = AssetDatabase.LoadAssetAtPath(@"Assets/SystemShock/texture.res.anim.asset", typeof(TextureLibrary)) as TextureLibrary;

            //ObjectPropertyLibrary objectPropertyLibrary = AssetDatabase.LoadAssetAtPath(@"Assets/SystemShock/objprop.dat.asset", typeof(ObjectPropertyLibrary)) as ObjectPropertyLibrary;
            //uint spriteIndex = objectPropertyLibrary.GetSpriteOffset(objectInstance.Class, objectInstance.SubClass, objectInstance.Type);


            //ArtObjectFactory(objartLibrary, (ushort)0, spriteIndex + objectInstance.AnimationState + 1, objectData, gameObject);

            //Sprite sprite = (Sprite)objartLibrary.GetSpriteAnimation(spriteIndex)[frame];

            InstanceObjects.Decoration decoration = gameObject.GetComponent<InstanceObjects.Decoration>();
            ObjectInstance.Decoration.Screen screen = decoration.ClassData.Data.Read<ObjectInstance.Decoration.Screen>();

            bool isSurveillance = false;
            float screenSize = objectInstance.Type == 9 ? 1f : 0.5f;
            ushort frameIndex = screen.StartFrameIndex;
            if (frameIndex > 101) {
                if (frameIndex == 246) {
                    frameIndex = 63;
                } else if (frameIndex >= 0xF8 && frameIndex <= 0xFF) { // Surveillance
                    frameIndex = (ushort)(frameIndex & 0x07);
                    isSurveillance = true;
                } else {
                    return; // TODO
                }
            }

            GameObject spriteGO = new GameObject();
            MeshFilter meshFilter = spriteGO.AddComponent<MeshFilter>();
            meshFilter.mesh = MeshUtils.CreateTwoSidedPlane();
            MeshRenderer meshRenderer = spriteGO.AddComponent<MeshRenderer>();

            Material material = isSurveillance ? new Material(Shader.Find(@"Standard")) : animationLibrary.GetMaterial((ushort)(frameIndex + objectInstance.AnimationState));

            if (isSurveillance) {
                RenderTexture renderTexture = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                material.mainTexture = renderTexture;

                Camera camera = CreateCamera(surveillanceNodes[frameIndex], renderTexture);
                Surveillance surveillance = spriteGO.AddComponent<Surveillance>();
                surveillance.Camera = camera;
            }

            meshRenderer.sharedMaterial = material;

            /*
            MaterialPropertyBlock materialPropertyBlock = new MaterialPropertyBlock();
            meshRenderer.GetPropertyBlock(materialPropertyBlock);
            materialPropertyBlock.SetVector(Shader.PropertyToID(@"_MainTex_ST"),
                new Vector4(sprite.rect.width / sprite.texture.width,
                            sprite.rect.height / sprite.texture.height,
                            sprite.rect.x / sprite.texture.width,
                            sprite.rect.y / sprite.texture.height));
            meshRenderer.SetPropertyBlock(materialPropertyBlock);
            */
            spriteGO.transform.localScale = new Vector3(screenSize, screenSize, 1f);
            spriteGO.transform.SetParent(gameObject.transform, false);
            /*
            if ((objectData.Base.Flags & (int)Flags.Solid) == (int)Flags.Solid) {
                MeshCollider meshCollider = gameObject.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = meshFilter.sharedMesh;
            }
            */
            GameObjectUtility.SetStaticEditorFlags(spriteGO,    StaticEditorFlags.LightmapStatic |
                                                                StaticEditorFlags.OccludeeStatic);

            EditorUtility.SetDirty(spriteGO);
        }

        private static Camera CreateCamera(ObjectInstance objectInstance, RenderTexture renderTarget) {
            GameObject cameraGO = new GameObject();
            cameraGO.name = "Surveillance Camera";

            float yFactor = (float)Tile.MAX_HEIGHT / ((1 << (int)levelInfo.HeightFactor) * 256f);

            cameraGO.transform.localScale = new Vector3(1f, 1f, 1f);
            cameraGO.transform.localPosition = new Vector3(objectInstance.X / 256f, objectInstance.Z * yFactor, objectInstance.Y / 256f);
            cameraGO.transform.localRotation = Quaternion.Euler(-objectInstance.Pitch / 256f * 360f, objectInstance.Yaw / 256f * 360f, -objectInstance.Roll / 256f * 360f);

            Camera camera = cameraGO.AddComponent<Camera>();
            camera.targetTexture = renderTarget;
            camera.fieldOfView = 80f;
            camera.nearClipPlane = 0.1f;
            camera.enabled = false;

            return camera;
        }

        private static void StainsFactory(ObjectInstance objectInstance, ObjectData objectData, GameObject gameObject) {
            SpriteLibrary objartLibrary = AssetDatabase.LoadAssetAtPath(@"Assets/SystemShock/objart.res.asset", typeof(SpriteLibrary)) as SpriteLibrary;

            ObjectPropertyLibrary objectPropertyLibrary = AssetDatabase.LoadAssetAtPath(@"Assets/SystemShock/objprop.dat.asset", typeof(ObjectPropertyLibrary)) as ObjectPropertyLibrary;
            uint spriteIndex = objectPropertyLibrary.GetSpriteOffset(objectInstance.Class, objectInstance.SubClass, objectInstance.Type);

            ArtObjectFactory(objartLibrary, (ushort)0, spriteIndex + objectInstance.AnimationState + 1, objectData, gameObject);
        }

        private static void TextFactory(ObjectInstance objectInstance, ObjectData objectData, GameObject gameObject) {
            InstanceObjects.Decoration decoration = gameObject.GetComponent<InstanceObjects.Decoration>();
            ObjectInstance.Decoration.Text text = decoration.ClassData.Data.Read<ObjectInstance.Decoration.Text>();

            PaletteLibrary paletteLibrary = AssetDatabase.LoadAssetAtPath(@"Assets/SystemShock/gamepal.res.asset", typeof(PaletteLibrary)) as PaletteLibrary;
            Palette gamePalette = paletteLibrary.GetPalette(KnownChunkId.Palette);

            uint[] fontMap = new uint[] {
                606,
                609,
                602,
                605,
                607
            };

            float[] sizeMap = new float[] {
                0.155f,
                0.0775f,
                0.0385f,
                0.08f,
                0.15f,
                0.15f
            };

            string assetPath = string.Format(@"Assets/SystemShock/gamescr.res/{0}.fontsettings", fontMap[text.Font & 0x000F]);
            Font font = AssetDatabase.LoadAssetAtPath(assetPath, typeof(Font)) as Font;
            StringLibrary stringLibrary = AssetDatabase.LoadAssetAtPath(@"Assets/SystemShock/cybstrng.res.asset", typeof(StringLibrary)) as StringLibrary;
            CyberString decalWords = stringLibrary.GetStrings(KnownChunkId.DecalWords);

            GameObject textGO = new GameObject();
            MeshRenderer meshRenderer = textGO.AddComponent<MeshRenderer>();

            TextMesh textMesh = textGO.AddComponent<TextMesh>();
            textMesh.offsetZ = -0.0001f;
            textMesh.font = font;
            textMesh.alignment = TextAlignment.Center;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.color = gamePalette[text.Color != 0 ? (uint)text.Color : 60];
            textMesh.richText = false;
            textMesh.characterSize = sizeMap[(text.Font & 0x00F0) >> 4];
            textMesh.text = decalWords[text.TextIndex];

            meshRenderer.sharedMaterial = font.material;

            textGO.transform.SetParent(gameObject.transform, false);
        }

        private static void InterfaceFactory(ObjectInstance objectInstance, ObjectData objectData, GameObject gameObject) {
            //ObjectPropertyLibrary objectPropertyLibrary = AssetDatabase.LoadAssetAtPath(@"Assets/SystemShock/objprop.dat.asset", typeof(ObjectPropertyLibrary)) as ObjectPropertyLibrary;
            //int nameIndex = objectPropertyLibrary.GetIndex(objectInstance.Class, objectInstance.SubClass, objectInstance.Type);
            //SpriteFactory((ushort)nameIndex, objectInstance, objectData, gameObject);

            ObjectPropertyLibrary objectPropertyLibrary = AssetDatabase.LoadAssetAtPath(@"Assets/SystemShock/objprop.dat.asset", typeof(ObjectPropertyLibrary)) as ObjectPropertyLibrary;

            uint spriteIndex = objectPropertyLibrary.GetSpriteOffset(objectInstance.Class, objectInstance.SubClass, objectInstance.Type);
            spriteIndex += 1; // World sprite.

            //int spriteIndex = 881 + (nameIndex - 284); // 270 = starting index in objart3, 284 = starting index in objproperty

            SpriteLibrary objartLibrary = AssetDatabase.LoadAssetAtPath(@"Assets/SystemShock/objart.res.asset", typeof(SpriteLibrary)) as SpriteLibrary;
            ArtObjectFactory(objartLibrary, (ushort)0, (uint)spriteIndex, objectData, gameObject);
        }

        private static void GraffitiFactory(ObjectInstance objectInstance, ObjectData objectData, GameObject gameObject) {
            SpriteLibrary objart3Library = AssetDatabase.LoadAssetAtPath(@"Assets/SystemShock/objart3.res.asset", typeof(SpriteLibrary)) as SpriteLibrary;
            ArtObjectFactory(objart3Library, (ushort)312, objectInstance.AnimationState, objectData, gameObject);
        }

        private static void WalkableFactory(ObjectInstance objectInstance, ObjectData objectData, GameObject gameObject, float defaultWidth) {
            TextureLibrary modelTextureLibrary = AssetDatabase.LoadAssetAtPath(@"Assets/SystemShock/citmat.res.asset", typeof(TextureLibrary)) as TextureLibrary;
            TextureLibrary textureLibrary = AssetDatabase.LoadAssetAtPath(@"Assets/SystemShock/texture.res.asset", typeof(TextureLibrary)) as TextureLibrary;

            InstanceObjects.Decoration decoration = gameObject.GetComponent<InstanceObjects.Decoration>();
            ObjectInstance.Decoration.Bridge bridge = decoration.ClassData.Data.Read<ObjectInstance.Decoration.Bridge>();

            int bridgeWidth = bridge.Size & (byte)ObjectInstance.Decoration.Bridge.SizeMask.X;
            int bridgeLength = (bridge.Size & (byte)ObjectInstance.Decoration.Bridge.SizeMask.Y) >> 4;
            byte topBottomTexture = (byte)(bridge.TopBottomTextures & (byte)ObjectInstance.Decoration.Bridge.TextureMask.Texture);
            byte sideTexture = (byte)(bridge.SideTextures & (byte)ObjectInstance.Decoration.Bridge.TextureMask.Texture);

            float width = bridgeWidth > 0 ? (float)bridgeWidth / (float)0x04 : defaultWidth;
            float length = bridgeLength > 0 ? (float)bridgeLength / (float)0x04 : 1f;
            float height = bridge.Height > 0 ? (float)bridge.Height / 32f : 1f / 32f;

            Material topBottomMaterial = bridge.TopBottomTextures > 0 ?
                    (bridge.TopBottomTextures & (byte)ObjectInstance.Decoration.Bridge.TextureMask.MapTexture) == (byte)ObjectInstance.Decoration.Bridge.TextureMask.MapTexture ?
                    textureLibrary.GetMaterial(textureMap[topBottomTexture]) :
                    modelTextureLibrary.GetMaterial((ushort)(51 + topBottomTexture)) :
                textureLibrary.GetMaterial(textureMap[0]);

            Material sideMaterial = bridge.SideTextures > 0 ?
                    (bridge.SideTextures & (byte)ObjectInstance.Decoration.Bridge.TextureMask.MapTexture) == (byte)ObjectInstance.Decoration.Bridge.TextureMask.MapTexture ?
                    textureLibrary.GetMaterial(textureMap[sideTexture]) :
                    modelTextureLibrary.GetMaterial((ushort)(51 + sideTexture)) :
                textureLibrary.GetMaterial(textureMap[0]);

            GameObject bridgeGO = new GameObject();
            MeshFilter meshFilter = bridgeGO.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = MeshUtils.CreateCubeTopPivot(width, length, height);
            MeshRenderer meshRenderer = bridgeGO.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterials = new Material[] {
                topBottomMaterial,
                sideMaterial
            };
            MeshCollider meshCollider = gameObject.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = meshFilter.sharedMesh;

            bridgeGO.transform.SetParent(gameObject.transform, false);

            GameObjectUtility.SetStaticEditorFlags(bridgeGO, StaticEditorFlags.BatchingStatic |
                                                                StaticEditorFlags.LightmapStatic |
                                                                StaticEditorFlags.OccluderStatic |
                                                                StaticEditorFlags.NavigationStatic |
                                                                StaticEditorFlags.OccludeeStatic |
                                                                StaticEditorFlags.OffMeshLinkGeneration |
                                                                StaticEditorFlags.ReflectionProbeStatic);
        }

        private static void BridgeFactory(ObjectInstance objectInstance, ObjectData objectData, GameObject gameObject) {
            WalkableFactory(objectInstance, objectData, gameObject, 1f);
        }

        private static void CatwalkFactory(ObjectInstance objectInstance, ObjectData objectData, GameObject gameObject) {
            WalkableFactory(objectInstance, objectData, gameObject, 0.5f);
        }

        private static void SmallCrateFactory(ObjectInstance objectInstance, ObjectData objectData, GameObject gameObject) {
            CrateFactory(new Vector3(8f / (float)Tile.MAX_HEIGHT, 8f / (float)Tile.MAX_HEIGHT, 8f / (float)Tile.MAX_HEIGHT), objectInstance, objectData, gameObject);
        }

        private static void LargeCrateFactory(ObjectInstance objectInstance, ObjectData objectData, GameObject gameObject) {
            CrateFactory(new Vector3(16f / (float)Tile.MAX_HEIGHT, 16f / (float)Tile.MAX_HEIGHT, 16f / (float)Tile.MAX_HEIGHT), objectInstance, objectData, gameObject);
        }

        private static void SecureCrateFactory(ObjectInstance objectInstance, ObjectData objectData, GameObject gameObject) {
            CrateFactory(new Vector3(24f / (float)Tile.MAX_HEIGHT, 24f / (float)Tile.MAX_HEIGHT, 24f / (float)Tile.MAX_HEIGHT), objectInstance, objectData, gameObject);
        }

        private static void CrateFactory(Vector3 defaultSize, ObjectInstance objectInstance, ObjectData objectData, GameObject gameObject) {
            TextureLibrary modelTextureLibrary = AssetDatabase.LoadAssetAtPath(@"Assets/SystemShock/citmat.res.asset", typeof(TextureLibrary)) as TextureLibrary;

            InstanceObjects.Container container = gameObject.GetComponent<InstanceObjects.Container>();
            ObjectInstance.Container crate = container.ClassData;

            ushort materialBase = 51;

            Material topBottomMaterial = modelTextureLibrary.GetMaterial(crate.TopBottomTexture > 0 ? (ushort)(materialBase + crate.TopBottomTexture) : (ushort)63);
            Material sideMaterial = modelTextureLibrary.GetMaterial(crate.SideTexture > 0 ? (ushort)(materialBase + crate.SideTexture) : (ushort)62);

            Vector3 size = new Vector3( crate.Width > 0 ? crate.Width / (float)Tile.MAX_HEIGHT : defaultSize.x,
                                        crate.Height > 0 ? crate.Height / (float)Tile.MAX_HEIGHT : defaultSize.y,
                                        crate.Depth > 0 ? crate.Depth / (float)Tile.MAX_HEIGHT : defaultSize.z);

            GameObject crateGO = new GameObject();
            MeshFilter meshFilter = crateGO.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = MeshUtils.CreateCubeCenterPivot(size);
            MeshRenderer meshRenderer = crateGO.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterials = new Material[] {
                topBottomMaterial,
                sideMaterial
            };
            MeshCollider meshCollider = gameObject.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = meshFilter.sharedMesh;

            crateGO.transform.SetParent(gameObject.transform, false);

            GameObjectUtility.SetStaticEditorFlags(crateGO, StaticEditorFlags.BatchingStatic |
                                                            StaticEditorFlags.LightmapStatic |
                                                            StaticEditorFlags.OccluderStatic |
                                                            StaticEditorFlags.NavigationStatic |
                                                            StaticEditorFlags.OccludeeStatic |
                                                            StaticEditorFlags.OffMeshLinkGeneration |
                                                            StaticEditorFlags.ReflectionProbeStatic);
        }

        private static void CameraFactory(ObjectInstance objectInstance, ObjectData objectData, GameObject gameObject) {
            ModelFactory(objectInstance, objectData, gameObject);

            InstanceObjects.Decoration decoration = gameObject.GetComponent<InstanceObjects.Decoration>();
            ObjectInstance.Decoration.Camera camera = decoration.ClassData.Data.Read<ObjectInstance.Decoration.Camera>();

            if (camera.Rotating > 0) {
                // Add rotating script
            }
        }

        public static void ForceDoorFactory(ObjectInstance objectInstance, ObjectData objectData, GameObject gameObject) {
            InstanceObjects.DoorAndGrating door = gameObject.GetComponent<InstanceObjects.DoorAndGrating>();

            GameObject spriteGO = new GameObject();
            MeshFilter meshFilter = spriteGO.AddComponent<MeshFilter>();
            meshFilter.mesh = MeshUtils.CreateTwoSidedPlane();
            MeshRenderer meshRenderer = spriteGO.AddComponent<MeshRenderer>();

            Color color = new Color(0.5f, 0f, 0f, 0.75f);
            Color emission = new Color(0.25f, 0f, 0f, 1f);

            if (door.ClassData.ForceColor == 253) {
                color = new Color(0f, 0f, 0.75f, 0.75f);
                emission = new Color(0f, 0f, 0.4f, 1f);
            } else if (door.ClassData.ForceColor == 254) {
                color = new Color(0f, 0.75f, 0f, 0.75f);
                emission = new Color(0f, 0.4f, 0f, 1f);
            }

            Material colorMaterial = new Material(Shader.Find(@"Standard")); // TODO should be screen?
            colorMaterial.color = color;
            colorMaterial.SetFloat(@"_Mode", 2f); // Fade
            colorMaterial.SetColor(@"_EmissionColor", emission);
            colorMaterial.SetFloat(@"_Glossiness", 0f);

            colorMaterial.SetColor("_EmissionColorUI", colorMaterial.GetColor(@"_EmissionColor"));
            colorMaterial.SetFloat("_EmissionScaleUI", 1f);

            colorMaterial.EnableKeyword(@"_EMISSION");

            colorMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            colorMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            colorMaterial.SetInt("_ZWrite", 0);
            colorMaterial.DisableKeyword("_ALPHATEST_ON");
            colorMaterial.EnableKeyword("_ALPHABLEND_ON");
            colorMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            colorMaterial.renderQueue = 3000;
            meshRenderer.sharedMaterial = colorMaterial;

            spriteGO.transform.SetParent(gameObject.transform, false);

            if ((objectData.Base.Flags & (int)Flags.Solid) == (int)Flags.Solid) {
                MeshCollider meshCollider = gameObject.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = meshFilter.sharedMesh;
            }

            GameObjectUtility.SetStaticEditorFlags(spriteGO, StaticEditorFlags.LightmapStatic |
                                                             StaticEditorFlags.OccludeeStatic);

            EditorUtility.SetDirty(spriteGO);
        }

        public static void ForceBridgeFactory(ObjectInstance objectInstance, ObjectData objectData, GameObject gameObject) {
            InstanceObjects.Decoration decoration = gameObject.GetComponent<InstanceObjects.Decoration>();
            ObjectInstance.Decoration.Bridge bridge = decoration.ClassData.Data.Read<ObjectInstance.Decoration.Bridge>();

            int bridgeWidth = bridge.Size & (byte)ObjectInstance.Decoration.Bridge.SizeMask.X;
            int bridgeLength = (bridge.Size & (byte)ObjectInstance.Decoration.Bridge.SizeMask.Y) >> 4;

            float width = bridgeWidth > 0 ? (float)bridgeWidth / (float)0x04 : 1f;
            float length = bridgeLength > 0 ? (float)bridgeLength / (float)0x04 : 1f;
            float height = bridge.Height > 0 ? (float)bridge.Height / 32f : 0.03f;

            Color color = new Color(0.5f, 0f, 0f, 0.75f);
            Color emission = new Color(0.25f, 0f, 0f, 1f);

            if(bridge.ForceColor == 253) {
                color = new Color(0f, 0f, 0.75f, 0.75f);
                emission =  new Color(0f, 0f, 0.4f, 1f);
            } else if(bridge.ForceColor == 254) {
                color = new Color(0f, 0.75f, 0f, 0.75f);
                emission =  new Color(0f, 0.4f, 0f, 1f);
            }

            Material colorMaterial = new Material(Shader.Find(@"Standard")); // TODO should be screen?
            colorMaterial.color = color;
            colorMaterial.SetFloat(@"_Mode", 2f); // Fade
            colorMaterial.SetColor(@"_EmissionColor", emission);
            colorMaterial.SetFloat(@"_Glossiness", 0f);

            colorMaterial.SetColor("_EmissionColorUI", colorMaterial.GetColor(@"_EmissionColor"));
            colorMaterial.SetFloat("_EmissionScaleUI", 1f);

            colorMaterial.EnableKeyword(@"_EMISSION");

            colorMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            colorMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            colorMaterial.SetInt("_ZWrite", 0);
            colorMaterial.DisableKeyword("_ALPHATEST_ON");
            colorMaterial.EnableKeyword("_ALPHABLEND_ON");
            colorMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            colorMaterial.renderQueue = 3000;

            GameObject bridgeGO = new GameObject();
            MeshFilter meshFilter = bridgeGO.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = MeshUtils.CreateCubeTopPivot(width, length, height);
            MeshRenderer meshRenderer = bridgeGO.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterials = new Material[] {
                colorMaterial,
                colorMaterial
            };
            MeshCollider meshCollider = gameObject.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = meshFilter.sharedMesh;

            bridgeGO.transform.SetParent(gameObject.transform, false);

            GameObjectUtility.SetStaticEditorFlags(bridgeGO,    StaticEditorFlags.LightmapStatic |
                                                                StaticEditorFlags.OccludeeStatic);

            EditorUtility.SetDirty(bridgeGO);
        }

        private static uint GetGroundSpriteIndex(int spriteId) {
            return (uint)(spriteId * 3) + 2;
        }

        private static LevelInfo ReadLevelInfo(KnownChunkId mapId, ResourceFile mapLibrary) {
            ChunkInfo chunkInfo = mapLibrary.GetChunkInfo(mapId + 0x0004);
            using (MemoryStream ms = new MemoryStream(mapLibrary.GetChunkData(chunkInfo))) {
                BinaryReader msbr = new BinaryReader(ms);
                return msbr.Read<LevelInfo>();
            }
        }

        private static ushort[] ReadTextureList(KnownChunkId mapId, ResourceFile mapLibrary) {
            ChunkInfo chunkInfo = mapLibrary.GetChunkInfo(mapId + 0x0007);
            using (MemoryStream ms = new MemoryStream(mapLibrary.GetChunkData(chunkInfo))) {
                BinaryReader msbr = new BinaryReader(ms);

                uint amount = (uint)ms.Length / sizeof(ushort);
                ushort[] textureMap = new ushort[amount];

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
        public uint HeightFactor;
        public uint TileMapPointer;
        public LevelInfoFlags Flags;

        public override string ToString() {
            return string.Format(@"Width = {0}, Height = {1}, LogWidth = {2}, LogHeight = {3}, HeightFactor = {4}, TileMapPointer = {5}, Flags = {6}",
                                 Width, Height, LogWidth, LogHeight, HeightFactor, TileMapPointer, Flags);
        }
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
            FloorTexture = 0xF800
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
