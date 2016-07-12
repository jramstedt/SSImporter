using UnityEngine;
using UnityEditor;

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using SystemShock.Resource;

namespace SSImporter.Resource {
    public static class TextureImport {
        [MenuItem("Assets/System Shock/7. Import Textures", false, 1007)]
        public static void Init() {
            CreateTextureAssets();
        }

        [MenuItem("Assets/System Shock/7. Import Textures", true)]
        public static bool Validate() {
            return PlayerPrefs.HasKey(@"SSHOCKRES");
        }

        public const bool compressTextures = false;

        private static void CreateTextureAssets() {
            string filePath = PlayerPrefs.GetString(@"SSHOCKRES");

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
                    Debug.Log(textureResource.GetChunkInfo(chunkId).info);

                return;
            }
            */

            #region Read texture properties
            TexturePropertiesLibrary texturePropertiesLibrary = ScriptableObject.CreateInstance<TexturePropertiesLibrary>();
            AssetDatabase.CreateAsset(texturePropertiesLibrary, @"Assets/SystemShock/textprop.dat.asset");

            using (FileStream fs = new FileStream(texturePropertiesLibraryPath, FileMode.Open)) {
                BinaryReader br = new BinaryReader(fs, Encoding.ASCII);

                ushort index = 0;

                /*uint unknownHeader =*/
                br.ReadUInt32();

                int dataSize = Marshal.SizeOf(typeof(TextureProperties));

                while (fs.Position <= (fs.Length - dataSize))
                    texturePropertiesLibrary.AddResource(index++, br.Read<TextureProperties>());
            }

            EditorUtility.SetDirty(texturePropertiesLibrary);

            ResourceLibrary.GetController().AddLibrary(texturePropertiesLibrary);
            #endregion

