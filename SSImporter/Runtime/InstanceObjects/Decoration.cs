﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;

using SystemShock.Object;
using SystemShock.Resource;
using System;
using System.Linq;

namespace SystemShock.InstanceObjects {
    public partial class Decoration : SystemShockObject<ObjectInstance.Decoration> {
        protected override void InitializeInstance() {
            SystemShockObjectProperties properties = GetComponent<SystemShockObjectProperties>();

            if (properties.Base.DrawType == Resource.DrawType.NoDraw)
                return;

            MeshProjector meshProjector = GetComponent<MeshProjector>();
            MeshRenderer meshRenderer = GetComponentInChildren<MeshRenderer>();

            TextureLibrary modelTextureLibrary = TextureLibrary.GetLibrary(@"citmat.res");
            Material nullMaterial = modelTextureLibrary.GetMaterial(0);

            ObjectFactory objectFactory = ObjectFactory.GetController();

            if (SubClass == 2) {
                if (Type == 6 || Type == 7 || Type == 8 || Type == 9 || Type == 10) {
                    // Nothing
                } else if (Type == 3) { // Text
                    PaletteLibrary paletteLibrary = PaletteLibrary.GetLibrary(@"gamepal.res");
                    StringLibrary stringLibrary = StringLibrary.GetLibrary(@"cybstrng.res");
                    FontLibrary fontLibrary = FontLibrary.GetLibrary(@"gamescr.res");

                    ObjectInstance.Decoration.Text text = ClassData.Data.Read<ObjectInstance.Decoration.Text>();

                    Palette gamePalette = paletteLibrary.GetPalette(KnownChunkId.Palette);

                    ushort[] fontMap = new ushort[] { 606, 609, 602, 605, 606 };

                    float[] sizeMap = new float[] { 1f, 0.125f, 0.25f, 0.5f, 1f, 2f };

                    Font font = fontLibrary.GetFont((KnownChunkId)fontMap[text.Font & 0x000F]);

                    CyberString decalWords = stringLibrary.GetStrings(KnownChunkId.DecalWords);

                    MeshText meshText = GetComponent<MeshText>();
                    meshText.Color = gamePalette[text.Color != 0 ? (uint)text.Color : 53];
                    meshText.Font = font;
                    meshText.Text = decalWords[text.TextIndex];

                    float scale = 1f/64f * sizeMap[(text.Font & 0x00F0) >> 4];
                    meshText.transform.localScale = new Vector3(scale, scale, scale);
                } else { // Sprite
                    SpriteLibrary objartLibrary = SpriteLibrary.GetLibrary(@"objart.res");
                    SpriteLibrary objart3Library = SpriteLibrary.GetLibrary(@"objart3.res");
                    ObjectPropertyLibrary objectPropertyLibrary = ObjectPropertyLibrary.GetLibrary(@"objprop.dat");

                    SpriteLibrary selectedLibrary = null;

                    ushort spriteIndex = 0;
                    uint animationIndex = State;

                    if (Type == 1) { // Icon
                        selectedLibrary = objart3Library;
                        spriteIndex = 311;
                    } else if (Type == 2) { // Graffiti
                        selectedLibrary = objart3Library;
                        spriteIndex = 312;
                    } else if (Type == 10) { // Repulsor
                        selectedLibrary = objart3Library;
                        spriteIndex = 313;
                    } else { // Sign
                        selectedLibrary = objartLibrary;

                        animationIndex += objectPropertyLibrary.GetSpriteOffset(Class, SubClass, Type);
                        animationIndex += 1; // World sprite
                    }

                    SpriteDefinition sprite = selectedLibrary.GetSpriteAnimation(spriteIndex)[animationIndex];
                    Material material = selectedLibrary.GetMaterial();

                    meshProjector.Size = properties.Base.GetRenderSize(Vector2.Scale(sprite.Rect.size, material.mainTexture.GetSize()));
                    meshProjector.UVRect = sprite.Rect;
                    meshRenderer.sharedMaterial = material;
                }
            } else if (SubClass == 7) { // Bridges, catwalks etc.
                if (properties.Base.DrawType == DrawType.Special) {
                    ushort[] textureMap = objectFactory.LevelInfo.TextureMap;

                    TextureLibrary textureLibrary = TextureLibrary.GetLibrary(@"texture.res");

                    ObjectInstance.Decoration.Bridge bridge = ClassData.Data.Read<ObjectInstance.Decoration.Bridge>();

                    int bridgeWidth = bridge.Size & (byte)ObjectInstance.Decoration.Bridge.SizeMask.X;
                    int bridgeLength = (bridge.Size & (byte)ObjectInstance.Decoration.Bridge.SizeMask.Y) >> 4;

                    float defaultWidth = properties.Base.Size.x;
                    float defaultLength = properties.Base.Size.y;

                    if (Type == 1) { // Strange override
                        defaultWidth = 0.5f;
                        defaultLength = 1f;
                    }

                    float width = bridgeWidth > 0 ? (float)bridgeWidth / (float)0x04 : defaultWidth;
                    float length = bridgeLength > 0 ? (float)bridgeLength / (float)0x04 : defaultLength;
                    float height = bridge.Height > 0 ? (float)bridge.Height / 32f : 1f / 32f;

                    MeshFilter meshFilter = GetComponent<MeshFilter>();
                    meshFilter.sharedMesh = MeshUtils.CreateCubeTopPivot(width, length, height);

                    if (Type == 7 || Type == 9) {
                        Color color = new Color(0.5f, 0f, 0f, 0.75f);
                        Color emission = new Color(0.25f, 0f, 0f, 1f);

                        if (bridge.ForceColor == 253) {
                            color = new Color(0f, 0f, 0.75f, 0.75f);
                            emission = new Color(0f, 0f, 0.4f, 1f);
                        } else if (bridge.ForceColor == 254) {
                            color = new Color(0f, 0.75f, 0f, 0.75f);
                            emission = new Color(0f, 0.4f, 0f, 1f);
                        }

                        Material colorMaterial = new Material(Shader.Find(@"Standard")); // TODO should be screen blendmode?
                        colorMaterial.color = color;
                        colorMaterial.SetFloat(@"_Mode", 2f); // Fade
                        colorMaterial.SetColor(@"_EmissionColor", emission);
                        colorMaterial.SetFloat(@"_Glossiness", 0f);

                        colorMaterial.SetColor("_EmissionColorUI", colorMaterial.GetColor(@"_EmissionColor"));
                        colorMaterial.SetFloat("_EmissionScaleUI", 1f);

                        colorMaterial.EnableKeyword(@"_EMISSION");

                        colorMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        colorMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        colorMaterial.SetInt("_ZWrite", 0);
                        colorMaterial.DisableKeyword("_ALPHATEST_ON");
                        colorMaterial.EnableKeyword("_ALPHABLEND_ON");
                        colorMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                        colorMaterial.renderQueue = 3000;

                        meshRenderer.sharedMaterials = new Material[] {
                            colorMaterial,
                            colorMaterial
                        };
                    } else {
                        byte topBottomTexture = (byte)(bridge.TopBottomTextures & (byte)ObjectInstance.Decoration.Bridge.TextureMask.Texture);
                        byte sideTexture = (byte)(bridge.SideTextures & (byte)ObjectInstance.Decoration.Bridge.TextureMask.Texture);

                        Material topBottomMaterial = bridge.TopBottomTextures > 0 ?
                                (bridge.TopBottomTextures & (byte)ObjectInstance.Decoration.Bridge.TextureMask.MapTexture) == (byte)ObjectInstance.Decoration.Bridge.TextureMask.MapTexture ?
                                textureLibrary.GetMaterial(textureMap[topBottomTexture]) :
                                modelTextureLibrary.GetMaterial((ushort)(51 + topBottomTexture)) :
                                textureLibrary.GetMaterial(textureMap[0]);

                        Material sideMaterial = bridge.SideTextures > 0 ?
                                (bridge.SideTextures & (byte)ObjectInstance.Decoration.Bridge.TextureMask.MapTexture) == (byte)ObjectInstance.Decoration.Bridge.TextureMask.MapTexture ?
                                textureLibrary.GetMaterial(textureMap[sideTexture]) :
                                modelTextureLibrary.GetMaterial((ushort)(51 + sideTexture)) :
                                textureLibrary.GetMaterial(textureMap[0]);

                        meshRenderer.sharedMaterials = new Material[] {
                            topBottomMaterial,
                            sideMaterial
                        };

                    }

                    GetComponent<MeshCollider>().sharedMesh = meshFilter.sharedMesh;
                }
            } else if (SubClass == 5) {
                if (Type == 4) { // Camera
                    ObjectInstance.Decoration.Camera camera = ClassData.Data.Read<ObjectInstance.Decoration.Camera>();
                    if (camera.Rotating != 0)
                        gameObject.AddComponent<RotatingCamera>();
                }
            }

            Material[] sharedMaterials = meshRenderer.sharedMaterials;
            if (Array.IndexOf<Material>(sharedMaterials, nullMaterial) != -1) {
                ObjectInstance.Decoration.MaterialOverride materialOverride = ClassData.Data.Read<ObjectInstance.Decoration.MaterialOverride>();

                TextureLibrary animationLibrary = TextureLibrary.GetLibrary(@"texture.res.anim");

                List<int> nullMaterialIndices = new List<int>();
                for (int i = 0; i < sharedMaterials.Length; ++i) {
                    if (sharedMaterials[i] == nullMaterial)
                        nullMaterialIndices.Add(i);
                }

                Material overridingMaterial;

                bool isAnimated = false;
                bool isSurveillance = materialOverride.StartFrameIndex >= 0x00F8 && materialOverride.StartFrameIndex <= 0x00FF;

                if (isSurveillance && objectFactory.LevelInfo.SurveillanceCameras[materialOverride.StartFrameIndex & 0x07] == null) { // No surveillance camera found.
                    materialOverride.StartFrameIndex = 0x00F7; // Override with noise.
                    isSurveillance = false;
                }

                bool isScreen = SubClass == 2 && (Type == 6 || Type == 8 || Type == 9); // Special case for broken screens where Frames == 0

                if (materialOverride.StartFrameIndex < 0x007F && (materialOverride.Frames > 0 || isScreen)) { // Animated texture
                    isAnimated = true;
                    overridingMaterial = animationLibrary.GetMaterial(materialOverride.StartFrameIndex);
                } else if (materialOverride.StartFrameIndex == 0x00F6) { // Noise + shodan
                    isAnimated = true;

                    materialOverride.Frames = 6;
                    //materialOverride.AnimationType = 2;
                    materialOverride.StartFrameIndex = 63;

                    overridingMaterial = animationLibrary.GetMaterial(63);

                    NoiseScreen noiseScreen = gameObject.AddComponent<NoiseScreen>();
                    noiseScreen.MaterialIndices = nullMaterialIndices.ToArray();

                    gameObject.AddComponent<ShodanScreen>();
                } else if (materialOverride.StartFrameIndex == 0x00F7) { // Noise
                    NoiseScreen noiseScreen = gameObject.AddComponent<NoiseScreen>();
                    overridingMaterial = noiseScreen.SetupMaterial();
                    noiseScreen.MaterialIndices = nullMaterialIndices.ToArray();
                } else if (isSurveillance) { // Surveillance
                    overridingMaterial = new Material(Shader.Find(@"Standard"));
                } else if (materialOverride.StartFrameIndex > 0x00FF) { // Text
                    StringLibrary stringLibrary = StringLibrary.GetLibrary(@"cybstrng.res");

                    int stringIndex = materialOverride.StartFrameIndex & 0x7F;
                    bool scrollVertically = (materialOverride.StartFrameIndex & 0x80) == 0x80;
                    bool isRandomScreen = stringIndex == 0x7F || stringIndex == 0x7E;

                    if (isRandomScreen) { // Random number in level before CPU is destroyed
                        materialOverride.Frames = 10;
                        stringIndex = 52;
                    }

                    TextScreen textScreen = gameObject.AddComponent<TextScreen>();
                    textScreen.Frames = materialOverride.Frames;
                    textScreen.Texts = new string[] { stringLibrary.GetStrings(KnownChunkId.ScreenTexts)[(uint)stringIndex] };
                    textScreen.Texture = new RenderTexture(128, 128, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                    textScreen.Texture.DiscardContents(false, true);
                    textScreen.Alignment = isRandomScreen ? TextAnchor.MiddleCenter : scrollVertically ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft;
                    textScreen.FPS = 2.5f;
                    textScreen.Type = isRandomScreen ? TextScreen.AnimationType.Random : TextScreen.AnimationType.Normal;

                    if (scrollVertically || isRandomScreen) {
                        int linesNeeded = scrollVertically ? materialOverride.Frames + TextScreen.LinesNeeded : materialOverride.Frames;
                        Array.Resize(ref textScreen.Texts, linesNeeded);
                        for (int i = 0; i < linesNeeded; ++i)
                            textScreen.Texts[i] = stringLibrary.GetStrings(KnownChunkId.ScreenTexts)[(uint)(stringIndex + i)];
                    }

                    overridingMaterial = new Material(Shader.Find(@"Standard"));
                    overridingMaterial.color = Color.black;
                    overridingMaterial.SetFloat(@"_Glossiness", 0.75f); // Add little gloss to screens
                    overridingMaterial.SetTexture(@"_EmissionMap", textScreen.Texture);
                    overridingMaterial.SetColor(@"_EmissionColor", Color.white);
                    overridingMaterial.EnableKeyword(@"_EMISSION");
                } else { // Model texture
                    if (SubClass == 2 && Type == 7) {
                        TextureLibrary textureLibrary = TextureLibrary.GetLibrary(@"texture.res");
                        ushort[] textureMap = objectFactory.LevelInfo.TextureMap;
                        overridingMaterial = textureLibrary.GetMaterial(textureMap[materialOverride.StartFrameIndex & 0x7F]);
                    } else {
                        overridingMaterial = modelTextureLibrary.GetMaterial((ushort)(51 + (materialOverride.StartFrameIndex & 0x7F)));
                    }
                }

                if (isSurveillance) {
                    LevelInfo.SurveillanceCamera surveillanceCamera = objectFactory.LevelInfo.SurveillanceCameras[materialOverride.StartFrameIndex & 0x07];
                    Camera camera = surveillanceCamera.Camera;
                    overridingMaterial.color = Color.black;

                    //Screens are blacklit, so use diffuse texture as emission!
                    overridingMaterial.SetTexture(@"_EmissionMap", camera.targetTexture);
                    overridingMaterial.SetColor(@"_EmissionColor", Color.white);
                    overridingMaterial.EnableKeyword(@"_EMISSION");

                    Surveillance surveillance = gameObject.AddComponent<Surveillance>();
                    surveillance.Camera = camera;

                    // TODO Add death watch to screen.
                }

                if (meshProjector != null) {
                    Texture projectedTexture = overridingMaterial.mainTexture ?? overridingMaterial.GetTexture(@"_EmissionMap");
                    meshProjector.Size = properties.Base.GetRenderSize(projectedTexture.GetSize());
                }

                foreach(int nullMaterialIndex in nullMaterialIndices)
                    sharedMaterials[nullMaterialIndex] = overridingMaterial;

                if (isAnimated) {
                    Material[] frames = animationLibrary.GetMaterialAnimation(materialOverride.StartFrameIndex, materialOverride.Frames);
                    AnimateMaterial animate = gameObject.GetComponent<AnimateMaterial>() ?? gameObject.AddComponent<AnimateMaterial>();

                    LoopConfiguration loopConfiguration;
                    if(objectFactory.LevelInfo.LoopConfigurations.TryGetValue(ObjectId, out loopConfiguration)) {
                        float fps = 256f / loopConfiguration.Frametime;

                        int wrapModeIndex = loopConfiguration.LoopWrapMode % (int)AnimateMaterial.WrapMode.EnumLength;
                        animate.AddAnimation(nullMaterialIndices.ToArray(), frames, (AnimateMaterial.WrapMode)wrapModeIndex, fps);
                    } else {
                        animate.AddAnimation(nullMaterialIndices.ToArray(), frames, AnimateMaterial.WrapMode.Repeat, 2f);
                    }
                }

                meshRenderer.sharedMaterials = sharedMaterials;
            }
        }
    }
}

