using UnityEngine;
using UnityEditor;

using System.IO;

using SystemShock.Resource;

namespace SSImporter.Resource {
    public class FontImport {
        [MenuItem("Assets/System Shock/6. Import Fonts", false, 1006)]
        public static void Init() {
            CreateObjectFontAssets();
        }

        [MenuItem("Assets/System Shock/6. Import Fonts", true)]
        public static bool Validate() {
            return PlayerPrefs.HasKey(@"SSHOCKRES");
        }

        private static void CreateObjectFontAssets() {
            string filePath = PlayerPrefs.GetString(@"SSHOCKRES");

            string gamescrPath = filePath + @"\DATA\gamescr.res";
            string gamePalettePath = filePath + @"\DATA\gamepal.res";

            if (!File.Exists(gamescrPath) ||
                !File.Exists(gamePalettePath))
                return;

            try {
                AssetDatabase.StartAssetEditing();

                if (!Directory.Exists(Application.dataPath + @"/SystemShock"))
                    AssetDatabase.CreateFolder(@"Assets", @"SystemShock");

                #region Texture palette
                ResourceFile paletteResource = new ResourceFile(gamePalettePath);
                PaletteChunk gamePalette = paletteResource.ReadPalette(KnownChunkId.Palette);
                #endregion

                AssetDatabase.CreateFolder(@"Assets/SystemShock", @"gamescr.res");

                FontLibrary fontLibrary = ScriptableObject.CreateInstance<FontLibrary>();

                ResourceFile gamescrResource = new ResourceFile(gamescrPath);
                foreach (KnownChunkId chunkId in gamescrResource.GetChunkList()) {
                    ChunkInfo chunkInfo = gamescrResource.GetChunkInfo(chunkId);

                    if (chunkInfo.info.ContentType == ContentType.Font) {
                        FontSet fontSet = gamescrResource.ReadFont(chunkInfo, gamePalette);

                        Material material = new Material(Shader.Find(@"UI/Default Font"));
                        material.mainTexture = fontSet.Texture;

                        fontSet.Font.material = material;

                        string assetPath = string.Format(@"Assets/SystemShock/gamescr.res/{0}.fontsettings", chunkId);
                        AssetDatabase.CreateAsset(fontSet.Font, assetPath);
                        AssetDatabase.AddObjectToAsset(fontSet.Texture, assetPath);
                        AssetDatabase.AddObjectToAsset(material, assetPath);

                        EditorUtility.SetDirty(fontSet.Font);
                        EditorUtility.SetDirty(fontSet.Texture);
                        EditorUtility.SetDirty(material);

                        fontLibrary.AddResource(chunkId, fontSet.Font);

                        SerializedObject fontAsset = new SerializedObject(AssetDatabase.LoadAssetAtPath<Font>(assetPath));
                        SerializedProperty lineSpacing = fontAsset.FindProperty(@"m_LineSpacing");
                        lineSpacing.floatValue = fontSet.LineHeight;
                        fontAsset.ApplyModifiedProperties();
                    }
                    //Debug.Log(chunkId + " " + chunkInfo.info);
                }

                AssetDatabase.CreateAsset(fontLibrary, @"Assets/SystemShock/gamescr.res.asset");
                EditorUtility.SetDirty(fontLibrary);

                ResourceLibrary.GetController().AddLibrary(fontLibrary);
            } finally {
                AssetDatabase.StopAssetEditing();
                EditorApplication.SaveAssets();
            }

            AssetDatabase.Refresh();
        }
    }
}