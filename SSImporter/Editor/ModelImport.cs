using UnityEngine;
using UnityEditor;

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using SystemShock.Resource;

namespace SSImporter.Resource {
    public class ModelImport {
        [MenuItem("Assets/System Shock/8. Import Models", false, 1008)]
        public static void Init() {
            CreateMeshAssets();
        }


        [MenuItem("Assets/System Shock/8. Import Models", true)]
        public static bool Validate() {
            return PlayerPrefs.HasKey(@"SSHOCKRES");
        }

        private static void CreateMeshAssets() {
            string filePath = PlayerPrefs.GetString(@"SSHOCKRES");

            string obj3dPath = filePath + @"\DATA\obj3d.res";

            if (!File.Exists(obj3dPath))
                return;

            PaletteLibrary paletteLibrary = PaletteLibrary.GetLibrary();
            Palette gamePalette = paletteLibrary.GetResource(KnownChunkId.Palette);

            TextureLibrary textureLibrary = TextureLibrary.GetLibrary();

            try {
                AssetDatabase.StartAssetEditing();

                ResourceFile obj3dResource = new ResourceFile(obj3dPath);

                #region Create assets
                AssetDatabase.CreateFolder(@"Assets/SystemShock", @"obj3d.res");

                ModelLibrary modelLibrary = ScriptableObject.CreateInstance<ModelLibrary>();
                AssetDatabase.CreateAsset(modelLibrary, @"Assets/SystemShock/obj3d.res.asset");

                ResourceLibrary.GetController().AddLibrary(modelLibrary);

                foreach (KnownChunkId chunkId in obj3dResource.GetChunkList()) {
                    string assetPath = string.Format(@"Assets/SystemShock/obj3d.res/{0}.prefab", (ushort)chunkId);

                    MeshInfo meshInfo = ReadMesh(chunkId, obj3dResource);

                    UnityEngine.Object prefabAsset = PrefabUtility.CreateEmptyPrefab(assetPath);
                    AssetDatabase.AddObjectToAsset(meshInfo.mesh, assetPath);

                    Material[] materials = new Material[meshInfo.textureIds.Length];
                    for (int i = 0; i < materials.Length; ++i) {
                        bool isColored = ((meshInfo.textureIds[i] >> 24) & 0x80) == 0x80;
                        ushort textureId = (ushort)meshInfo.textureIds[i];
                        byte color = (byte)(meshInfo.textureIds[i] >> 16);

                        if (isColored) {
                            Material colorMaterial = materials[i] = new Material(Shader.Find(@"Standard"));
                            colorMaterial.color = gamePalette[color];
                            AssetDatabase.AddObjectToAsset(colorMaterial, assetPath);
                        } else {
                            materials[i] = textureLibrary.GetResource(KnownChunkId.ModelTexturesStart + textureId);
                        }
                    }

                    GameObject gameObject = new GameObject();

                    MeshFilter meshFilter = gameObject.AddComponent<MeshFilter>();
                    meshFilter.sharedMesh = meshInfo.mesh;

                    MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();
                    meshRenderer.materials = materials;

                    EditorUtility.SetDirty(gameObject);
                    GameObject prefabGameObject = PrefabUtility.ReplacePrefab(gameObject, prefabAsset, ReplacePrefabOptions.ConnectToPrefab);

                    modelLibrary.AddResource(chunkId, prefabGameObject);

                    GameObject.DestroyImmediate(gameObject);
                }

                EditorUtility.SetDirty(modelLibrary);
                #endregion
            } finally {
                AssetDatabase.StopAssetEditing();
                EditorApplication.SaveAssets();
            }

            AssetDatabase.Refresh();
        }

