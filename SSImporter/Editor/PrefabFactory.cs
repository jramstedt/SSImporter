using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

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
        [MenuItem("Assets/System Shock/10. Create Object Prefabs", false, 1010)]
        public static void Init() {
            CreateObjectPrefabs();
        }

        [MenuItem("Assets/System Shock/10. Create Object Prefabs", true)]
        public static bool Validate() {
            return PlayerPrefs.HasKey(@"SSHOCKRES");
        }

        private static StringLibrary stringLibrary;
        private static ModelLibrary modelLibrary;
        private static SpriteLibrary spriteLibrary;
        private static ObjectPropertyLibrary objectPropertyLibrary;
        private static Material nullMaterial;

        private static EnemyAnimations[] enemyAnimations;

        private static Material spriteMaterial;

        private static void CreateObjectPrefabs() {
            if (!Directory.Exists(Application.dataPath + @"/SystemShock"))
                AssetDatabase.CreateFolder(@"Assets", @"SystemShock");

            int itemsLayer = LayerMask.NameToLayer(@"Items");

            if (itemsLayer == -1)
                throw new Exception(@"No 'Items' Layer set.");

            try {
                AssetDatabase.StartAssetEditing();

                AssetDatabase.CreateFolder(@"Assets/SystemShock", @"Prefabs");
                AssetDatabase.CreateFolder(@"Assets/SystemShock", @"Animations");

                stringLibrary = StringLibrary.GetLibrary();
                modelLibrary = ModelLibrary.GetLibrary();
                spriteLibrary = SpriteLibrary.GetLibrary();
                objectPropertyLibrary = ObjectPropertyLibrary.GetLibrary();
                nullMaterial = TextureLibrary.GetLibrary().GetResource(KnownChunkId.ModelTexturesStart);

                spriteMaterial = new Material(Shader.Find(@"Sprites/Diffuse"));
                spriteMaterial.name = @"SpriteMaterial";
                AssetDatabase.CreateAsset(spriteMaterial, @"Assets/SystemShock/SpriteMaterial.asset");

                PrefabLibrary prefabLibrary = ScriptableObject.CreateInstance<PrefabLibrary>();
                AssetDatabase.CreateAsset(prefabLibrary, @"Assets/SystemShock/objprefabs.asset");

                ResourceLibrary.GetController().AddLibrary(prefabLibrary);

                CalculateAnimationIndices();

                CyberString objectNames = stringLibrary.GetResource(KnownChunkId.ObjectNames);

                for (byte classIndex = 0; classIndex < ObjectPropertyImport.ObjectDeclarations.Length; ++classIndex) {
                    ObjectDeclaration[] objectDataSubclass = ObjectPropertyImport.ObjectDeclarations[classIndex];

                    for (byte subclassIndex = 0; subclassIndex < objectDataSubclass.Length; ++subclassIndex) {
                        ObjectDeclaration objectDataType = objectDataSubclass[subclassIndex];

                        for (byte typeIndex = 0; typeIndex < objectDataType.Count; ++typeIndex) {
                            uint combinedType = (uint)classIndex << 16 | (uint)subclassIndex << 8 | typeIndex;

                            ObjectData objectData = objectPropertyLibrary.GetResource(combinedType);
                            BaseProperties baseProperties = objectData.Base;

                            string fullName = objectNames[objectData.Index];

                            string fileName = string.Format(@"{0} {1}.prefab", objectData.Index, fullName);
                            fileName = Path.GetInvalidFileNameChars().Aggregate(fileName, (current, c) => current.Replace(c.ToString(), string.Empty));

                            string assetPath = string.Format(@"Assets/SystemShock/Prefabs/{0}", fileName);
                            UnityEngine.Object prefabAsset = PrefabUtility.CreateEmptyPrefab(assetPath);

                            GameObject gameObject = new GameObject(fullName);
                            gameObject.transform.localPosition = Vector3.zero;
                            gameObject.transform.localRotation = Quaternion.identity;
                            gameObject.transform.localScale = Vector3.one;

                            SystemShockObjectProperties properties = gameObject.AddComponent(Type.GetType(objectData.GetType().FullName + @"MonoBehaviour, Assembly-CSharp")) as SystemShockObjectProperties;
                            properties.SetProperties(objectData);

                            if (baseProperties.DrawType == DrawType.Model)
                                AddModel(combinedType, baseProperties, gameObject);
                            else if (baseProperties.DrawType == DrawType.Sprite)
                                AddSprite(combinedType, baseProperties, gameObject, prefabAsset);
                            else if (baseProperties.DrawType == DrawType.Screen)
                                AddScreen(combinedType, baseProperties, gameObject);
                            else if (baseProperties.DrawType == DrawType.Enemy)
                                AddEnemy(combinedType, baseProperties, gameObject, prefabAsset);
                            else if (baseProperties.DrawType == DrawType.T5)
                                Debug.LogWarning("DrawType.T5 not supported", gameObject);
                            else if (baseProperties.DrawType == DrawType.Fragments)
                                Debug.LogWarning("DrawType.Fragments not supported", gameObject);
                            else if (baseProperties.DrawType == DrawType.NoDraw)
                                Debug.Log("DrawType.NoDraw", gameObject);
                            else if (baseProperties.DrawType == DrawType.Decal)
                                AddDecal(combinedType, baseProperties, gameObject);
                            else if (baseProperties.DrawType == DrawType.T9)
                                Debug.LogWarning("DrawType.T9 not supported", gameObject);
                            else if (baseProperties.DrawType == DrawType.T10)
                                Debug.LogWarning("DrawType.T10 not supported", gameObject);
                            else if (baseProperties.DrawType == DrawType.Special)
                                AddSpecial(combinedType, baseProperties, gameObject);
                            else if (baseProperties.DrawType == DrawType.ForceDoor)
                                AddForceDoor(combinedType, baseProperties, gameObject, prefabAsset);

                            StaticEditorFlags staticFlags = 0;

                            if (baseProperties.DrawType == DrawType.Decal || baseProperties.DrawType == DrawType.Screen)
                                staticFlags |= StaticEditorFlags.BatchingStatic | StaticEditorFlags.LightmapStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.NavigationStatic;

                            bool HasPhysics = baseProperties.Rigidbody != 0;

                            #region Flags
                            if (!HasPhysics &&
                                (baseProperties.DrawType == DrawType.Special ||
                                 (baseProperties.DrawType != DrawType.Sprite &&
                                  baseProperties.Vulnerabilities == DamageType.None &&
                                  baseProperties.SpecialVulnerabilities == 0x00 &&
                                  ((Flags)baseProperties.Flags & Flags.NoPickup) == Flags.NoPickup
                                 )
                                ))
                                staticFlags |= StaticEditorFlags.ReflectionProbeStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.LightmapStatic | StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.NavigationStatic;
                            #endregion

                            #region collider
                            {
                                Renderer renderer = gameObject.GetComponentInChildren<Renderer>();
                                if (renderer == null) {
                                    Debug.LogWarning("Marked for collider, but has no renderer! " + gameObject.name, gameObject);
                                } else {
                                    Collider collider = gameObject.GetComponent<Collider>();
                                    if (collider == null) {
                                        MeshFilter meshFilter = gameObject.GetComponentInChildren<MeshFilter>();

                                        if (((Flags)baseProperties.Flags & Flags.CylindericalCollider) == Flags.CylindericalCollider && meshFilter != null) {
                                            MeshCollider meshCollider = gameObject.AddComponent<MeshCollider>(); // TODO should be capsule?
                                            meshCollider.sharedMesh = meshFilter.sharedMesh;

                                            if (HasPhysics)
                                                meshCollider.convex = true;

                                            collider = meshCollider;
                                        } else {
                                            BoxCollider boxCollider = gameObject.AddComponent<BoxCollider>();

                                            boxCollider.center = renderer.bounds.center;
                                            boxCollider.size = renderer.bounds.size;

                                            collider = boxCollider;
                                        }
                                    }

                                    collider.isTrigger = !HasPhysics &&
                                                         ((Flags)baseProperties.Flags & Flags.FlatCollider) == 0 &&
                                                         ((Flags)baseProperties.Flags & Flags.CylindericalCollider) == 0 &&
                                                         (baseProperties.DrawType == DrawType.Screen ||
                                                          baseProperties.DrawType == DrawType.Decal ||
                                                          baseProperties.DrawType == DrawType.Sprite);
                                }
                            }
                            #endregion

                            if (HasPhysics) {
                                Rigidbody rigidbody = gameObject.AddComponent<Rigidbody>();
                                rigidbody.mass = baseProperties.Mass / 10f;

                                if (baseProperties.DrawType == DrawType.Sprite || baseProperties.DrawType == DrawType.Enemy)
                                    rigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
                            }

                            if(((Flags)baseProperties.Flags & Flags.PlayerPushable) == 0)
                                gameObject.layer = itemsLayer;

                            GameObjectUtility.SetStaticEditorFlags(gameObject, staticFlags);
                            foreach(Transform child in gameObject.transform)
                                GameObjectUtility.SetStaticEditorFlags(child.gameObject, staticFlags);

                            PostProcess(properties, (ObjectClass)classIndex, subclassIndex, typeIndex, prefabAsset);

                            {
                                MeshFilter meshFilter = gameObject.GetComponent<MeshFilter>();
                                if (meshFilter && meshFilter.sharedMesh)
                                    AssetDatabase.AddObjectToAsset(meshFilter.sharedMesh, prefabAsset);
                            }

                            EditorUtility.SetDirty(gameObject);
                            GameObject prefabGameObject = PrefabUtility.ReplacePrefab(gameObject, prefabAsset, ReplacePrefabOptions.ConnectToPrefab);

                            prefabLibrary.AddResource(combinedType, prefabGameObject);

                            GameObject.DestroyImmediate(gameObject);
                        }
                    }
                }

                EditorUtility.SetDirty(prefabLibrary);
            } finally {
                AssetDatabase.StopAssetEditing();
                EditorApplication.SaveAssets();
            }

            AssetDatabase.Refresh();
        }

        private static void PostProcess(SystemShockObjectProperties properties, ObjectClass objectClass, byte subclassIndex, byte typeIndex, UnityEngine.Object prefabAsset) {
            GameObject gameObject = properties.gameObject;

            if (objectClass == ObjectClass.Decoration) {
                if (subclassIndex == 2) {
                    if (typeIndex == 3) { // Text
                        GameObject.DestroyImmediate(gameObject.GetComponent<MeshProjector>());
                        gameObject.AddComponent<MeshText>();

                        // TODO create text material. Add to same place as screen material

                        Material material = new Material(Shader.Find(@"Standard"));
                        material.color = Color.white;
                        material.SetFloat(@"_Mode", 1f); // Cutout
                        material.SetFloat(@"_Cutoff", 0.25f);
                        material.SetColor(@"_EmissionColor", Color.white);
                        material.SetFloat(@"_Glossiness", 0f);

                        material.SetOverrideTag("RenderType", "TransparentCutout");
                        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                        material.SetInt("_ZWrite", 1);
                        material.EnableKeyword("_ALPHATEST_ON");
                        material.DisableKeyword("_ALPHABLEND_ON");
                        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                        material.renderQueue = 2450;

                        material.EnableKeyword(@"_EMISSION");

                        AssetDatabase.AddObjectToAsset(material, prefabAsset); // TODO remove when in global materials

                        gameObject.GetComponent<MeshRenderer>().sharedMaterial = material;
                    }
                } else if (subclassIndex == 3) {
                    Light light = gameObject.AddComponent<Light>();
                    light.type = LightType.Point;
                    light.range = 4f;
                    light.shadows = LightShadows.Soft;
                } else if (subclassIndex == 7) { // Bridges, catwalks etc.
                    if (properties.Base.DrawType == DrawType.Special) {
                        Material[] materials = new Material[2];

                        if (typeIndex == 7 || typeIndex == 9) { // forcebridge
                            // TODO create force material. Add to same place as screen material

                            Material colorMaterial = new Material(Shader.Find(@"Standard"));
                            colorMaterial.color = Color.magenta;
                            colorMaterial.SetFloat(@"_Mode", 2f); // Fade
                            colorMaterial.SetColor(@"_EmissionColor", Color.magenta);
                            colorMaterial.SetFloat(@"_Glossiness", 0f);

                            colorMaterial.SetOverrideTag("RenderType", "Transparent");
                            colorMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                            colorMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                            colorMaterial.SetInt("_ZWrite", 0);
                            colorMaterial.DisableKeyword("_ALPHATEST_ON");
                            colorMaterial.EnableKeyword("_ALPHABLEND_ON");
                            colorMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                            colorMaterial.renderQueue = 3000;

                            colorMaterial.EnableKeyword(@"_EMISSION");

                            AssetDatabase.AddObjectToAsset(colorMaterial, prefabAsset); // TODO remove when in global materials

                            materials[0] = materials[1] = colorMaterial;
                        }

                        gameObject.GetComponent<MeshFilter>().sharedMesh = MeshUtils.CreateCubeTopPivot(1, 1, 1f / 32f);
                        gameObject.GetComponent<MeshRenderer>().sharedMaterials = materials;
                    }
                }
            } else if (objectClass == ObjectClass.DoorAndGrating) {
                MeshFilter meshFilter = gameObject.GetComponent<MeshFilter>();
                MeshRenderer meshRenderer = gameObject.GetComponent<MeshRenderer>();

                if (properties.Base.DrawType == DrawType.Decal) {
                    SpriteAnimation spriteAnimation = spriteLibrary.GetResource((KnownChunkId)(KnownChunkId.DoorsStart + properties.ClassIndex));

                    SpriteDefinition sprite = spriteAnimation[0];
                    Material material = spriteLibrary.Material;

                    meshRenderer.sharedMaterial = material;

                    if (spriteAnimation.Sprites.Length > 1) {
                        meshFilter.sharedMesh = MeshUtils.CreateTwoSidedPlane(sprite.PivotNormalized, Vector2.one);
                        meshFilter.sharedMesh.name = sprite.Name;
                    } else {
                        Vector3 worldSize = properties.Base.GetRenderSize(Vector2.Scale(sprite.UVRect.size, material.mainTexture.GetSize()));

                        meshFilter.sharedMesh = MeshUtils.CreateTwoSidedPlane(sprite.PivotNormalized, worldSize, sprite.UVRect);
                        meshFilter.sharedMesh.name = sprite.Name;
                    }
                }
                
                if(meshFilter) {
                    BoxCollider boxCollider = gameObject.GetComponent<BoxCollider>();
                    boxCollider.center = meshFilter.sharedMesh.bounds.center;
                    boxCollider.size = meshFilter.sharedMesh.bounds.size;
                }
            } else if(objectClass == ObjectClass.Trigger) {
                gameObject.layer = LayerMask.NameToLayer(@"Ignore Raycast");
            }
        }
        private static void CalculateAnimationIndices() {
            List<EnemyAnimations> enemyAnimations = new List<EnemyAnimations>();

            EnemyAnimations.Frames
                idle = new EnemyAnimations.Frames { ChunkIdStart = KnownChunkId.EnemyIdleStart, Directional = true },
                walk = new EnemyAnimations.Frames { ChunkIdStart = KnownChunkId.EnemyWalkStart, Directional = true },
                evade = new EnemyAnimations.Frames { ChunkIdStart = KnownChunkId.EnemyEvadeStart, Directional = false },
                damage = new EnemyAnimations.Frames { ChunkIdStart = KnownChunkId.EnemyLightDamageStart, Directional = false },
                criticalDamage = new EnemyAnimations.Frames { ChunkIdStart = KnownChunkId.EnemySevereDamageStart, Directional = false },
                death = new EnemyAnimations.Frames { ChunkIdStart = KnownChunkId.EnemyDeathStart, Directional = false },
                primaryAttack = new EnemyAnimations.Frames { ChunkIdStart = KnownChunkId.EnemyAttackStart, Directional = false },
                secondaryAttack = new EnemyAnimations.Frames { ChunkIdStart = KnownChunkId.EnemyAttackSecondaryStart, Directional = false };

            ObjectClass classIndex = ObjectClass.Enemy;
            ObjectDeclaration[] objectDataSubclass = ObjectPropertyImport.ObjectDeclarations[(uint)classIndex];

            for (byte subclassIndex = 0; subclassIndex < objectDataSubclass.Length; ++subclassIndex) {
                ObjectDeclaration objectDataType = objectDataSubclass[subclassIndex];

                for (byte typeIndex = 0; typeIndex < objectDataType.Count; ++typeIndex) {
                    uint combinedType = (uint)classIndex << 16 | (uint)subclassIndex << 8 | typeIndex;

                    ObjectData objectData = objectPropertyLibrary.GetResource(combinedType);
                    BaseProperties baseProperties = objectData.Base;

                    enemyAnimations.Add(new EnemyAnimations {
                        Idle = idle,
                        Walk = walk,
                        Evade = evade,
                        Damage = damage,
                        CriticalDamage = criticalDamage,
                        Death = death,
                        PrimaryAttack = primaryAttack,
                        SecondaryAttack = secondaryAttack
                    });

                    if (baseProperties.DrawType == DrawType.Enemy) {
                        bool isStub = subclassIndex == 1 && typeIndex == 4; // HACK
                        idle.ChunkIdStart += (ushort)(isStub ? 1 : 8);
                        walk.ChunkIdStart += (ushort)(isStub ? 1 : 8);
                    }

                    ++evade.ChunkIdStart;
                    ++damage.ChunkIdStart;
                    ++criticalDamage.ChunkIdStart;
                    ++death.ChunkIdStart;
                    ++primaryAttack.ChunkIdStart;
                    ++secondaryAttack.ChunkIdStart;
                }
            }

            PrefabFactory.enemyAnimations = enemyAnimations.ToArray();
        }

        private static void AddModel(uint combinedType, BaseProperties baseProperties, GameObject gameObject) {
            GameObject modelGO = PrefabUtility.InstantiatePrefab(modelLibrary.GetResource(KnownChunkId.ModelsStart + baseProperties.ModelIndex)) as GameObject;
            modelGO.transform.SetParent(gameObject.transform, false);
        }

        private static void AddSprite(uint combinedType, BaseProperties baseProperties, GameObject gameObject, UnityEngine.Object prefabAsset) {
            uint spriteIndex = objectPropertyLibrary.GetSpriteOffset(combinedType);
            spriteIndex += 1; // World sprite.

            SpriteDefinition sprite = spriteLibrary.GetResource(KnownChunkId.ObjectSprites)[spriteIndex];
            Material material = spriteLibrary.Material;

            GameObject visualization = new GameObject(sprite.Name);
            visualization.transform.SetParent(gameObject.transform, false);

            MeshFilter meshFilter = visualization.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = MeshUtils.CreateTwoSidedPlane(
                sprite.PivotNormalized,
                new Vector2(sprite.UVRect.width * material.mainTexture.width / 100f, sprite.UVRect.height * material.mainTexture.height / 100f),
                sprite.UVRect);
            MeshRenderer meshRenderer = visualization.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;

            visualization.AddComponent<Billboard>();

            AssetDatabase.AddObjectToAsset(meshFilter.sharedMesh, prefabAsset);
        }

        private static void AddScreen(uint combinedType, BaseProperties baseProperties, GameObject gameObject) {
            MeshProjector meshProjector = gameObject.AddComponent<MeshProjector>();
            meshProjector.Size = new Vector3(
                baseProperties.Size.x,
                baseProperties.Size.y,
                Mathf.Min(baseProperties.Size.x, baseProperties.Size.y));
            MeshRenderer meshRenderer = gameObject.GetComponent<MeshRenderer>();

            DynamicGI.SetEmissive(meshRenderer, Color.white);

            meshRenderer.sharedMaterial = nullMaterial;
        }

        private static void AddEnemy(uint combinedType, BaseProperties baseProperties, GameObject gameObject, UnityEngine.Object prefabAsset) {
            GameObject visualization = new GameObject("Visualization");
            visualization.transform.SetParent(gameObject.transform, false);

            SpriteRenderer spriteRenderer = visualization.AddComponent<SpriteRenderer>();
            spriteRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.UseProxyVolume;
            spriteRenderer.receiveShadows = true;
            spriteRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.BlendProbes;
            spriteRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
            spriteRenderer.sharedMaterial = spriteMaterial;

            LightProbeProxyVolume lightProxyVolune = visualization.AddComponent<LightProbeProxyVolume>();
            lightProxyVolune.probePositionMode = LightProbeProxyVolume.ProbePositionMode.CellCenter;
            lightProxyVolune.resolutionMode = LightProbeProxyVolume.ResolutionMode.Custom;
            lightProxyVolune.gridResolutionX = 2;
            lightProxyVolune.gridResolutionY = 2;
            lightProxyVolune.gridResolutionZ = 1;

            int enemyIndex =    objectPropertyLibrary.GetIndex(combinedType) -
                                objectPropertyLibrary.GetIndex(ObjectClass.Enemy, 0, 0);

            Sprite snapshot;

            Animator animator = visualization.AddComponent<Animator>();
            animator.updateMode = AnimatorUpdateMode.AnimatePhysics;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            AnimatorController.SetAnimatorController(animator, CreateEnemyAnimatorController(gameObject, (ushort)enemyIndex, prefabAsset, out snapshot));

            spriteRenderer.sprite = snapshot;

            gameObject.AddComponent<SystemShock.Enemy>();
        }

        private static void AddDecal(uint combinedType, BaseProperties baseProperties, GameObject gameObject) {
            uint spriteIndex = objectPropertyLibrary.GetSpriteOffset(combinedType);
            Material material = spriteLibrary.Material;

            SpriteDefinition sprite = spriteLibrary.GetResource(KnownChunkId.ObjectSprites)[spriteIndex + 1];

            Vector3 worldSize = baseProperties.GetRenderSize(Vector2.Scale(sprite.UVRect.size, material.mainTexture.GetSize()));

            if (((Flags)baseProperties.Flags & Flags.FlatCollider) == Flags.FlatCollider ||
                ((Flags)baseProperties.Flags & Flags.CylindericalCollider) == Flags.CylindericalCollider) {
                MeshFilter meshFilter = gameObject.AddComponent<MeshFilter>();
                meshFilter.sharedMesh = MeshUtils.CreateTwoSidedPlane(sprite.PivotNormalized, worldSize, sprite.UVRect);
                meshFilter.sharedMesh.name = sprite.Name;

                MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = material;
            } else {
                MeshProjector meshProjector = gameObject.AddComponent<MeshProjector>();
                meshProjector.Size = worldSize;
                meshProjector.UVRect = sprite.UVRect;

                MeshRenderer meshRenderer = gameObject.GetComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = material;
            }
        }

        private static void AddSpecial(uint combinedType, BaseProperties baseProperties, GameObject gameObject) {
            MeshFilter meshFilter = gameObject.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();
        }

        private static void AddForceDoor(uint combinedType, BaseProperties baseProperties, GameObject gameObject, UnityEngine.Object prefabAsset) {
            MeshFilter meshFilter = gameObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = MeshUtils.CreateTwoSidedPlane(baseProperties.Size);
            MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();

            // TODO create force material. Add to same place as screen material

            Material colorMaterial = new Material(Shader.Find(@"Standard"));
            colorMaterial.color = Color.magenta;
            colorMaterial.SetFloat(@"_Mode", 2f); // Fade
            colorMaterial.SetColor(@"_EmissionColor", Color.magenta);
            colorMaterial.SetFloat(@"_Glossiness", 0f);

            colorMaterial.SetOverrideTag("RenderType", "Transparent");
            colorMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            colorMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            colorMaterial.SetInt("_ZWrite", 0);
            colorMaterial.DisableKeyword("_ALPHATEST_ON");
            colorMaterial.EnableKeyword("_ALPHABLEND_ON");
            colorMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            colorMaterial.renderQueue = 3000;

            colorMaterial.EnableKeyword(@"_EMISSION");

            AssetDatabase.AddObjectToAsset(colorMaterial, prefabAsset); // TODO remove when in global materials

            meshRenderer.sharedMaterial = colorMaterial;
        }

        private const string AttackPrimaryParameter = @"AttackPrimary";
        private const string AttackSecondaryParameter = @"AttackSecondary";
        private const string EvadingParameter = @"Evading";
        private const string DeadParameter = @"Dead";
        private const string DirectionParameter = @"Direction";
        private const string SpeedParameter = @"Speed";
        private const string DamageParameter = @"Damage";
        private const string CriticalDamageParameter = @"CriticalDamage";

        private static AnimatorController CreateEnemyAnimatorController(GameObject gameObject, int enemyIndex, UnityEngine.Object prefabAsset, out Sprite snapshot) {
            string fileName = string.Format(@"{0} {1}.controller", enemyIndex, gameObject.name);
            fileName = Path.GetInvalidFileNameChars().Aggregate(fileName, (current, c) => current.Replace(c.ToString(), string.Empty));

            string assetPath = string.Format(@"Assets/SystemShock/Animations/{0}", fileName);

            AnimatorController animatorController = AnimatorController.CreateAnimatorControllerAtPath(assetPath);
            AnimatorStateMachine rootStateMachine = animatorController.layers[0].stateMachine;

            #region Add parameters
            animatorController.AddParameter(AttackPrimaryParameter, AnimatorControllerParameterType.Trigger);
            animatorController.AddParameter(AttackSecondaryParameter, AnimatorControllerParameterType.Trigger);
            animatorController.AddParameter(EvadingParameter, AnimatorControllerParameterType.Trigger);
            animatorController.AddParameter(DeadParameter, AnimatorControllerParameterType.Bool);
            animatorController.AddParameter(DirectionParameter, AnimatorControllerParameterType.Float); // Degrees, Offset from looking at player
            animatorController.AddParameter(SpeedParameter, AnimatorControllerParameterType.Float);
            animatorController.AddParameter(DamageParameter, AnimatorControllerParameterType.Trigger);
            animatorController.AddParameter(CriticalDamageParameter, AnimatorControllerParameterType.Trigger);
            #endregion

            #region Add states
            AnimatorState primaryAttackState = rootStateMachine.AddState(@"PrimaryAttack");
            AnimatorState secondaryAttackState = rootStateMachine.AddState(@"SecondaryAttack");
            AnimatorState evadeState = rootStateMachine.AddState(@"Evade");
            AnimatorState deathState = rootStateMachine.AddState(@"Death");
            AnimatorState damageState = rootStateMachine.AddState(@"Damage");
            AnimatorState criticalDamageState = rootStateMachine.AddState(@"CriticalDamage");
            #endregion

            #region Add Motions
            EnemyAnimations enemyAnimations = PrefabFactory.enemyAnimations[enemyIndex];

            AnimatorState motionState = CreateMotionState(enemyAnimations, animatorController, prefabAsset);
            primaryAttackState.motion = CreateAnimationClip(@"Primary Attack", enemyAnimations.PrimaryAttack, prefabAsset);
            secondaryAttackState.motion = CreateAnimationClip(@"Secondary Attack", enemyAnimations.SecondaryAttack, prefabAsset);
            evadeState.motion = CreateAnimationClip(@"Evade", enemyAnimations.Evade, prefabAsset);
            deathState.motion = CreateAnimationClip(@"Death", enemyAnimations.Death, prefabAsset);
            damageState.motion = CreateAnimationClip(@"Damage", enemyAnimations.Damage, prefabAsset);
            criticalDamageState.motion = CreateAnimationClip(@"Critical Damage", enemyAnimations.CriticalDamage, prefabAsset);

            rootStateMachine.defaultState = motionState;
            #endregion

            #region Add transitions
            #region Evade
            AnimatorStateTransition idleToEvadeTransition = motionState.AddTransition(evadeState);
            idleToEvadeTransition.AddCondition(AnimatorConditionMode.If, 0f, EvadingParameter);

            evadeState.AddTransition(motionState).hasExitTime = true;
            #endregion

            #region Attack
            AnimatorStateTransition anyToPrimaryAttack = rootStateMachine.AddAnyStateTransition(primaryAttackState);
            anyToPrimaryAttack.interruptionSource = TransitionInterruptionSource.Source;
            anyToPrimaryAttack.AddCondition(AnimatorConditionMode.If, 0f, AttackPrimaryParameter);

            primaryAttackState.AddTransition(motionState).hasExitTime = true;

            AnimatorStateTransition anyToSecondaryAttack = rootStateMachine.AddAnyStateTransition(secondaryAttackState);
            anyToSecondaryAttack.interruptionSource = TransitionInterruptionSource.Source;
            anyToSecondaryAttack.AddCondition(AnimatorConditionMode.If, 0f, AttackSecondaryParameter);

            secondaryAttackState.AddTransition(motionState).hasExitTime = true;
            #endregion

            #region Death
            AnimatorStateTransition anyToDeath = rootStateMachine.AddAnyStateTransition(deathState);
            anyToDeath.interruptionSource = TransitionInterruptionSource.None;
            anyToDeath.canTransitionToSelf = false;
            anyToDeath.AddCondition(AnimatorConditionMode.If, 0f, DeadParameter);
            #endregion

            #region Damage
            AnimatorStateTransition anyToDamage = rootStateMachine.AddAnyStateTransition(damageState);
            anyToDamage.interruptionSource = TransitionInterruptionSource.None;
            anyToDamage.AddCondition(AnimatorConditionMode.If, 0f, DamageParameter);
            damageState.AddTransition(motionState).hasExitTime = true;

            AnimatorStateTransition anyToCriticalDamage = rootStateMachine.AddAnyStateTransition(criticalDamageState);
            anyToCriticalDamage.interruptionSource = TransitionInterruptionSource.None;
            anyToCriticalDamage.AddCondition(AnimatorConditionMode.If, 0f, CriticalDamageParameter);
            criticalDamageState.AddTransition(motionState).hasExitTime = true;
            #endregion

            #endregion
            
            snapshot = AnimationUtility.GetObjectReferenceCurve(evadeState.motion as AnimationClip, AnimationUtility.GetObjectReferenceCurveBindings(evadeState.motion as AnimationClip)[0])[0].value as Sprite;

            return animatorController;
        }

        private static AnimationClip CreateAnimationClip(string name, SpriteAnimation spriteAnimation, UnityEngine.Object prefabAsset, bool loop = false, float framesPerSecond = 10f) {
            Material material = spriteLibrary.Material;

            ObjectReferenceKeyframe[] keyFrames = new ObjectReferenceKeyframe[spriteAnimation.Count()];
            for (uint k = 0; k < keyFrames.Length; ++k) {
                SpriteDefinition spriteDefinition = spriteAnimation[k];

                Rect spriteRect = new Rect( spriteDefinition.UVRect.x * material.mainTexture.width,
                                            spriteDefinition.UVRect.y * material.mainTexture.height,
                                            spriteDefinition.UVRect.width * material.mainTexture.width,
                                            spriteDefinition.UVRect.height * material.mainTexture.height);

                Sprite sprite = Sprite.Create(material.mainTexture as Texture2D, spriteRect, spriteDefinition.PivotNormalized, 100f);

                AssetDatabase.AddObjectToAsset(sprite, prefabAsset);

                keyFrames[k] = new ObjectReferenceKeyframe() {
                    time = k / framesPerSecond,
                    value = sprite
                };
            }

            AnimationClip clip = new AnimationClip();
            clip.name = name;
            clip.frameRate = framesPerSecond;

            if (loop) {
                AnimationClipSettings clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
                clipSettings.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(clip, clipSettings);
                EditorUtility.SetDirty(clip);
            }

            AnimationUtility.SetObjectReferenceCurve(clip, new EditorCurveBinding() {
                type = typeof(SpriteRenderer),
                propertyName = "m_Sprite"
            }, keyFrames);

            AssetDatabase.AddObjectToAsset(clip, prefabAsset);

            return clip;
        }

        private static AnimationClip CreateAnimationClip(string name, EnemyAnimations.Frames animationFrames, UnityEngine.Object prefabAsset, bool loop = false, float framesPerSecond = 5f) {
            SpriteAnimation spriteAnimation = spriteLibrary.GetResource(animationFrames.ChunkIdStart);
            AnimationClip clip = CreateAnimationClip(name, spriteAnimation, prefabAsset, loop, framesPerSecond);
            
            return clip;
        }

        private static AnimatorState CreateMotionState(EnemyAnimations animations, AnimatorController animatorController, UnityEngine.Object prefabAsset) {
            BlendTree blendTree;
            AnimatorState state = animatorController.CreateBlendTreeInController("Motion", out blendTree);
            blendTree.blendType = BlendTreeType.FreeformCartesian2D;
            blendTree.blendParameter = DirectionParameter;
            blendTree.blendParameterY = SpeedParameter;

            for (uint i = 0; i < 8; ++i) {
                ushort direction = (ushort)((i + 6) & 7); // We want 0 to be looking at player.

                EnemyAnimations.Frames idleFrames = animations.Idle;
                SpriteAnimation idleSpriteAnimation = spriteLibrary.GetResource(idleFrames.ChunkIdStart + direction);
                AnimationClip idleClip = CreateAnimationClip(@"Idle " + i, idleSpriteAnimation, prefabAsset, true, 3f);

                EnemyAnimations.Frames walkFrames = animations.Walk;
                SpriteAnimation walkSpriteAnimation = spriteLibrary.GetResource(walkFrames.ChunkIdStart + direction);
                AnimationClip walkClip = CreateAnimationClip(@"Walk " + i, walkSpriteAnimation, prefabAsset, true, 3f);

                blendTree.AddChild(idleClip, new Vector2(i / 8f, 0f));
                blendTree.AddChild(walkClip, new Vector2(i / 8f, 1f));

                if (i == 0) { // Wrap
                    blendTree.AddChild(idleClip, new Vector2(1f, 0f));
                    blendTree.AddChild(walkClip, new Vector2(1f, 1f));
                }
            }

            return state;
        }

        private struct EnemyAnimations {
            public struct Frames {
                public KnownChunkId ChunkIdStart;
                public bool Directional;
            }

            public Frames Idle;
            public Frames Walk;
            public Frames Evade;
            public Frames Damage;
            public Frames CriticalDamage;
            public Frames Death;
            public Frames PrimaryAttack;
            public Frames SecondaryAttack;
        }
    }
}
