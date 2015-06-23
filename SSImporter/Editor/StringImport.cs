using UnityEngine;
using UnityEditor;

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using SystemShock.Object;
using SystemShock.Resource;

namespace SSImporter.Resource {
    public class StringImport {
        [MenuItem("Assets/System Shock/4. Import Strings", false, 1004)]
        public static void Init() {
            CreateStringAssets();
        }

        [MenuItem("Assets/System Shock/4. Import Strings", true)]
        public static bool Validate() {
            return PlayerPrefs.HasKey(@"SSHOCKRES");
        }

        private static void CreateStringAssets() {
            string filePath = PlayerPrefs.GetString(@"SSHOCKRES");

            string stringResourcePath = filePath + @"\DATA\cybstrng.res";

            if (!File.Exists(stringResourcePath))
                return;

            ResourceFile stringResource = new ResourceFile(stringResourcePath);

            try {
                AssetDatabase.StartAssetEditing();

                Dictionary<uint, string[]> stringDictionary = new Dictionary<uint, string[]>();
                foreach (KnownChunkId chunkId in stringResource.GetChunkList())
                    stringDictionary.Add((uint)chunkId, stringResource.ReadStrings(chunkId));

                StringLibrary stringLibrary = ScriptableObject.CreateInstance<StringLibrary>();
                stringLibrary.SetStrings(stringDictionary);

                if (!Directory.Exists(Application.dataPath + @"/SystemShock"))
                    AssetDatabase.CreateFolder(@"Assets", @"SystemShock");

                AssetDatabase.CreateAsset(stringLibrary, @"Assets/SystemShock/cybstrng.res.asset");
                EditorUtility.SetDirty(stringLibrary);

                ObjectFactory.GetController().AddLibrary(stringLibrary);
            } finally {
                AssetDatabase.StopAssetEditing();
                EditorApplication.SaveAssets();
            }

            AssetDatabase.Refresh();
        }
    }
}