        private static MeshInfo ReadMesh(KnownChunkId modelChunkId, ResourceFile obj3dResource) {
            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<Polygon> polygons = new List<Polygon>();

            //Debug.Log("Mesh " + modelChunkId);

            ChunkInfo chunkInfo = obj3dResource.GetChunkInfo(modelChunkId);
            using (MemoryStream ms = new MemoryStream(obj3dResource.GetChunkData(chunkInfo))) {
                BinaryReader msbr = new BinaryReader(ms);

                msbr.ReadBytes(8);

                Polygon polygon = new Polygon();

                while (ms.Position < ms.Length) {
                    ushort command = msbr.ReadUInt16();

                    //Debug.Log(command.ToString("x4"));

                    if (command == 0x0000) { // Unknown
                        continue;
                    } else if (command == 0x0001) { // Define face
                        /*ushort length = */msbr.ReadUInt16();

                        Vector3 normal = new Vector3(msbr.ReadFixed1616(), -msbr.ReadFixed1616(), msbr.ReadFixed1616());
                        Vector3 point = new Vector3(msbr.ReadFixed1616(), -msbr.ReadFixed1616(), msbr.ReadFixed1616());

                        polygon = new Polygon() {
                            Normal = normal,
                            Point = point
                        };
                    } else if (command == 0x0003) { // Define vertices
                        ushort count = msbr.ReadUInt16();
                        ushort vertexIndex = msbr.ReadUInt16();

                        while (count-- > 0) {
                            vertices.Insert(vertexIndex, new Vector3(msbr.ReadFixed1616(), -msbr.ReadFixed1616(), msbr.ReadFixed1616()));
                            uvs.Insert(vertexIndex++, new Vector2());
                        }
                    } else if (command == 0x0004) { // Draw flat shaded polygon
                        ushort count = msbr.ReadUInt16();

                        polygon.vertices = new Vector3[count];
                        polygon.uvs = new Vector2[count];

                        while (count-- > 0) {
                            ushort vertexIndex = msbr.ReadUInt16();
                            polygon.vertices[count] = vertices[vertexIndex];
                            polygon.uvs[count] = uvs[vertexIndex];
                        }

                        polygon.textureMapped = false;

                        polygons.Add(polygon);
                    } else if (command == 0x0005) { // Set flat shading colour
                        polygon.color = msbr.ReadUInt16();
                    } else if (command == 0x0006) { // ?
                        /*Vector3 normal = */new Vector3(msbr.ReadFixed1616(), msbr.ReadFixed1616(), msbr.ReadFixed1616());
                        /*Vector3 point = */new Vector3(msbr.ReadFixed1616(), msbr.ReadFixed1616(), msbr.ReadFixed1616());

                        /*ushort leftOffset = */msbr.ReadUInt16();
                        /*ushort rightOffset = */msbr.ReadUInt16();
                    } else if (command == 0x000A) {
                        ushort vertexIndex = msbr.ReadUInt16();
                        ushort referenceVertex = msbr.ReadUInt16();

                        Vector3 vertex = vertices[referenceVertex];
                        vertex.x += msbr.ReadFixed1616();

                        vertices.Insert(vertexIndex, vertex);
                        uvs.Insert(vertexIndex, uvs[referenceVertex]);
                    } else if (command == 0x000B) {
                        ushort vertexIndex = msbr.ReadUInt16();
                        ushort referenceVertex = msbr.ReadUInt16();

                        Vector3 vertex = vertices[referenceVertex];
                        vertex.y += -msbr.ReadFixed1616();

                        vertices.Insert(vertexIndex, vertex);
                        uvs.Insert(vertexIndex, uvs[referenceVertex]);
                    } else if (command == 0x000C) {
                        ushort vertexIndex = msbr.ReadUInt16();
                        ushort referenceVertex = msbr.ReadUInt16();

                        Vector3 vertex = vertices[referenceVertex];
                        vertex.z += msbr.ReadFixed1616();

                        vertices.Insert(vertexIndex, vertex);
                        uvs.Insert(vertexIndex, uvs[referenceVertex]);
                    } else if (command == 0x000D) {
                        ushort vertexIndex = msbr.ReadUInt16();
                        ushort referenceVertex = msbr.ReadUInt16();

                        Vector3 vertex = vertices[referenceVertex];
                        vertex.x += msbr.ReadFixed1616();
                        vertex.y += -msbr.ReadFixed1616();

                        vertices.Insert(vertexIndex, vertex);
                        uvs.Insert(vertexIndex, uvs[referenceVertex]);
                    } else if (command == 0x000E) {
                        ushort vertexIndex = msbr.ReadUInt16();
                        ushort referenceVertex = msbr.ReadUInt16();

                        Vector3 vertex = vertices[referenceVertex];
                        vertex.x += msbr.ReadFixed1616();
                        vertex.z += msbr.ReadFixed1616();

                        vertices.Insert(vertexIndex, vertex);
                        uvs.Insert(vertexIndex, uvs[referenceVertex]);
                    } else if (command == 0x000F) {
                        ushort vertexIndex = msbr.ReadUInt16();
                        ushort referenceVertex = msbr.ReadUInt16();

                        Vector3 vertex = vertices[referenceVertex];
                        vertex.y += -msbr.ReadFixed1616();
                        vertex.z += msbr.ReadFixed1616();

                        vertices.Insert(vertexIndex, vertex);
                        uvs.Insert(vertexIndex, uvs[referenceVertex]);
                    } else if (command == 0x0015) {
                        ushort vertexIndex = msbr.ReadUInt16();
                        vertices.Insert(vertexIndex, new Vector3(msbr.ReadFixed1616(), -msbr.ReadFixed1616(), msbr.ReadFixed1616()));
                        uvs.Insert(vertexIndex, new Vector2());
                    } else if (command == 0x001C) {
                        polygon.color = msbr.ReadUInt16();
                        polygon.shade = msbr.ReadUInt16();
                    } else if (command == 0x0025) {
                        ushort count = msbr.ReadUInt16();

                        while (count-- > 0)
                            uvs[msbr.ReadUInt16()] = new Vector2(msbr.ReadFixed1616(), 1f - msbr.ReadFixed1616());
                    } else if (command == 0x0026) { // Draw textured
                        ushort textureId = msbr.ReadUInt16();
                        ushort count = msbr.ReadUInt16();

                        polygon.vertices = new Vector3[count];
                        polygon.uvs = new Vector2[count];

                        while (count-- > 0) {
                            ushort vertexIndex = msbr.ReadUInt16();
                            polygon.vertices[count] = vertices[vertexIndex];
                            polygon.uvs[count] = uvs[vertexIndex];
                        }

                        polygon.textureId = textureId;
                        polygon.textureMapped = true;

                        polygons.Add(polygon);
                    } else {
                        Debug.Log("Unknown command " + command.ToString("x4"));
                        continue;
                    }
                }
            }

            List<Vector3> meshVertices = new List<Vector3>();
            List<Vector2> meshUVs = new List<Vector2>();

            Dictionary<uint, List<int>> submeshes = new Dictionary<uint, List<int>>();
            foreach (Polygon polygon in polygons) {
                uint key = polygon.textureMapped ? polygon.textureId : ((uint)(polygon.color & 0xFF) | 0x8000) << 16;

                List<int> triangles;
                if (!submeshes.TryGetValue(key, out triangles))
                    submeshes.Add(key, triangles = new List<int>());

                int vertexIndex = meshVertices.Count;

                Array.Reverse(polygon.vertices);
                Array.Reverse(polygon.uvs);

                meshVertices.AddRange(polygon.vertices);
                meshUVs.AddRange(polygon.uvs);

                for (int i = 0; i < (polygon.vertices.Length - 2); ++i) {
                    triangles.Add(vertexIndex);
                    triangles.Add(vertexIndex + i + 1);
                    triangles.Add(vertexIndex + i + 2);
                }
            }

            Mesh mesh = new Mesh();
            mesh.vertices = meshVertices.ToArray();
            mesh.uv = meshUVs.ToArray();

            uint[] textureIds = new uint[submeshes.Count];

            mesh.subMeshCount = submeshes.Count;

            int submesh = 0;
            foreach (KeyValuePair<uint, List<int>> kvp in submeshes) {
                mesh.SetTriangles(kvp.Value.ToArray(), submesh);
                textureIds[submesh++] = kvp.Key;
            }

            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.Optimize();
            mesh.RecalculateBounds();

            MeshUtility.SetMeshCompression(mesh, ModelImporterMeshCompression.High);
            MeshUtility.Optimize(mesh);

            return new MeshInfo() {
                mesh = mesh,
                textureIds = textureIds
            };
        }

        private struct MeshInfo {
            public Mesh mesh;
            public uint[] textureIds;
        }

        private struct Polygon {
            public Vector3 Normal;
            public Vector3 Point;
            public ushort color;
            public ushort shade;
            public ushort textureId;

            public bool textureMapped;

            public Vector3[] vertices;
            public Vector2[] uvs;
        }
    }
}