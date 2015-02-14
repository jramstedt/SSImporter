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
        [MenuItem("Assets/System Shock/Import Strings")]
        public static void Init() {
            CreateStringAssets();
        }

        private static void CreateStringAssets() {
            string filePath = @"D:\Users\Janne\Downloads\SYSTEMSHOCK-Portable-v1.2.3\RES";
            string stringResourcePath = filePath + @"\DATA\cybstrng.res";

            if (!File.Exists(stringResourcePath))
                return;

            ResourceFile stringResource = new ResourceFile(stringResourcePath);

            Dictionary<uint, string[]> stringDictionary = new Dictionary<uint, string[]>();
            foreach (KnownChunkId chunkId in stringResource.GetChunkList())
                stringDictionary.Add((uint)chunkId, stringResource.ReadStrings(chunkId));

            StringLibrary stringLibrary = ScriptableObject.CreateInstance<StringLibrary>();
            stringLibrary.SetStrings(stringDictionary);

            if (!Directory.Exists(Application.dataPath + @"/SystemShock"))
                AssetDatabase.CreateFolder(@"Assets", @"SystemShock");

            AssetDatabase.CreateAsset(stringLibrary, @"Assets/SystemShock/cybstrng.res.asset");
            EditorUtility.SetDirty(stringLibrary);

            AssetDatabase.SaveAssets();
            EditorApplication.SaveAssets();

            AssetDatabase.Refresh();

            Resources.UnloadUnusedAssets();
        }
    }
}
