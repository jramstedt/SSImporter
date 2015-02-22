using UnityEngine;
using UnityEditor;

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using SystemShock.Resource;

namespace SSImporter.Resource {
    public class TextureImport {
        [MenuItem("Assets/System Shock/Import Textures")]
        public static void Init() {
            CreateTextureAssets();
        }

        private static void CreateTextureAssets() {
            //string filePath = EditorUtility.OpenFolderPanel(@"System Shock RES folder", @"", @"RES");

            string filePath = @"D:\Users\Janne\Downloads\SYSTEMSHOCK-Portable-v1.2.3\RES";

            string gamePalettePath = filePath + @"\DATA\gamepal.res";
            string texturePropertiesLibraryPath = filePath + @"\DATA\textprop.dat";

            string textureLibraryPath = filePath + @"\DATA\texture.res";
            string citmatPath = filePath + @"\DATA\citmat.res";

            string objartPath = filePath + @"\DATA\objart.res";
            string objart2Path = filePath + @"\DATA\objart2.res";
            string objart3Path = filePath + @"\DATA\objart3.res";

            if (!File.Exists(gamePalettePath) ||
                !File.Exists(texturePropertiesLibraryPath) ||
                !File.Exists(textureLibraryPath) ||
                !File.Exists(citmatPath) ||
                !File.Exists(objartPath) ||
                !File.Exists(objart2Path) ||
                !File.Exists(objart3Path))
                return;

            /*
            {
                ResourceFile textureResource = new ResourceFile(textureLibraryPath);

                foreach (KnownChunkId chunkId in textureResource.GetChunkList())
                    Debug.Log(chunkId);

                return;
            }
            */
            StringLibrary stringLibrary = AssetDatabase.LoadAssetAtPath(@"Assets/SystemShock/cybstrng.res.asset", typeof(StringLibrary)) as StringLibrary;
            CyberString textureNames = stringLibrary.GetStrings(KnownChunkId.TextureNames);

            //AssetDatabase.StartAssetEditing();

            #region Read texture properties
            List<TextureProperties> textureProperties = new List<TextureProperties>();

            using (FileStream fs = new FileStream(texturePropertiesLibraryPath, FileMode.Open)) {
                BinaryReader br = new BinaryReader(fs, Encoding.ASCII);
                while (fs.Position < fs.Length)
                    textureProperties.Add(br.Read<TextureProperties>());
            }
            #endregion

            if (!Directory.Exists(Application.dataPath + @"/SystemShock"))
                AssetDatabase.CreateFolder(@"Assets", @"SystemShock");

            #region Texture palette
            ResourceFile paletteResource = new ResourceFile(gamePalettePath);
            PaletteChunk gamePalette = paletteResource.ReadPalette(KnownChunkId.Palette);

            PaletteLibrary paletteLibrary = ScriptableObject.CreateInstance<PaletteLibrary>();
            AssetDatabase.CreateAsset(paletteLibrary, @"Assets/SystemShock/gamepal.res.asset");

            foreach (KnownChunkId chunkId in paletteResource.GetChunkList())
                paletteLibrary.AddPalette(chunkId, paletteResource.ReadPalette(chunkId).Colors);

            EditorUtility.SetDirty(paletteLibrary);

            { // Create png for palettes
                ICollection<KnownChunkId> palettes = paletteResource.GetChunkList();
                IEnumerator<KnownChunkId> palettesEnumerator = palettes.GetEnumerator();
                Texture2D paletteTexture = new Texture2D(256, palettes.Count, TextureFormat.RGBA32, false, true);
                for (int y = 0; y < paletteTexture.height; ++y) {
                    palettesEnumerator.MoveNext();
                    PaletteChunk palette = paletteResource.ReadPalette(palettesEnumerator.Current);
                    paletteTexture.SetPixels(0, y, paletteTexture.width, 1, palette.Colors);
                }

                File.WriteAllBytes(Application.dataPath + "/SystemShock/gamepal.res.png", paletteTexture.EncodeToPNG());
            }
            #endregion

            #region Create object sprites
            {
                ResourceFile spritesResource = new ResourceFile(objartPath);

                CreateSpriteLibrary(spritesResource, @"Assets/SystemShock/objart.res.asset", gamePalette);
                /*
                SpriteLibrary spriteLibrary = ScriptableObject.CreateInstance<SpriteLibrary>();
                AssetDatabase.CreateAsset(spriteLibrary, @"Assets/SystemShock/objart.res.asset");

                ushort spriteCount = spritesResource.GetChunkBlockCount(KnownChunkId.ObjectSprites);
                Texture2D[] sprites = new Texture2D[spriteCount];
                for (ushort spriteId = 0; spriteId < spriteCount; ++spriteId)
                    sprites[spriteId] = spritesResource.ReadBitmap(KnownChunkId.ObjectSprites, gamePalette, spriteId);

                Texture2D atlas = new Texture2D(1024, 1024, TextureFormat.RGBA32, true, true);
                Rect[] rects = atlas.PackTextures(sprites, 1, 4096);
                atlas.Apply(true, true);
                EditorUtility.CompressTexture(atlas, TextureFormat.DXT5, TextureCompressionQuality.Best);
                atlas.name = @"objart.res sprites";
                AssetDatabase.AddObjectToAsset(atlas, spriteLibrary);

                Material material = new Material(Shader.Find(@"Standard"));
                material.mainTexture = atlas;

                AssetDatabase.AddObjectToAsset(material, spriteLibrary);

                EditorUtility.SetDirty(spriteLibrary);

                spriteLibrary.SetSprites(material, atlas, rects);

                foreach (Texture2D sprite in sprites)
                    Texture2D.DestroyImmediate(sprite);
                */
            }
            #endregion

            #region Create object sprites
            {
                ResourceFile spritesResource = new ResourceFile(objart2Path);

                CreateSpriteLibrary(spritesResource, @"Assets/SystemShock/objart2.res.asset", gamePalette);

                /*
                SpriteLibrary spriteLibrary = ScriptableObject.CreateInstance<SpriteLibrary>();
                AssetDatabase.CreateAsset(spriteLibrary, @"Assets/SystemShock/objart2.res.asset");

                ICollection<KnownChunkId> spriteChunkIds = spritesResource.GetChunkList();
                Texture2D[] sprites = new Texture2D[spriteChunkIds.Count];
                for (ushort spriteId = 0; spriteId < sprites.Length; ++spriteId)
                    sprites[spriteId] = spritesResource.ReadBitmap(KnownChunkId.EnemyAnimationStart + spriteId, gamePalette);

                Texture2D atlas = new Texture2D(1024, 1024, TextureFormat.RGBA32, true, true);
                Rect[] rects = atlas.PackTextures(sprites, 1, 4096);
                atlas.Apply(true, true);
                EditorUtility.CompressTexture(atlas, TextureFormat.DXT5, TextureCompressionQuality.Best);
                atlas.name = @"objart2.res sprites";
                AssetDatabase.AddObjectToAsset(atlas, spriteLibrary);

                Material material = new Material(Shader.Find(@"Standard"));
                material.mainTexture = atlas;

                AssetDatabase.AddObjectToAsset(material, spriteLibrary);

                EditorUtility.SetDirty(spriteLibrary);

                spriteLibrary.SetSprites(material, atlas, rects);

                foreach (Texture2D sprite in sprites)
                    Texture2D.DestroyImmediate(sprite);
                */
            }
            #endregion

            #region Create object sprites
            {
                ResourceFile spritesResource = new ResourceFile(objart3Path);

                CreateSpriteLibrary(spritesResource, @"Assets/SystemShock/objart3.res.asset", gamePalette);
            }
            #endregion

            #region Create map texture assets
            {
                AssetDatabase.CreateFolder(@"Assets/SystemShock", @"texture.res");

                ResourceFile textureResource = new ResourceFile(textureLibraryPath);

                List<TextureSet> textures = new List<TextureSet>();
                for (ushort i = 0; i < 273; ++i)
                    textures.Add(CreateTexture(i, textureResource, gamePalette));

                TextureLibrary textureLibrary = ScriptableObject.CreateInstance<TextureLibrary>();
                AssetDatabase.CreateAsset(textureLibrary, @"Assets/SystemShock/texture.res.asset");

                for (ushort textureId = 0; textureId < textures.Count; ++textureId) {
                    TextureSet texture = textures[textureId];

                    string assetPath = string.Format(@"Assets/SystemShock/texture.res/{0}.asset", textureId + " (" + textureNames[textureId] + ")");
                    AssetDatabase.CreateAsset(texture.Diffuse, assetPath);
                    if (texture.Emission != null)
                        AssetDatabase.AddObjectToAsset(texture.Emission, assetPath);

                    Material material = new Material(Shader.Find(@"Standard"));
                    material.name = textureNames[textureId];
                    material.mainTexture = texture.Diffuse;
                    material.SetFloat(@"_Glossiness", 0f);

                    Texture2D emissiveTexture = texture.Emission ?? AssetDatabase.LoadAssetAtPath(string.Format(@"Assets/SSImporter/Emission/{0:000}.png", textureId), typeof(Texture2D)) as Texture2D;
                    if (emissiveTexture != null) {
                        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive | MaterialGlobalIlluminationFlags.BakedEmissive;
                        material.SetTexture(@"_EmissionMap", emissiveTexture);
                        material.SetColor(@"_EmissionColor", Color.white);
                        material.EnableKeyword(@"_EMISSION");
                    }

                    if (texture.Animated)
                        material.mainTextureScale = new Vector2(0.25f, 1f);

                    EditorUtility.SetDirty(material);

                    AssetDatabase.AddObjectToAsset(material, assetPath);

                    string guid = AssetDatabase.AssetPathToGUID(assetPath);

                    textureLibrary.SetTexture(textureId, guid, textureProperties[textureId]);
                }

                EditorUtility.SetDirty(textureLibrary);
            }
            #endregion

            #region Create animation texture assets
            {
                AssetDatabase.CreateFolder(@"Assets/SystemShock", @"texture.res.anim");

                ResourceFile textureResource = new ResourceFile(textureLibraryPath);

                List<TextureSet> textures = new List<TextureSet>();
                for (ushort i = 0; textureResource.HasChunk(KnownChunkId.AnimationsStart + i); ++i)
                    textures.Add(textureResource.ReadBitmap(KnownChunkId.AnimationsStart + i, gamePalette, i.ToString()));

                TextureLibrary textureLibrary = ScriptableObject.CreateInstance<TextureLibrary>();
                AssetDatabase.CreateAsset(textureLibrary, @"Assets/SystemShock/texture.res.anim.asset");

                for (ushort textureId = 0; textureId < textures.Count; ++textureId) {
                    TextureSet texture = textures[textureId];

                    string assetPath = string.Format(@"Assets/SystemShock/texture.res.anim/{0}.asset", textureId);
                    AssetDatabase.CreateAsset(texture.Diffuse, assetPath);
                    if (texture.Emission != null)
                        AssetDatabase.AddObjectToAsset(texture.Emission, assetPath);

                    Material material = new Material(Shader.Find(@"Standard"));
                    material.name = textureNames[textureId];
                    material.mainTexture = texture.Diffuse;
                    material.SetFloat(@"_Glossiness", 0f);

                    Texture2D emissiveTexture = texture.Emission /*?? AssetDatabase.LoadAssetAtPath(string.Format(@"Assets/Emission/{0:000}.png", textureId), typeof(Texture2D)) as Texture2D*/;
                    if (emissiveTexture != null) {
                        material.SetTexture(@"_EmissionMap", emissiveTexture);
                        material.SetColor(@"_EmissionColor", Color.white);
                        material.EnableKeyword(@"_EMISSION");
                    }

                    EditorUtility.SetDirty(material);

                    AssetDatabase.AddObjectToAsset(material, assetPath);

                    string guid = AssetDatabase.AssetPathToGUID(assetPath);

                    textureLibrary.SetTexture(textureId, guid, textureProperties[textureId]);
                }

                EditorUtility.SetDirty(textureLibrary);
            }
            #endregion

            #region Create model texture assets
            {
                AssetDatabase.CreateFolder(@"Assets/SystemShock", @"citmat.res");

                ResourceFile materialResource = new ResourceFile(citmatPath);

                TextureLibrary materialLibrary = ScriptableObject.CreateInstance<TextureLibrary>();
                AssetDatabase.CreateAsset(materialLibrary, @"Assets/SystemShock/citmat.res.asset");

                ushort materialId = 0;
                foreach (KnownChunkId chunkId in materialResource.GetChunkList()) {
                    TextureSet texture = materialResource.ReadBitmap(chunkId, gamePalette, materialId.ToString());

                    texture.Diffuse.Apply();
                    EditorUtility.CompressTexture(texture.Diffuse, TextureFormat.DXT1, TextureCompressionQuality.Best);

                    if (texture.Emission != null) {
                        texture.Emission.Apply();
                        EditorUtility.CompressTexture(texture.Emission, TextureFormat.DXT1, TextureCompressionQuality.Best);
                    }

                    string assetPath = string.Format(@"Assets/SystemShock/citmat.res/{0}.asset", materialId);

                    AssetDatabase.CreateAsset(texture.Diffuse, assetPath);
                    if (texture.Emission != null)
                        AssetDatabase.AddObjectToAsset(texture.Emission, assetPath);

                    Material material = new Material(Shader.Find(@"Standard"));
                    material.mainTexture = texture.Diffuse;
                    material.SetFloat(@"_Glossiness", 0f);
                    if (texture.Emission != null) {
                        material.SetTexture(@"_EmissionMap", texture.Emission);
                        material.SetColor(@"_EmissionColor", Color.white);
                        material.EnableKeyword(@"_EMISSION");
                    }

                    AssetDatabase.AddObjectToAsset(material, assetPath);

                    string guid = AssetDatabase.AssetPathToGUID(assetPath);

                    materialLibrary.SetTexture(materialId, guid, textureProperties[materialId]);

                    ++materialId;
                }

                EditorUtility.SetDirty(materialLibrary);
            }
            #endregion

            //AssetDatabase.StopAssetEditing();

            AssetDatabase.SaveAssets();
            EditorApplication.SaveAssets();

            AssetDatabase.Refresh();

            Resources.UnloadUnusedAssets();
        }

