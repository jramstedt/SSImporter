using UnityEngine;
using UnityEditor;

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using SystemShock.Resource;

namespace SSImporter.Resource {
    public class GraphicsImport : ScriptableObject {
        [MenuItem("Assets/System Shock/Import UI Graphics", false, 1100)]
        public static void Init() {
            CreateAssets();
        }

        [MenuItem("Assets/System Shock/Import UI Graphics", true)]
        public static bool ValidateCreateGameController() {
            return PlayerPrefs.HasKey(@"SSHOCKRES");
        }

        private static void CreateAssets() {
            string filePath = PlayerPrefs.GetString(@"SSHOCKRES");

            string gamePalettePath = filePath + @"\DATA\gamepal.res";

            string gamescrPath = filePath + @"\DATA\gamescr.res";
            string handartPath = filePath + @"\DATA\handart.res";
            string mfdartPath = filePath + @"\DATA\mfdart.res";
            string sideartPath = filePath + @"\DATA\sideart.res";

            if (!File.Exists(gamePalettePath) ||
                !File.Exists(gamescrPath) ||
                !File.Exists(handartPath) ||
                !File.Exists(mfdartPath) ||
                !File.Exists(sideartPath))
                return;

            try {
                //AssetDatabase.StartAssetEditing();

                if (!Directory.Exists(Application.dataPath + @"/SystemShock"))
                    AssetDatabase.CreateFolder(@"Assets", @"SystemShock");

                #region Texture palette
                ResourceFile paletteResource = new ResourceFile(gamePalettePath);
                PaletteChunk gamePalette = paletteResource.ReadPalette(KnownChunkId.Palette);
                #endregion

                #region Create game screen sprites
                {
                    ResourceFile spritesResource = new ResourceFile(gamescrPath);
                    CreateSpriteLibrary(spritesResource, @"gamescr.res.png", gamePalette);
                }
                #endregion

                #region Create hand art sprites
                {
                    ResourceFile spritesResource = new ResourceFile(handartPath);
                    CreateSpriteLibrary(spritesResource, @"handart.res.png", gamePalette);
                }
                #endregion

                #region Create multi functional display sprites
                {
                    ResourceFile spritesResource = new ResourceFile(mfdartPath);
                    CreateSpriteLibrary(spritesResource, @"mfdart.res.png", gamePalette);
                }
                #endregion

                #region Create side art sprites
                {
                    ResourceFile spritesResource = new ResourceFile(sideartPath);

                    CreateSpriteLibrary(spritesResource, @"sideart.res.png", gamePalette);
                }
                #endregion
            } finally {
                //AssetDatabase.StopAssetEditing();
                EditorApplication.SaveAssets();
            }

            AssetDatabase.Refresh();
        }

        private static void CreateSpriteLibrary(ResourceFile spritesResource, string libraryAssetPath, PaletteChunk gamePalette) {
            ICollection<KnownChunkId> spriteChunkIds = spritesResource.GetChunkList();

            List<Texture2D> allSpritesDiffuse = new List<Texture2D>();
            List<TextureSet> allSprites = new List<TextureSet>();
            List<int[]> spriteRectIndices = new List<int[]>();

            foreach (KnownChunkId chunkId in spriteChunkIds) {
                if (spritesResource.GetChunkInfo(chunkId).info.ContentType != ContentType.Bitmap)
                    continue;

                TextureSet[] sprites = spritesResource.ReadBitmaps(chunkId, gamePalette);

                int[] indices = new int[sprites.Length];
                for (int i = 0; i < sprites.Length; ++i)
                    indices[i] = allSpritesDiffuse.Count + i; // Calculates index to rect array from PackTextures

                spriteRectIndices.Add(indices);

                foreach (TextureSet textureSet in sprites) {
                    allSpritesDiffuse.Add(textureSet.Diffuse);
                    allSprites.Add(textureSet);
                }
            }

            Texture2D atlasDiffuse = new Texture2D(1024, 1024, TextureFormat.RGBA32, true, true);
            Rect[] allRects = atlasDiffuse.PackTextures(allSpritesDiffuse.ToArray(), 1, 4096);
            atlasDiffuse.name = Path.GetFileNameWithoutExtension(libraryAssetPath) + @" Diffuse";
            atlasDiffuse.alphaIsTransparency = true;
            atlasDiffuse.Apply(true, false);

            File.WriteAllBytes(Application.dataPath + "/SystemShock/" + libraryAssetPath, atlasDiffuse.EncodeToPNG());

            string assetPath = @"Assets/SystemShock/" + libraryAssetPath;

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

            TextureImporter textureImporter = TextureImporter.GetAtPath(assetPath) as TextureImporter;
            textureImporter.alphaIsTransparency = true;
            textureImporter.compressionQuality = 100;
            textureImporter.linearTexture = true;
            textureImporter.npotScale = TextureImporterNPOTScale.None;
            textureImporter.isReadable = false;
            textureImporter.wrapMode = TextureWrapMode.Clamp;

            SpriteMetaData[] spriteMetaDatas = new SpriteMetaData[allRects.Length];

            int spriteIndex = 0;
            foreach (Rect uvRect in allRects) {
                Rect pixelRect = uvRect;
                pixelRect.x = uvRect.x * atlasDiffuse.width;
                pixelRect.y = uvRect.y * atlasDiffuse.height;
                pixelRect.width = uvRect.width * atlasDiffuse.width;
                pixelRect.height = uvRect.height * atlasDiffuse.height;

                SpriteMetaData spriteMetaData = new SpriteMetaData();
                spriteMetaData.name = (spriteIndex).ToString();
                spriteMetaData.rect = pixelRect;
                spriteMetaData.pivot = new Vector2(0.5f, 0.0f);
                spriteMetaData.alignment = 7;

                spriteMetaDatas[spriteIndex++] = spriteMetaData;
            }

            textureImporter.spritesheet = spriteMetaDatas;
            textureImporter.textureType = TextureImporterType.Sprite;
            textureImporter.spriteImportMode = SpriteImportMode.Multiple;
            textureImporter.spritePixelsPerUnit = 128f;

            textureImporter.SaveAndReimport();
        }
    }
}