            try {
                AssetDatabase.StartAssetEditing();

                StringLibrary stringLibrary = StringLibrary.GetLibrary();
                CyberString textureNames = stringLibrary.GetResource(KnownChunkId.TextureNames);

                if (!Directory.Exists(Application.dataPath + @"/SystemShock"))
                    AssetDatabase.CreateFolder(@"Assets", @"SystemShock");

                #region Texture palette
                ResourceFile paletteResource = new ResourceFile(gamePalettePath);
                PaletteChunk gamePalette = paletteResource.ReadPalette(KnownChunkId.Palette);

                PaletteLibrary paletteLibrary = ScriptableObject.CreateInstance<PaletteLibrary>();
                AssetDatabase.CreateAsset(paletteLibrary, @"Assets/SystemShock/gamepal.res.asset");

                foreach (KnownChunkId chunkId in paletteResource.GetChunkList())
                    paletteLibrary.AddResource(chunkId, (Palette)paletteResource.ReadPalette(chunkId).Color32s);

                EditorUtility.SetDirty(paletteLibrary);

                { // Create png for palettes
                    ICollection<KnownChunkId> palettes = paletteResource.GetChunkList();
                    IEnumerator<KnownChunkId> palettesEnumerator = palettes.GetEnumerator();
                    Texture2D paletteTexture = new Texture2D(256, palettes.Count, TextureFormat.RGBA32, false, true);
                    for (int y = 0; y < paletteTexture.height; ++y) {
                        palettesEnumerator.MoveNext();
                        PaletteChunk palette = paletteResource.ReadPalette(palettesEnumerator.Current);
                        paletteTexture.SetPixels32(0, y, paletteTexture.width, 1, palette.Color32s);
                    }

                    File.WriteAllBytes(Application.dataPath + "/SystemShock/gamepal.res.png", paletteTexture.EncodeToPNG());
                }

                ResourceLibrary.GetController().AddLibrary(paletteLibrary);

                #endregion

                #region Create object sprites
                CreateSpriteLibrary(@"Assets/SystemShock/objart.res.asset", gamePalette,
                    new ResourceFile(objartPath),
                    new ResourceFile(objart2Path),
                    new ResourceFile(objart3Path));
                #endregion
                
                // TODO Combine map texture assets and animation texture assets more closely

                TextureLibrary textureLibrary = ScriptableObject.CreateInstance<TextureLibrary>();
                AssetDatabase.CreateAsset(textureLibrary, @"Assets/SystemShock/texture.res.asset");

                #region Create map texture assets
                {
                    AssetDatabase.CreateFolder(@"Assets/SystemShock", @"texture.res");

                    ResourceFile textureResource = new ResourceFile(textureLibraryPath);

                    ushort textureCount = textureResource.GetBlockCount(KnownChunkId.Textures16x16);

                    List<TextureSet> textures = new List<TextureSet>();
                    for (ushort i = 0; i < textureCount; ++i)
                        textures.Add(CreateTexture(i, textureResource, gamePalette));

                    for (ushort textureId = 0; textureId < textures.Count; ++textureId) {
                        TextureSet texture = textures[textureId];

                        string assetPath = string.Format(@"Assets/SystemShock/texture.res/{0}.asset", textureId + " (" + textureNames[textureId] + ")");

                        texture.Diffuse.Apply();
                        EditorUtility.CompressTexture(texture.Diffuse, compressTextures ? TextureFormat.DXT1 : TextureFormat.RGB24, TextureCompressionQuality.Best);
                        AssetDatabase.CreateAsset(texture.Diffuse, assetPath);

                        if (texture.Emission != null) {
                            texture.Emission.Apply();
                            EditorUtility.CompressTexture(texture.Emission, compressTextures ? TextureFormat.DXT1 : TextureFormat.RGB24, TextureCompressionQuality.Best);
                            AssetDatabase.AddObjectToAsset(texture.Emission, assetPath);
                        }

                        Material material = new Material(Shader.Find(@"Standard"));
                        material.name = textureNames[textureId];
                        material.mainTexture = texture.Diffuse;
                        material.SetFloat(@"_Glossiness", 0f);

                        Texture2D emissiveTexture = texture.Emission ?? AssetDatabase.LoadAssetAtPath<Texture2D>(string.Format(@"Assets/SSImporter/Emission/{0:000}.png", textureId));
                        if (emissiveTexture != null) {
                            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive | MaterialGlobalIlluminationFlags.BakedEmissive;
                            material.SetTexture(@"_EmissionMap", emissiveTexture);
                            material.SetColor(@"_EmissionColor", Color.white);
                            material.EnableKeyword(@"_EMISSION");
                        }

                        EditorUtility.SetDirty(material);

                        AssetDatabase.AddObjectToAsset(material, assetPath);

                        textureLibrary.AddResource(textureLibrary.LevelTextureIdToChunk(textureId), material);
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

                    for (ushort textureId = 0; textureId < textures.Count; ++textureId) {
                        TextureSet texture = textures[textureId];

                        string assetPath = string.Format(@"Assets/SystemShock/texture.res.anim/{0}.asset", textureId);

                        texture.Diffuse.Apply();
                        EditorUtility.CompressTexture(texture.Diffuse, compressTextures ? TextureFormat.DXT1Crunched : TextureFormat.RGB24, TextureCompressionQuality.Best);
                        AssetDatabase.CreateAsset(texture.Diffuse, assetPath);

                        Material material = new Material(Shader.Find(@"Standard"));
                        material.name = textureId.ToString();
                        material.color = Color.black;
                        material.SetFloat(@"_Glossiness", 0.75f); // Add little gloss to screens

                        material.SetTexture(@"_EmissionMap", texture.Diffuse);
                        material.SetColor(@"_EmissionColor", Color.white);
                        material.EnableKeyword(@"_EMISSION");

                        EditorUtility.SetDirty(material);

                        AssetDatabase.AddObjectToAsset(material, assetPath);

                        textureLibrary.AddResource(textureLibrary.AnimationTextureIdToChunk(textureId), material);
                    }

                    EditorUtility.SetDirty(textureLibrary);
                }
                #endregion

                #region Create model texture assets
                {
                    AssetDatabase.CreateFolder(@"Assets/SystemShock", @"citmat.res");

                    ResourceFile materialResource = new ResourceFile(citmatPath);

                    foreach (KnownChunkId chunkId in materialResource.GetChunkList()) {
                        TextureSet texture = materialResource.ReadBitmap(chunkId, gamePalette, chunkId.ToString());

                        string assetPath = string.Format(@"Assets/SystemShock/citmat.res/{0}.asset", chunkId);

                        texture.Diffuse.Apply();
                        EditorUtility.CompressTexture(texture.Diffuse, compressTextures ? TextureFormat.DXT1Crunched : TextureFormat.RGB24, TextureCompressionQuality.Best);
                        AssetDatabase.CreateAsset(texture.Diffuse, assetPath);

                        if (texture.Emission != null) {
                            texture.Emission.Apply();
                            EditorUtility.CompressTexture(texture.Emission, compressTextures ? TextureFormat.DXT1Crunched : TextureFormat.RGB24, TextureCompressionQuality.Best);
                            AssetDatabase.AddObjectToAsset(texture.Emission, assetPath);
                        }

                        Material material = new Material(Shader.Find(@"Standard"));
                        material.mainTexture = texture.Diffuse;
                        material.SetFloat(@"_Glossiness", 0f);
                        if (texture.Emission != null) {
                            material.SetTexture(@"_EmissionMap", texture.Emission);
                            material.SetColor(@"_EmissionColor", Color.white);
                            material.EnableKeyword(@"_EMISSION");
                        }

                        AssetDatabase.AddObjectToAsset(material, assetPath);

                        textureLibrary.AddResource(chunkId, material);
                    }

                    EditorUtility.SetDirty(textureLibrary);
                }
                #endregion

                ResourceLibrary.GetController().AddLibrary(textureLibrary);
            } finally {
                AssetDatabase.StopAssetEditing();
                EditorApplication.SaveAssets();
            }

            AssetDatabase.Refresh();
        }