        private static void CreateSpriteLibrary(ResourceFile spritesResource, string libraryAssetPath, PaletteChunk gamePalette) {
            ICollection<KnownChunkId> spriteChunkIds = spritesResource.GetChunkList();

            List<Texture2D> allSpritesDiffuse = new List<Texture2D>();
            List<TextureSet> allSprites = new List<TextureSet>();
            List<int[]> spriteRectIndices = new List<int[]>();

            bool hasEmission = false;

            foreach (KnownChunkId chunkId in spriteChunkIds) {
                TextureSet[] sprites = spritesResource.ReadBitmaps(chunkId, gamePalette);

                int[] indices = new int[sprites.Length];
                for (int i = 0; i < sprites.Length; ++i)
                    indices[i] = allSpritesDiffuse.Count + i; // Calculates index to rect array from PackTextures

                spriteRectIndices.Add(indices);

                foreach (TextureSet textureSet in sprites) {
                    allSpritesDiffuse.Add(textureSet.Diffuse);
                    allSprites.Add(textureSet);

                    hasEmission |= textureSet.Emissive;
                }
            }

            Texture2D atlasDiffuse = new Texture2D(1024, 1024, TextureFormat.RGBA32, true, true);
            Rect[] allRects = atlasDiffuse.PackTextures(allSpritesDiffuse.ToArray(), 1, 4096);
            atlasDiffuse.name = Path.GetFileNameWithoutExtension(libraryAssetPath) + @" Diffuse";
            atlasDiffuse.alphaIsTransparency = true;
            atlasDiffuse.Apply(true, false);
            EditorUtility.CompressTexture(atlasDiffuse, TextureFormat.DXT5, TextureCompressionQuality.Best);

            Texture2D atlasEmission = new Texture2D(atlasDiffuse.width, atlasDiffuse.height, TextureFormat.RGB24, true, true);
            if (hasEmission) {
                atlasEmission.Fill(new Color32(0, 0, 0, 0));
                for (int spriteIndex = 0; spriteIndex < allRects.Length; ++spriteIndex) {
                    TextureSet sprite = allSprites[spriteIndex];
                    if (sprite.Emissive) {
                        Rect pixelRect = allRects[spriteIndex];
                        atlasEmission.SetPixels(
                            (int)(pixelRect.x * atlasDiffuse.width),
                            (int)(pixelRect.y * atlasDiffuse.height),
                            (int)(pixelRect.width * atlasDiffuse.width),
                            (int)(pixelRect.height * atlasDiffuse.height),
                            sprite.Emission.GetPixels());
                    }
                }
                atlasEmission.name = Path.GetFileNameWithoutExtension(libraryAssetPath) + @" Emission";
                atlasEmission.Apply(true, false);
                EditorUtility.CompressTexture(atlasEmission, TextureFormat.DXT1, TextureCompressionQuality.Best);
            }

            /*
            File.WriteAllBytes(Application.dataPath + "/SystemShock/gamepal.res.png", atlas.EncodeToPNG());

            AssetDatabase.Refresh();

            TextureImporter textureImporter = TextureImporter.GetAtPath(@"Assets/SystemShock/gamepal.res.png") as TextureImporter;
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
                pixelRect.x = uvRect.x * atlas.width;
                pixelRect.y = uvRect.y * atlas.height;
                pixelRect.width = uvRect.width * atlas.width;
                pixelRect.height = uvRect.height * atlas.height;

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
            */

            Material material = new Material(Shader.Find(@"Standard"));
            material.mainTexture = atlasDiffuse;
            material.SetFloat(@"_Glossiness", 0f);
            material.SetFloat(@"_Mode", 1f); // Cutoff
            material.SetFloat(@"_Cutoff", 0.25f);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            material.SetInt("_ZWrite", 1);
            material.EnableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = 2450;
            if (hasEmission) {
                material.SetTexture(@"_EmissionMap", atlasEmission);
                material.SetColor(@"_EmissionColor", Color.white);
                material.EnableKeyword(@"_EMISSION");
            }

            SpriteDefinition[][] unitySprites = new SpriteDefinition[spriteRectIndices.Count][];
            for (int i = 0, nameIndex = 0; i < unitySprites.Length; ++i) {
                int[] rectIndices = spriteRectIndices[i];
                SpriteDefinition[] unitySprite = new SpriteDefinition[rectIndices.Length];

                for (int j = 0; j < rectIndices.Length; ++j, ++nameIndex) {
                    TextureSet spriteTextureSet = allSprites[nameIndex];
                    unitySprite[j] = new SpriteDefinition() {
                        Rect = allRects[rectIndices[j]],
                        Pivot = Vector2.Scale(spriteTextureSet.Pivot, new Vector2(1f / spriteTextureSet.Diffuse.width, 1f / spriteTextureSet.Diffuse.height)),
                        Name = i + " " + spriteTextureSet.Name
                    };
                }

                unitySprites[i] = unitySprite;
            }

            /*
            Sprite[][] unitySprites = new Sprite[spriteRectIndices.Count][];
            for (int i = 0, nameIndex = 0; i < unitySprites.Length; ++i) {
                int[] rectIndices = spriteRectIndices[i];
                Sprite[] unitySprite = new Sprite[rectIndices.Length];

                for (int j = 0; j < rectIndices.Length; ++j, ++nameIndex) {
                    Rect pixelRect = allRects[rectIndices[j]];
                    pixelRect.x *= atlasDiffuse.width;
                    pixelRect.y *= atlasDiffuse.height;
                    pixelRect.width *= atlasDiffuse.width;
                    pixelRect.height *= atlasDiffuse.height;

                    Vector2 pivot = allSpritePivot[nameIndex];
                    pivot.x /= pixelRect.width;
                    pivot.y /= pixelRect.height;

                    Sprite sprite = Sprite.Create(atlasDiffuse, pixelRect, pivot, 100f, 0, SpriteMeshType.Tight);
                    sprite.name = i + " " + spriteNames[nameIndex];

                    Debug.Log(pivot + " " + sprite.pivot + " " + sprite.);

                    unitySprite[j] = sprite;
                }

                unitySprites[i] = unitySprite;
            }
            */

            foreach (TextureSet sprite in allSprites)
                sprite.Dispose();

            SpriteLibrary spriteLibrary = ScriptableObject.CreateInstance<SpriteLibrary>();
            spriteLibrary.SetSprites(material, unitySprites);

            EditorUtility.SetDirty(material);
            EditorUtility.SetDirty(atlasDiffuse);
            EditorUtility.SetDirty(atlasEmission);
            EditorUtility.SetDirty(spriteLibrary);

            AssetDatabase.CreateAsset(atlasDiffuse, libraryAssetPath);
            if(hasEmission) AssetDatabase.AddObjectToAsset(atlasEmission, libraryAssetPath);
            AssetDatabase.AddObjectToAsset(material, libraryAssetPath);
            AssetDatabase.AddObjectToAsset(spriteLibrary, libraryAssetPath);
        }

