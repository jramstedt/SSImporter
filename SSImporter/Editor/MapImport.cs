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
        [MenuItem("Assets/System Shock/10. Import Maps")]
        public static void Init() {
            CreateMapAssets();
        }

        private delegate void ObjectFactoryDelegate(ObjectInstance objectInstance, ObjectData objectData, GameObject gameObject);

        private static void CreateMapAssets() {
            string filePath = PlayerPrefs.GetString(@"SSHOCKRES");

            string mapLibraryPath = filePath + @"\DATA\archive.dat";

            if (!File.Exists(mapLibraryPath))
                return;
            
            ResourceFile mapLibrary = new ResourceFile(mapLibraryPath);

            LoadLevel(KnownChunkId.Level1Start, mapLibrary);
        }

        private static LevelInfo levelInfo;
        private static ushort[] textureMap;

        private static TextureLibrary textureLibrary;

        private static void LoadLevel(KnownChunkId mapId, ResourceFile mapLibrary) {
            //AssetDatabase.StartAssetEditing();

            levelInfo = ReadLevelInfo(mapId, mapLibrary);
            textureMap = ReadTextureList(mapId, mapLibrary);
            Tile[,] tileMap = ReadTiles(mapId, mapLibrary, levelInfo);
            TileMesh[,] tileMeshes = new TileMesh[levelInfo.Width, levelInfo.Height];

            #region Create LevelInfo
            GameObject levelInfoGO = new GameObject(@"LevelInfo");
            SystemShock.LevelInfo levelInfoRuntime = levelInfoGO.AddComponent<SystemShock.LevelInfo>();
            levelInfoRuntime.HeightFactor = (float)Tile.MAX_HEIGHT / ((1 << (int)levelInfo.HeightPower) * 256f);
            levelInfoRuntime.TextureMap = textureMap;
            #endregion

            textureLibrary = TextureLibrary.GetLibrary(@"texture.res");

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

                            GameObject floorGo = CreateGameObject(CombineTile(movingFloor, tileMeshes, true), "Moving floor", true);
                            floorGo.transform.SetParent(tileGO.transform, false);
                        }

                        if (tileMesh.CeilingMoving) {
                            MovingTileMesh movingCeiling = new MovingTileMesh(MovingTileMesh.Type.Ceiling, levelInfo, tile, x, y, movingFloorHeightRange[x, y], movingCeilingHeightRange[x, y]);

                            GameObject ceilingGo = CreateGameObject(CombineTile(movingCeiling, tileMeshes, true), "Moving ceiling", true);
                            ceilingGo.transform.SetParent(tileGO.transform, false);
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
                    TileMesh tileMesh = tileMeshes[x, y];

                    if (tileMesh.tile.Type != TileType.Solid) {
                        lightProbes.Add(new Vector3(x + 0.5f, tileMesh.FloorHeightMiddle / (float)(1 << (int)levelInfo.HeightPower) + 0.25f, y + 0.5f));
                        lightProbes.Add(new Vector3(x + 0.5f, tileMesh.CeilingHeightMiddle / (float)(1 << (int)levelInfo.HeightPower) - 0.25f, y + 0.5f));
                    }
                }
            }
            lightProbeGroub.probePositions = lightProbes.ToArray();
            #endregion

            ObjectInstance[] objectInstances = mapLibrary.ReadArrayOf<ObjectInstance>(mapId + 0x0008);

            #region Surveillance nodes
            ushort[] surveillanceNodeIndices = mapLibrary.ReadArrayOf<ushort>(mapId + 0x002B);
            List<Camera> surveillanceCamera = new List<Camera>(surveillanceNodeIndices.Length);
            for (int nodeIndex = 0; nodeIndex < surveillanceNodeIndices.Length; ++nodeIndex)
                surveillanceCamera.Add(CreateCamera(objectInstances[surveillanceNodeIndices[nodeIndex]]));

            levelInfoRuntime.SurveillanceCamera = surveillanceCamera.ToArray();
            #endregion

            ObjectFactory objectFactory = ObjectFactory.GetController();
            objectFactory.UpdateLevelInfo();

            #region Objects
            foreach (ObjectInstance objectInstance in objectInstances) {
                if (objectInstance.InUse == 0)
                    continue;

                GameObject instanceGO = objectFactory.Instantiate(objectInstance, instanceDatas[(byte)objectInstance.Class][objectInstance.ClassTableIndex]);
                EditorUtility.SetDirty(instanceGO);
            }
            #endregion

            StaticOcclusionCulling.smallestOccluder = 0.5f;
            StaticOcclusionCulling.smallestHole = 0.1f;
            StaticOcclusionCulling.Compute();

            AssetDatabase.SaveAssets();
            //AssetDatabase.StopAssetEditing();

            Resources.UnloadUnusedAssets();
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

        private static Camera CreateCamera(ObjectInstance objectInstance) {
            GameObject cameraGO = new GameObject();
            cameraGO.name = "Surveillance Camera";

            float yFactor = (float)Tile.MAX_HEIGHT / ((1 << (int)levelInfo.HeightPower) * 256f);

            cameraGO.transform.localPosition = new Vector3(objectInstance.X / 256f, objectInstance.Z * yFactor, objectInstance.Y / 256f);
            cameraGO.transform.localRotation = Quaternion.Euler(-objectInstance.Pitch / 256f * 360f, objectInstance.Yaw / 256f * 360f, -objectInstance.Roll / 256f * 360f);

            Camera camera = cameraGO.AddComponent<Camera>();
            camera.targetTexture = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            camera.fieldOfView = 80f;
            camera.nearClipPlane = 0.1f;
            camera.enabled = false;

            return camera;
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
        public uint HeightPower;
        public uint TileMapPointer;
        public LevelInfoFlags Flags;

        public override string ToString() {
            return string.Format(@"Width = {0}, Height = {1}, LogWidth = {2}, LogHeight = {3}, HeightPower = {4}, TileMapPointer = {5}, Flags = {6}",
                                 Width, Height, LogWidth, LogHeight, HeightPower, TileMapPointer, Flags);
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