        private static void CreateSpriteLibrary(string libraryAssetPath, PaletteChunk gamePalette, params ResourceFile[] spritesResources) {
            List<Texture2D> allSpritesDiffuse = new List<Texture2D>();
            List<TextureSet> allSprites = new List<TextureSet>();

            Dictionary<KnownChunkId, int[]> spriteRectIndices = new Dictionary<KnownChunkId, int[]>();

            bool hasEmission = false;

            foreach (ResourceFile spritesResource in spritesResources) {
                ICollection<KnownChunkId> spriteChunkIds = spritesResource.GetChunkList();
                foreach (KnownChunkId chunkId in spriteChunkIds) {
                    TextureSet[] sprites = spritesResource.ReadBitmaps(chunkId, gamePalette);

                    int[] indices = new int[sprites.Length];
                    for (int i = 0; i < sprites.Length; ++i)
                        indices[i] = allSpritesDiffuse.Count + i; // Calculates index to rect array from PackTextures

                    spriteRectIndices.Add(chunkId, indices);

                    foreach (TextureSet textureSet in sprites) {
                        allSpritesDiffuse.Add(textureSet.Diffuse);
                        allSprites.Add(textureSet);

                        hasEmission |= textureSet.Emissive;
                    }
                }
            }

            Texture2D atlasDiffuse = new Texture2D(1024, 1024, TextureFormat.RGBA32, true, true);
            Rect[] allRects = atlasDiffuse.PackTextures(allSpritesDiffuse.ToArray(), 1, 4096);
            atlasDiffuse.name = Path.GetFileNameWithoutExtension(libraryAssetPath) + @" Diffuse";
            atlasDiffuse.alphaIsTransparency = true;
            atlasDiffuse.Apply(true, false);
            EditorUtility.CompressTexture(atlasDiffuse, compressTextures ? TextureFormat.DXT5Crunched : TextureFormat.RGBA32, TextureCompressionQuality.Best);

            Texture2D atlasEmission = new Texture2D(atlasDiffuse.width, atlasDiffuse.height, TextureFormat.RGB24, true, true);
            if (hasEmission) {
                atlasEmission.Fill(new Color32(0, 0, 0, 0));
                for (int spriteIndex = 0; spriteIndex < allRects.Length; ++spriteIndex) {
                    TextureSet sprite = allSprites[spriteIndex];
                    if (sprite.Emissive) {
                        Rect pixelRect = allRects[spriteIndex];
                        atlasEmission.SetPixels32(
                            (int)(pixelRect.x * atlasDiffuse.width),
                            (int)(pixelRect.y * atlasDiffuse.height),
                            (int)(pixelRect.width * atlasDiffuse.width),
                            (int)(pixelRect.height * atlasDiffuse.height),
                            sprite.Emission.GetPixels32());
                    }
                }
                atlasEmission.name = Path.GetFileNameWithoutExtension(libraryAssetPath) + @" Emission";
                atlasEmission.Apply(true, false);
                EditorUtility.CompressTexture(atlasEmission, compressTextures ? TextureFormat.DXT1Crunched : TextureFormat.RGB24, TextureCompressionQuality.Best);
            }

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

            SpriteLibrary spriteLibrary = ScriptableObject.CreateInstance<SpriteLibrary>();
            spriteLibrary.Material = material;

            foreach (KeyValuePair<KnownChunkId, int[]> rectIndices in spriteRectIndices) {
                SpriteDefinition[] spriteDefinitions = new SpriteDefinition[rectIndices.Value.Length];

                for (int j = 0; j < spriteDefinitions.Length; ++j) {
                    TextureSet spriteTextureSet = allSprites[j];
                    spriteDefinitions[j] = new SpriteDefinition() {
                        UVRect = allRects[rectIndices.Value[j]],
                        PivotNormalized = Vector2.Scale(spriteTextureSet.Pivot, new Vector2(1f / spriteTextureSet.Diffuse.width, 1f / spriteTextureSet.Diffuse.height)),
                        Name = rectIndices.Key + " " + spriteTextureSet.Name
                    };
                }

                spriteLibrary.AddResource(rectIndices.Key, (SpriteAnimation)spriteDefinitions);
            }

            foreach (TextureSet sprite in allSprites)
                sprite.Dispose();

            AssetDatabase.CreateAsset(spriteLibrary, libraryAssetPath);
            AssetDatabase.AddObjectToAsset(atlasDiffuse, libraryAssetPath);
            if (hasEmission)
                AssetDatabase.AddObjectToAsset(atlasEmission, libraryAssetPath);
            AssetDatabase.AddObjectToAsset(material, libraryAssetPath);

            EditorUtility.SetDirty(material);
            EditorUtility.SetDirty(atlasDiffuse);
            EditorUtility.SetDirty(atlasEmission);
            EditorUtility.SetDirty(spriteLibrary);

            ResourceLibrary.GetController().AddLibrary(spriteLibrary);
        }

