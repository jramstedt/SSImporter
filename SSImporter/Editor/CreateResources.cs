using UnityEngine;
using UnityEditor;

using System.IO;
using System.Collections;

using SystemShock.Resource;

namespace SSImporter.Resource {
    public static class CreateResources {
        [MenuItem("Assets/System Shock/1. Create Object Factory")]
        public static void CreateGameController() {
            if (!Directory.Exists(Application.dataPath + @"/SystemShock"))
                AssetDatabase.CreateFolder(@"Assets", @"SystemShock");

            if (!Directory.Exists(Application.dataPath + @"/SystemShock/Resources"))
                AssetDatabase.CreateFolder(@"Assets/SystemShock", @"Resources");

            GameObject prefab = Resources.Load<GameObject>(@"GameController");
            if (prefab == null) {
                prefab = new GameObject(@"GameController");
                prefab.tag = @"GameController";
                prefab.isStatic = true;

                UnityEngine.Object prefabAsset = PrefabUtility.CreateEmptyPrefab(@"Assets/SystemShock/Resources/GameController.prefab");
                PrefabUtility.ReplacePrefab(prefab, prefabAsset, ReplacePrefabOptions.ConnectToPrefab);
            }

            ObjectFactory objectFactory = prefab.GetComponent<ObjectFactory>() ?? prefab.AddComponent<ObjectFactory>();
            objectFactory.Reset();

            PrefabUtility.ReplacePrefab(prefab, PrefabUtility.GetPrefabParent(prefab), ReplacePrefabOptions.ConnectToPrefab);

            AssetDatabase.SaveAssets();
            EditorApplication.SaveAssets();

            AssetDatabase.Refresh();

            Resources.UnloadUnusedAssets();

            GameObject.DestroyImmediate(prefab);
        }
    }
}