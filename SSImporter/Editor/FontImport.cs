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
                AssetDatabase.CreateFolder(@"Assets/SystemShock/gamescr.res", @"Materials");

                Dictionary<uint, Font> fonts = new Dictionary<uint, Font>();

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

                        assetPath = string.Format(@"Assets/SystemShock/gamescr.res/Materials/{0} material.asset", chunkId);
                        AssetDatabase.CreateAsset(fontSet.Texture, assetPath);
                        AssetDatabase.AddObjectToAsset(material, assetPath);

                        //AssetDatabase.AddObjectToAsset(fontSet.Texture, assetPath);
                        //AssetDatabase.AddObjectToAsset(material, assetPath);

                        EditorUtility.SetDirty(fontSet.Font);
                        EditorUtility.SetDirty(fontSet.Texture);
                        EditorUtility.SetDirty(material);

                        fonts.Add((uint)chunkId, fontSet.Font);
                    }
                    //Debug.Log(chunkId + " " + chunkInfo.info);
                }

                FontLibrary fontLibrary = ScriptableObject.CreateInstance<FontLibrary>();
                fontLibrary.SetFonts(fonts);

                AssetDatabase.CreateAsset(fontLibrary, @"Assets/SystemShock/gamescr.res.asset");
                EditorUtility.SetDirty(fontLibrary);

                ObjectFactory.GetController().AddLibrary(fontLibrary);
            } finally {
                AssetDatabase.StopAssetEditing();
                EditorApplication.SaveAssets();
            }

            AssetDatabase.Refresh();
        }
    }
}