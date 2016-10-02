using UnityEngine;
using UnityEditor;

using System.IO;
using System.Collections.Generic;

using SystemShock.Resource;
using System.Collections;

namespace SSImporter.Resource {
    public class GraphicsImport : ScriptableObject {
        [MenuItem("Assets/System Shock/13. Import UI Graphics", false, 1013)]
        public static void Init() {
            Coroutines = new List<IEnumerator>();
            Coroutines.Add(CreateAssets());

            EditorApplication.update += Update;
        }

        [MenuItem("Assets/System Shock/13. Import UI Graphics", true)]
        public static bool ValidateCreateGameController() {
            return PlayerPrefs.HasKey(@"SSHOCKRES");
        }

        public const bool compressTextures = false;

        private static List<IEnumerator> Coroutines;
        private static void Update() {
            if (Coroutines.Count > 0) {
                IEnumerator Coroutine = Coroutines[Coroutines.Count - 1];
                if (Coroutine.MoveNext()) {
                    if (Coroutine.Current is IEnumerator)
                        Coroutines.Add(Coroutine.Current as IEnumerator);
                } else {
                    Coroutines.RemoveAt(Coroutines.Count - 1);
                }
            } else {
                EditorApplication.update -= Update;
            }
        }

        private static IEnumerator CreateAssets() {
            string filePath = PlayerPrefs.GetString(@"SSHOCKRES");

            string gamePalettePath = filePath + @"\DATA\gamepal.res";

            string gamescrPath = filePath + @"\DATA\gamescr.res";
            string handartPath = filePath + @"\DATA\handart.res";
            string mfdartPath = filePath + @"\DATA\mfdart.res";
            //string sideartPath = filePath + @"\DATA\sideart.res";

            if (!File.Exists(gamePalettePath) ||
                !File.Exists(gamescrPath) ||
                !File.Exists(handartPath) ||
                !File.Exists(mfdartPath)/* ||
                !File.Exists(sideartPath)*/)
                yield break;

            try {
                //AssetDatabase.StartAssetEditing();

                if (!Directory.Exists(Application.dataPath + @"/SystemShock"))
                    AssetDatabase.CreateFolder(@"Assets", @"SystemShock");

                #region Texture palette
                ResourceFile paletteResource = new ResourceFile(gamePalettePath);
                PaletteChunk gamePalette = paletteResource.ReadPalette(KnownChunkId.Palette);
                #endregion

                Debug.Log("WAT!?!!");

                yield return CreateSpriteLibrary(@"mfdart.res", gamePalette,
                    new ResourceFile(gamescrPath),
                    new ResourceFile(handartPath),
                    new ResourceFile(mfdartPath)/*,
                    new ResourceFile(sideartPath)*/);

                Debug.Log("PASKA!");
            } finally {
                //AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
            }

            AssetDatabase.Refresh();
        }

