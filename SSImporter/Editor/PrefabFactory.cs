using UnityEngine;
using UnityEditor;

using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

using SystemShock;
using SystemShock.Object;
using SystemShock.Resource;

namespace SSImporter.Resource {
    public static class PrefabFactory {
        [MenuItem("Assets/System Shock/9. Create Object Prefabs")]
        public static void Init() {
            CreateObjectPrefabs();
        }

        private static ModelLibrary modelLibrary;
        private static SpriteLibrary objartLibrary;
        private static StringLibrary stringLibrary;
        private static ObjectPropertyLibrary objectPropertyLibrary;
        private static Material nullMaterial;

        private static void CreateObjectPrefabs() {
            if (!Directory.Exists(Application.dataPath + @"/SystemShock"))
                AssetDatabase.CreateFolder(@"Assets", @"SystemShock");

            AssetDatabase.CreateFolder(@"Assets/SystemShock", @"Prefabs");

            modelLibrary = ModelLibrary.GetLibrary(@"obj3d.res");
            objartLibrary = SpriteLibrary.GetLibrary(@"objart.res");
            stringLibrary = StringLibrary.GetLibrary(@"cybstrng.res");
            objectPropertyLibrary = ObjectPropertyLibrary.GetLibrary(@"objprop.dat");
            nullMaterial = TextureLibrary.GetLibrary(@"citmat.res").GetMaterial(0);

            CyberString objectNames = stringLibrary.GetStrings(KnownChunkId.ObjectNames);

            PrefabLibrary prefabLibrary = ScriptableObject.CreateInstance<PrefabLibrary>();
            AssetDatabase.CreateAsset(prefabLibrary, @"Assets/SystemShock/objprefabs.asset");

            uint nameIndex = 0;

            for (byte classIndex = 0; classIndex < ObjectPropertyImport.ObjectDeclarations.Length; ++classIndex) {
                ObjectDeclaration[] objectDataSubclass = ObjectPropertyImport.ObjectDeclarations[classIndex];

                for (byte subclassIndex = 0; subclassIndex < objectDataSubclass.Length; ++subclassIndex) {
                    ObjectDeclaration objectDataType = objectDataSubclass[subclassIndex];

                    for (byte typeIndex = 0; typeIndex < objectDataType.Count; ++typeIndex) {
                        uint combinedId = (uint)classIndex << 24 | (uint)subclassIndex << 16 | typeIndex;

                        ObjectData objectData = objectPropertyLibrary.GetObject<ObjectData>(combinedId);
                        BaseProperties baseProperties = objectData.Base;

                        string fileName = string.Format(@"{0} {1}.prefab", ++nameIndex, objectData.FullName);
                        fileName = Path.GetInvalidFileNameChars().Aggregate(fileName, (current, c) => current.Replace(c.ToString(), string.Empty));

                        string assetPath = string.Format(@"Assets/SystemShock/Prefabs/{0}", fileName);
                        UnityEngine.Object prefabAsset = PrefabUtility.CreateEmptyPrefab(assetPath);

                        GameObject gameObject = new GameObject(objectData.FullName);
                        gameObject.transform.localPosition = Vector3.zero;
                        gameObject.transform.localRotation = Quaternion.identity;
                        gameObject.transform.localScale = Vector3.one;

                        SystemShockObjectProperties properties = gameObject.AddComponent(Type.GetType(objectData.GetType().FullName + @"MonoBehaviour, Assembly-CSharp")) as SystemShockObjectProperties;
                        properties.SetProperties(objectData);

                        if (baseProperties.DrawType == DrawType.Model)
                            AddModel(combinedId, baseProperties, gameObject);
                        else if (baseProperties.DrawType == DrawType.Sprite)
                            AddSprite(combinedId, baseProperties, gameObject);
                        else if (baseProperties.DrawType == DrawType.Screen)
                            AddScreen(combinedId, baseProperties, gameObject);
                        else if (baseProperties.DrawType == DrawType.Enemy)
                            Debug.LogWarning("DrawType.Enemy not supported", gameObject);
                        else if (baseProperties.DrawType == DrawType.T5)
                            Debug.LogWarning("DrawType.T5 not supported", gameObject);
                        else if (baseProperties.DrawType == DrawType.Fragments)
                            Debug.LogWarning("DrawType.Fragments not supported", gameObject);
                        else if (baseProperties.DrawType == DrawType.NoDraw)
                            Debug.Log("DrawType.NoDraw", gameObject);
                        else if (baseProperties.DrawType == DrawType.Decal)
                            AddDecal(combinedId, baseProperties, gameObject);
                        else if (baseProperties.DrawType == DrawType.T9)
                            Debug.LogWarning("DrawType.T9 not supported", gameObject);
                        else if (baseProperties.DrawType == DrawType.T10)
                            Debug.LogWarning("DrawType.T10 not supported", gameObject);
                        else if (baseProperties.DrawType == DrawType.Special)
                            AddSpecial(combinedId, baseProperties, gameObject);
                        else if (baseProperties.DrawType == DrawType.ForceDoor)
                            AddForceDoor(combinedId, baseProperties, gameObject);

                        StaticEditorFlags staticFlags = 0;

                        if (baseProperties.DrawType == DrawType.Decal || baseProperties.DrawType == DrawType.Screen)
                            staticFlags |= StaticEditorFlags.BatchingStatic | StaticEditorFlags.LightmapStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.NavigationStatic;

                        {
                            MeshFilter meshFilter = gameObject.GetComponent<MeshFilter>();
                            if (meshFilter && meshFilter.sharedMesh)
                                AssetDatabase.AddObjectToAsset(meshFilter.sharedMesh, prefabAsset);
                        }

                        bool HasPhysics = baseProperties.Rigidbody != 0;

                        #region Flags
                        if (((Flags)baseProperties.Flags & Flags.Collider) == Flags.Collider) {
                            MeshFilter meshFilter = gameObject.GetComponentInChildren<MeshFilter>();
                            Renderer renderer = gameObject.GetComponentInChildren<Renderer>();

                            if (meshFilter != null) {
                                MeshCollider meshCollider = gameObject.AddComponent<MeshCollider>();
                                meshCollider.sharedMesh = meshFilter.sharedMesh;

                                if (HasPhysics)
                                    meshCollider.convex = true;
                            } else if (renderer != null) {
                                SphereCollider sphereCollider = gameObject.AddComponent<SphereCollider>();
                                sphereCollider.center = gameObject.transform.InverseTransformPoint(renderer.bounds.center);
                                sphereCollider.radius = Mathf.Min(renderer.bounds.extents.x, renderer.bounds.extents.y);
                            } else {
                                Debug.LogWarning("Marked for collider, but has no mesh or renderer!" + gameObject.name, gameObject);
                            }
                        }

                        if (((Flags)baseProperties.Flags & Flags.Raycastable) == Flags.Raycastable ||
                            ((Flags)baseProperties.Flags & Flags.Touchable) == Flags.Touchable ||
                            ((Flags)baseProperties.Flags & Flags.UsefulItem) == Flags.UsefulItem) {
                            Renderer renderer = gameObject.GetComponentInChildren<Renderer>();

                            if (renderer == null) {
                                Debug.LogWarning("Marked for collider, but has no renderer! " + gameObject.name, gameObject);
                            } else if (gameObject.GetComponent<Collider>() == null) {
                                BoxCollider boxCollider = gameObject.AddComponent<BoxCollider>();
                                boxCollider.isTrigger = true;
                                boxCollider.center = renderer.bounds.center;
                                boxCollider.size = renderer.bounds.size;
                            }
                        }

                        if (((Flags)baseProperties.Flags & Flags.OpaqueClosed) == Flags.OpaqueClosed ||
                            ((Flags)baseProperties.Flags & Flags.Activable) == Flags.Activable) {
                            Collider collider = gameObject.GetComponent<Collider>();
                            if (collider == null) {
                                BoxCollider boxCollider = gameObject.AddComponent<BoxCollider>();

                                Renderer renderer = gameObject.GetComponentInChildren<Renderer>();
                                if (renderer == null) {
                                    Debug.LogWarning("Marked for collider, but has no renderer!" + gameObject.name, gameObject);
                                } else {
                                    boxCollider.center = renderer.bounds.center;
                                    boxCollider.size = renderer.bounds.size;
                                }

                                collider = boxCollider;
                            }

                            collider.isTrigger = false;

                            staticFlags |= StaticEditorFlags.LightmapStatic | StaticEditorFlags.BatchingStatic;
                        }

                        if (!HasPhysics &&
                            ((Flags)baseProperties.Flags & Flags.NoPickup) == Flags.NoPickup &&
                            baseProperties.DrawType != DrawType.Sprite)
                            staticFlags |= StaticEditorFlags.ReflectionProbeStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.LightmapStatic | StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.NavigationStatic;
                        #endregion

                        if (HasPhysics) {
                            Rigidbody rigidbody = gameObject.AddComponent<Rigidbody>();
                            rigidbody.mass = baseProperties.Mass / 10f;

                            if (gameObject.GetComponent<Collider>() == null) { // Rigidbody always needs collider
                                Renderer renderer = gameObject.GetComponentInChildren<Renderer>();

                                if (renderer == null) {
                                    Debug.LogWarning("Rigidbody needs collider, but object has no renderer!" + gameObject.name, gameObject);
                                } else {
                                    SphereCollider sphereCollider = gameObject.AddComponent<SphereCollider>();
                                    sphereCollider.center = gameObject.transform.InverseTransformPoint(renderer.bounds.center);
                                    sphereCollider.radius = Mathf.Min(renderer.bounds.extents.x, renderer.bounds.extents.y);
                                }
                            }
                        }

                        GameObjectUtility.SetStaticEditorFlags(gameObject, staticFlags);
                        foreach(Transform child in gameObject.transform)
                            GameObjectUtility.SetStaticEditorFlags(child.gameObject, staticFlags);

                        EditorUtility.SetDirty(gameObject);
                        GameObject prefabGameObject = PrefabUtility.ReplacePrefab(gameObject, prefabAsset, ReplacePrefabOptions.ConnectToPrefab);

                        prefabLibrary.AddPrefab(combinedId, prefabGameObject);

                        GameObject.DestroyImmediate(gameObject);
                    }
                }
            }

            EditorUtility.SetDirty(prefabLibrary);

            ObjectFactory.GetController().AddLibrary(prefabLibrary);

            AssetDatabase.SaveAssets();
            EditorApplication.SaveAssets();

            AssetDatabase.Refresh();

            Resources.UnloadUnusedAssets();
        }