        private static TextureSet CreateTexture(ushort textureId, ResourceFile textureResource, PaletteChunk palette) {
            TextureSet fullSizeTexture = textureResource.ReadBitmap(KnownChunkId.Textures128x128Start + textureId, palette, textureId.ToString());
            fullSizeTexture.Dispose();

            Texture2D completeDiffuse = new Texture2D(128, 128, TextureFormat.RGB24, true, true);
            Texture2D completeEmission = new Texture2D(128, 128, TextureFormat.RGB24, true, true);
            bool emissionHasPixels = false;

            #region 128x128
            {
                TextureSet texture = textureResource.ReadBitmap(KnownChunkId.Textures128x128Start + textureId, palette, textureId.ToString());

                completeDiffuse.SetPixels(0, 0, texture.Diffuse.width, texture.Diffuse.height, texture.Diffuse.GetPixels(), 0);
                if (texture.Emissive) { emissionHasPixels |= true; completeEmission.SetPixels(0, 0, texture.Emission.width, texture.Emission.height, texture.Emission.GetPixels(), 0); }

                texture.Dispose();
            }
            #endregion

            completeDiffuse.Apply();
            completeEmission.Apply();

            #region 64x64
            {
                TextureSet texture = textureResource.ReadBitmap(KnownChunkId.Textures64x64Start + textureId, palette, textureId.ToString());

                completeDiffuse.SetPixels(0, 0, texture.Diffuse.width, texture.Diffuse.height, texture.Diffuse.GetPixels(), 1);
                if (texture.Emissive) { emissionHasPixels |= true; completeEmission.SetPixels(0, 0, texture.Emission.width, texture.Emission.height, texture.Emission.GetPixels(), 1); }

                texture.Dispose();
            }
            #endregion

            #region 32x32
            {
                TextureSet texture = textureResource.ReadBitmap(KnownChunkId.Textures32x32, palette, textureId.ToString(), textureId);

                completeDiffuse.SetPixels(0, 0, texture.Diffuse.width, texture.Diffuse.height, texture.Diffuse.GetPixels(), 2);
                if (texture.Emissive) { emissionHasPixels |= true; completeEmission.SetPixels(0, 0, texture.Emission.width, texture.Emission.height, texture.Emission.GetPixels(), 2); }

                texture.Dispose();
            }
            #endregion

            #region 16x16
            {
                TextureSet texture = textureResource.ReadBitmap(KnownChunkId.Textures16x16, palette, textureId.ToString(), textureId);

                completeDiffuse.SetPixels(0, 0, texture.Diffuse.width, texture.Diffuse.height, texture.Diffuse.GetPixels(), 3);
                if (texture.Emissive) { emissionHasPixels |= true; completeEmission.SetPixels(0, 0, texture.Emission.width, texture.Emission.height, texture.Emission.GetPixels(), 3); }

                texture.Dispose();
            }
            #endregion

            completeDiffuse.Apply(false);
            EditorUtility.CompressTexture(completeDiffuse, TextureFormat.DXT1, TextureCompressionQuality.Best);
            
            if (!emissionHasPixels) {
                Texture2D.DestroyImmediate(completeEmission);
                completeEmission = null;
            } else {
                completeEmission.Apply(false);
                EditorUtility.CompressTexture(completeEmission, TextureFormat.DXT1, TextureCompressionQuality.Best);
            }

            return new TextureSet() {
                Name = fullSizeTexture.Name,
                Diffuse = completeDiffuse,
                Emission = completeEmission,
                Animated = false,
                Emissive = emissionHasPixels
            };
        }
    }
}