        private static IEnumerator CreateSpriteLibrary(string libraryAssetPath, PaletteChunk gamePalette, params ResourceFile[] spritesResources) {
            List<Texture2D> allSpritesDiffuse = new List<Texture2D>();

            Dictionary<KnownChunkId, int[]> spriteRectIndices = new Dictionary<KnownChunkId, int[]>();

            foreach (ResourceFile spritesResource in spritesResources) {
                ICollection<KnownChunkId> spriteChunkIds = spritesResource.GetChunkList();
                foreach (KnownChunkId chunkId in spriteChunkIds) {
                    if (spritesResource.GetChunkInfo(chunkId).info.ContentType != ContentType.Bitmap)
                        continue;

                    TextureSet[] sprites = spritesResource.ReadBitmaps(chunkId, gamePalette);

                    int[] indices = new int[sprites.Length];
                    for (int i = 0; i < sprites.Length; ++i) {
                        TextureSet textureSet = sprites[i];

                        indices[i] = allSpritesDiffuse.Count;
                        allSpritesDiffuse.Add(textureSet.Diffuse);
                    }

                    spriteRectIndices.Add(chunkId, indices);
                }
            }

            Texture2D atlasDiffuse = new Texture2D(1024, 1024, TextureFormat.RGBA32, true, true);
            Rect[] allRects = atlasDiffuse.PackTextures(allSpritesDiffuse.ToArray(), 2, 4096);
            atlasDiffuse.name = Path.GetFileNameWithoutExtension(libraryAssetPath) + @" Diffuse";
            atlasDiffuse.alphaIsTransparency = true;
            atlasDiffuse.Apply(true, false);

            File.WriteAllBytes(Application.dataPath + "/SystemShock/" + libraryAssetPath + @".png", atlasDiffuse.EncodeToPNG());

            string assetPath = @"Assets/SystemShock/" + libraryAssetPath + @".png";

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

            SpriteMetaData[] spriteMetaDatas = new SpriteMetaData[allRects.Length];

            foreach (KeyValuePair<KnownChunkId, int[]> rectIndices in spriteRectIndices) {
                int[] chunkRectIndices = rectIndices.Value;
                for (int i = 0; i < chunkRectIndices.Length; ++i) {
                    Rect uvRect = allRects[chunkRectIndices[i]];

                    Rect pixelRect = uvRect;
                    pixelRect.x = uvRect.x * atlasDiffuse.width;
                    pixelRect.y = uvRect.y * atlasDiffuse.height;
                    pixelRect.width = uvRect.width * atlasDiffuse.width;
                    pixelRect.height = uvRect.height * atlasDiffuse.height;

                    SpriteMetaData spriteMetaData = new SpriteMetaData();
                    spriteMetaData.name = string.Format(@"{0} {1}", rectIndices.Key, i);
                    spriteMetaData.rect = pixelRect;
                    spriteMetaData.pivot = new Vector2(0.5f, 0.0f);
                    spriteMetaData.alignment = 7;

                    spriteMetaDatas[chunkRectIndices[i]] = spriteMetaData;
                }
            }

            TextureImporter textureImporter = TextureImporter.GetAtPath(assetPath) as TextureImporter;

            while(textureImporter == null) {
                textureImporter = TextureImporter.GetAtPath(assetPath) as TextureImporter;
                yield return true;
            }

            textureImporter.alphaIsTransparency = true;
            textureImporter.compressionQuality = 100;
            textureImporter.sRGBTexture = true;
            textureImporter.npotScale = TextureImporterNPOTScale.None;
            textureImporter.isReadable = false;
            textureImporter.wrapMode = TextureWrapMode.Clamp;
            textureImporter.spritesheet = spriteMetaDatas;
            textureImporter.textureType = TextureImporterType.Sprite;
            textureImporter.spriteImportMode = SpriteImportMode.Multiple;
            textureImporter.spritePixelsPerUnit = 128f;
            textureImporter.filterMode = FilterMode.Point;
            textureImporter.SetPlatformTextureSettings(new TextureImporterPlatformSettings() {
                allowsAlphaSplitting = true,
                compressionQuality = (int)TextureCompressionQuality.Best,
                crunchedCompression = true,
                maxTextureSize = 4096,
                overridden = true,
                textureCompression = TextureImporterCompression.CompressedHQ
            });

            textureImporter.SaveAndReimport();

            yield return true;

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

            {
                Dictionary<KnownChunkId, Sprite[]> spriteChunks = new Dictionary<KnownChunkId, Sprite[]>();

                UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath);
                foreach (UnityEngine.Object asset in assets) {
                    Sprite sprite = asset as Sprite;

                    string[] splittedName = sprite.name.Split(' ');

                    KnownChunkId chunkId = (KnownChunkId)int.Parse(splittedName[0]);
                    uint spriteIndex = uint.Parse(splittedName[1]);

                    Sprite[] chunkSprites;
                    if (!spriteChunks.TryGetValue(chunkId, out chunkSprites))
                        spriteChunks.Add(chunkId, chunkSprites = new Sprite[spriteRectIndices[chunkId].Length]);

                    chunkSprites[spriteIndex] = sprite;
                }

                GraphicsLibrary graphicsLibrary = ScriptableObject.CreateInstance<GraphicsLibrary>();

                foreach (KeyValuePair<KnownChunkId, Sprite[]> chunkSprites in spriteChunks)
                    graphicsLibrary.AddResource((ushort)chunkSprites.Key, (GraphicsChunk)chunkSprites.Value);

                AssetDatabase.CreateAsset(graphicsLibrary, @"Assets/SystemShock/" + libraryAssetPath + @".asset");

                EditorUtility.SetDirty(graphicsLibrary);

                ResourceLibrary.GetController().AddLibrary(graphicsLibrary);
            }
        }
    }
}