        private static TextureSet CreateTexture(ushort textureId, ResourceFile textureResource, PaletteChunk palette) {

            Texture2D completeDiffuse = new Texture2D(128, 128, TextureFormat.RGB24, true, true);
            Texture2D completeEmission = new Texture2D(128, 128, TextureFormat.RGB24, true, true);
            bool emissionHasPixels = false;

            #region 128x128
            if (textureResource.HasChunk(KnownChunkId.Textures128x128Start + textureId)) {
                TextureSet texture = textureResource.ReadBitmap(KnownChunkId.Textures128x128Start + textureId, palette, textureId.ToString());

                completeDiffuse.SetPixels32(0, 0, texture.Diffuse.width, texture.Diffuse.height, texture.Diffuse.GetPixels32(), 0);
                if (texture.Emissive) { emissionHasPixels |= true; completeEmission.SetPixels32(0, 0, texture.Emission.width, texture.Emission.height, texture.Emission.GetPixels32(), 0); }

                texture.Dispose();
            }
            #endregion

            completeDiffuse.Apply();
            completeEmission.Apply();

            #region 64x64
            if (textureResource.HasChunk(KnownChunkId.Textures64x64Start + textureId)) {
                TextureSet texture = textureResource.ReadBitmap(KnownChunkId.Textures64x64Start + textureId, palette, textureId.ToString());

                completeDiffuse.SetPixels32(0, 0, texture.Diffuse.width, texture.Diffuse.height, texture.Diffuse.GetPixels32(), 1);
                if (texture.Emissive) { emissionHasPixels |= true; completeEmission.SetPixels32(0, 0, texture.Emission.width, texture.Emission.height, texture.Emission.GetPixels32(), 1); }

                texture.Dispose();
            }
            #endregion

            #region 32x32
            if (textureResource.HasChunk(KnownChunkId.Textures32x32)) {
                TextureSet texture = textureResource.ReadBitmap(KnownChunkId.Textures32x32, palette, textureId.ToString(), textureId);

                completeDiffuse.SetPixels32(0, 0, texture.Diffuse.width, texture.Diffuse.height, texture.Diffuse.GetPixels32(), 2);
                if (texture.Emissive) { emissionHasPixels |= true; completeEmission.SetPixels32(0, 0, texture.Emission.width, texture.Emission.height, texture.Emission.GetPixels32(), 2); }

                texture.Dispose();
            }
            #endregion

            #region 16x16
            if (textureResource.HasChunk(KnownChunkId.Textures16x16)) {
                TextureSet texture = textureResource.ReadBitmap(KnownChunkId.Textures16x16, palette, textureId.ToString(), textureId);

                completeDiffuse.SetPixels32(0, 0, texture.Diffuse.width, texture.Diffuse.height, texture.Diffuse.GetPixels32(), 3);
                if (texture.Emissive) { emissionHasPixels |= true; completeEmission.SetPixels32(0, 0, texture.Emission.width, texture.Emission.height, texture.Emission.GetPixels32(), 3); }

                texture.Dispose();
            }
            #endregion

            completeDiffuse.Apply(false);
            
            if (!emissionHasPixels) {
                Texture2D.DestroyImmediate(completeEmission);
                completeEmission = null;
            } else {
                completeEmission.Apply(false);
            }

            return new TextureSet() {
                Name = textureId.ToString(),
                Diffuse = completeDiffuse,
                Emission = completeEmission,
                Emissive = emissionHasPixels
            };
        }
    }
}