        private static void AddModel(uint combinedId, BaseProperties baseProperties, GameObject gameObject) {
            string modelPath = AssetDatabase.GUIDToAssetPath(modelLibrary.GetModelGuid(baseProperties.ModelIndex));
            GameObject modelGO = PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath(modelPath, typeof(GameObject))) as GameObject;
            modelGO.transform.SetParent(gameObject.transform, false);
        }

        private static void AddSprite(uint combinedId, BaseProperties baseProperties, GameObject gameObject) {
            uint spriteIndex = objectPropertyLibrary.GetSpriteOffset(combinedId);
            spriteIndex += 1; // World sprite.

            SpriteDefinition sprite = objartLibrary.GetSpriteAnimation(0)[spriteIndex];
            Material material = objartLibrary.GetMaterial();

            MeshFilter meshFilter = gameObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = MeshUtils.CreateTwoSidedPlane(
                sprite.Pivot,
                new Vector2(sprite.Rect.width * material.mainTexture.width / 100f, sprite.Rect.height * material.mainTexture.height / 100f),
                sprite.Rect);
            MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;

            gameObject.AddComponent<Billboard>();
        }

        private static void AddScreen(uint combinedId, BaseProperties baseProperties, GameObject gameObject) {
            MeshProjector meshProjector = gameObject.AddComponent<MeshProjector>();
            meshProjector.Size = new Vector3(
                baseProperties.Size.x,
                baseProperties.Size.y,
                Mathf.Min(baseProperties.Size.x, baseProperties.Size.y));
            MeshRenderer meshRenderer = gameObject.GetComponent<MeshRenderer>();

            DynamicGI.SetEmissive(meshRenderer, Color.white);

            meshRenderer.sharedMaterial = nullMaterial;
        }

