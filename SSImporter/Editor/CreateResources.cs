using UnityEngine;
using UnityEditor;

using System.IO;
using System.Collections;

using SystemShock.Resource;

namespace SSImporter.Resource {
    public static class CreateResources {
        [MenuItem("Assets/System Shock/1. Create Object Factory", false, 1001)]
        public static void CreateGameController() {
            GameObject gameControllerPrefab = null;

            try {
                AssetDatabase.StartAssetEditing();

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

                ObjectFactory objectFactory = gameControllerPrefab.GetComponent<ObjectFactory>() ?? gameControllerPrefab.AddComponent<ObjectFactory>();
                objectFactory.Reset();

                PrefabUtility.ReplacePrefab(gameControllerPrefab, PrefabUtility.GetPrefabParent(gameControllerPrefab), ReplacePrefabOptions.ConnectToPrefab);
            } finally {
                AssetDatabase.StopAssetEditing();
                EditorApplication.SaveAssets();
            }

            AssetDatabase.Refresh();

            if (gameControllerPrefab != null)
                GameObject.DestroyImmediate(gameControllerPrefab);
        }

        [MenuItem("Assets/System Shock/1. Create Object Factory", true)]
        public static bool Validate() {
            return PlayerPrefs.HasKey(@"SSHOCKRES");
        }

        [MenuItem("Assets/System Shock/0. Set RES path", false, 1000)]
        public static void ClearResourcePath() {
            PlayerPrefs.DeleteKey(@"SSHOCKRES");

            string resPath = EditorUtility.OpenFolderPanel(@"System Shock folder with DATA", string.Empty, string.Empty);

            if(resPath != string.Empty)
                PlayerPrefs.SetString(@"SSHOCKRES", resPath);
        }
    }
}