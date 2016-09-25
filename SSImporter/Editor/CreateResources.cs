using UnityEngine;
using UnityEditor;

using System.IO;

using SystemShock;
using SystemShock.Resource;
using SystemShock.Gameplay;

namespace SSImporter.Resource {
    public static class CreateResources {
        [MenuItem("Assets/System Shock/1. Create Object Factory", false, 1001)]
        public static void CreateGameController() {
            GameObject gameControllerPrefab = null;

            try {
                AssetDatabase.StartAssetEditing();

                CreateLayers();

                if (!Directory.Exists(Application.dataPath + @"/SystemShock"))
                    AssetDatabase.CreateFolder(@"Assets", @"SystemShock");

                if (!Directory.Exists(Application.dataPath + @"/SystemShock/Resources"))
                    AssetDatabase.CreateFolder(@"Assets/SystemShock", @"Resources");

                gameControllerPrefab = Resources.Load<GameObject>(@"GameController");
                if (gameControllerPrefab == null) {
                    gameControllerPrefab = new GameObject(@"GameController");
                    gameControllerPrefab.tag = @"GameController";
                    gameControllerPrefab.isStatic = true;

                    UnityEngine.Object prefabAsset = PrefabUtility.CreateEmptyPrefab(@"Assets/SystemShock/Resources/GameController.prefab");
                    PrefabUtility.ReplacePrefab(gameControllerPrefab, prefabAsset, ReplacePrefabOptions.ConnectToPrefab);
                }

                MessageBus messageBus = gameControllerPrefab.GetComponent<MessageBus>() ?? gameControllerPrefab.AddComponent<MessageBus>();

                ResourceLibrary resourceLibrary = gameControllerPrefab.GetComponent<ResourceLibrary>() ?? gameControllerPrefab.AddComponent<ResourceLibrary>();

                ObjectFactory objectFactory = gameControllerPrefab.GetComponent<ObjectFactory>() ?? gameControllerPrefab.AddComponent<ObjectFactory>();

                GameVariables gameVariables = gameControllerPrefab.GetComponent<GameVariables>() ?? gameControllerPrefab.AddComponent<GameVariables>();
                gameVariables.Clear();

                PrefabUtility.ReplacePrefab(gameControllerPrefab, PrefabUtility.GetPrefabParent(gameControllerPrefab), ReplacePrefabOptions.ConnectToPrefab);
            } finally {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
            }

            AssetDatabase.Refresh();

            if (gameControllerPrefab != null)
                GameObject.DestroyImmediate(gameControllerPrefab);
        }

        [MenuItem("Assets/System Shock/1. Create Object Factory", true)]
        public static bool Validate() {
            return PlayerPrefs.HasKey(@"SSHOCKRES");
        }

        private static void CreateLayers() {
            string[] layerNames = { @"Level Geometry", @"Items" };

            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            //SerializedProperty tagsProp = tagManager.FindProperty("tags");
            SerializedProperty layersProp = tagManager.FindProperty("layers");

            foreach (string layerName in layerNames) {
                if (LayerMask.NameToLayer(layerName) != -1)
                    continue;

                for (int layerIndex = 8; layerIndex < layersProp.arraySize; ++layerIndex) {
                    SerializedProperty sp = layersProp.GetArrayElementAtIndex(layerIndex);

                    if (string.IsNullOrEmpty(sp.stringValue)) {
                        sp.stringValue = layerName;
                        break;
                    }
                }
            }

            tagManager.ApplyModifiedProperties();
        }

        [MenuItem("Assets/System Shock/0. Set RES path", false, 1000)]
        public static void ClearResourcePath() {
            PlayerPrefs.DeleteKey(@"SSHOCKRES");

            string resPath = EditorUtility.OpenFolderPanel(@"System Shock folder with DATA folder", string.Empty, string.Empty);

            if (string.IsNullOrEmpty(resPath))
                return;

            if(!Directory.Exists(resPath + @"\DATA"))
                throw new DirectoryNotFoundException(@"No DATA folder found at " + resPath);
            
            PlayerPrefs.SetString(@"SSHOCKRES", resPath);
        }
    }
}