        public static void AddDecal(uint combinedId, BaseProperties baseProperties, GameObject gameObject) {
            uint spriteIndex = objectPropertyLibrary.GetSpriteOffset(combinedId);
            Material material = objartLibrary.GetMaterial();

            SpriteDefinition sprite = objartLibrary.GetSpriteAnimation(0)[spriteIndex + 1];

            Vector3 worldSize = baseProperties.GetRenderSize(Vector2.Scale(sprite.Rect.size, material.mainTexture.GetSize()));

            if (((Flags)baseProperties.Flags & Flags.Collider) == Flags.Collider ||
                ((Flags)baseProperties.Flags & Flags.OpaqueClosed) == Flags.OpaqueClosed ||
                ((Flags)baseProperties.Flags & Flags.Activable) == Flags.Activable) {
                MeshFilter meshFilter = gameObject.AddComponent<MeshFilter>();
                meshFilter.sharedMesh = MeshUtils.CreateTwoSidedPlane(sprite.Pivot, worldSize, sprite.Rect);
                meshFilter.sharedMesh.name = sprite.Name;
                MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = material;
            } else {
                MeshProjector meshProjector = gameObject.AddComponent<MeshProjector>();
                meshProjector.Size = worldSize;
                meshProjector.UVRect = sprite.Rect;
                MeshRenderer meshRenderer = gameObject.GetComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = material;
            }
        }

        public static void AddSpecial(uint combinedId, BaseProperties baseProperties, GameObject gameObject) {
            MeshFilter meshFilter = gameObject.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();
        }

        public static void AddForceDoor(uint combinedId, BaseProperties baseProperties, GameObject gameObject) {
            MeshFilter meshFilter = gameObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = MeshUtils.CreateTwoSidedPlane(baseProperties.Size);
            MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();

            // Material is added on instance creation.
        }
    }
}
