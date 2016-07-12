using UnityEngine;
using System.Collections;
using System.Collections.Generic;

using SystemShock.Object;
using SystemShock.Resource;
using System;
using System.Linq;

namespace SystemShock.InstanceObjects {
    [ExecuteInEditMode]
    public partial class Decoration : SystemShockObject<ObjectInstance.Decoration> {
        [SerializeField, HideInInspector]
        protected bool overrideColor;
        [SerializeField, HideInInspector]
        protected Color colorOverride;

        [SerializeField, HideInInspector]
        protected bool overrideEmission;
        [SerializeField, HideInInspector]
        protected Color emissionOverride;

        protected void Start() {
            MeshRenderer meshRenderer = GetComponentInChildren<MeshRenderer>();
            if(meshRenderer && (overrideColor || overrideEmission)) {
                MaterialPropertyBlock materialPropertyBlock = new MaterialPropertyBlock();
                meshRenderer.GetPropertyBlock(materialPropertyBlock);

                if(overrideColor)
                    materialPropertyBlock.SetColor(@"_Color", colorOverride);

                if(overrideEmission)
                    materialPropertyBlock.SetColor(@"_EmissionColor", emissionOverride);


                meshRenderer.SetPropertyBlock(materialPropertyBlock);
            }


        }

        protected override void InitializeInstance() {
            SystemShockObjectProperties properties = GetComponent<SystemShockObjectProperties>();

            if (properties.Base.DrawType == Resource.DrawType.NoDraw)
                return;

            MeshProjector meshProjector = GetComponent<MeshProjector>();
            MeshRenderer meshRenderer = GetComponentInChildren<MeshRenderer>();

            TextureLibrary textureLibrary = TextureLibrary.GetLibrary();
            Material nullModelMaterial = textureLibrary.GetResource(KnownChunkId.ModelTexturesStart);

            ObjectFactory objectFactory = ObjectFactory.GetController();

            if (SubClass == 2) {
                if (Type == 6 || Type == 7 || Type == 8 || Type == 9) {
                    // Nothing
                } else if (Type == 3) { // Text
                    ObjectInstance.Decoration.Text text = ClassData.Data.Read<ObjectInstance.Decoration.Text>();

                    Palette gamePalette = PaletteLibrary.GetLibrary().GetResource(KnownChunkId.Palette);

                    ushort[] fontMap = new ushort[] { 606, 609, 602, 605, 606 };

                    float[] sizeMap = new float[] { 1f, 0.125f, 0.25f, 0.5f, 1f, 2f };

                    Font font = FontLibrary.GetLibrary().GetResource((KnownChunkId)fontMap[text.Font & 0x000F]);

                    CyberString decalWords = StringLibrary.GetLibrary().GetResource(KnownChunkId.DecalWords);

                    Color color = gamePalette[text.Color != 0 ? (uint)text.Color : 53];

                    MeshText meshText = GetComponent<MeshText>();
                    meshText.Font = font;
                    meshText.Text = decalWords[text.TextIndex];

                    overrideColor = true;
                    overrideEmission = true;

                    colorOverride = color;
                    emissionOverride = color * 0.1f;

                    float scale = 1f / 64f * sizeMap[(text.Font & 0x00F0) >> 4];
                    meshText.transform.localScale = new Vector3(scale, scale, scale);
                } else { // Sprite
                    KnownChunkId spriteChunk = KnownChunkId.ObjectSprites;
                    uint animationIndex = State;

                    if (Type == 1) { // Icon
                        spriteChunk = KnownChunkId.Icon;
                    } else if (Type == 2) { // Graffiti
                        spriteChunk = KnownChunkId.Graffiti;
                    } else if (Type == 10) { // Repulsor
                        spriteChunk = KnownChunkId.Repulsor;
                    } else { // Sign
                        animationIndex += ObjectPropertyLibrary.GetLibrary().GetSpriteOffset(CombinedId);
                        animationIndex += 1; // World sprite
                    }

                    SpriteLibrary spriteLibrary = SpriteLibrary.GetLibrary();
                    SpriteDefinition sprite = spriteLibrary.GetResource(spriteChunk)[animationIndex];
                    Material material = spriteLibrary.Material;

                    meshProjector.Size = properties.Base.GetRenderSize(Vector2.Scale(sprite.UVRect.size, material.mainTexture.GetSize()));
                    meshProjector.UVRect = sprite.UVRect;
                    meshRenderer.sharedMaterial = material;
                }
            } else if (SubClass == 7) { // Bridges, catwalks etc.
                if (properties.Base.DrawType == DrawType.Special) {
                    ushort[] textureMap = objectFactory.LevelInfo.TextureMap;

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
                    float height = bridge.Height > 0 ? (float)bridge.Height : 1f;

                    transform.localScale = new Vector3(width, height, length);

                    if (Type == 7 || Type == 9) { // forcebridge
                        colorOverride = new Color(0.5f, 0f, 0f, 0.75f);
                        emissionOverride = new Color(0.25f, 0f, 0f, 1f);

                        if (bridge.ForceColor == 253) {
                            colorOverride = new Color(0f, 0f, 0.75f, 0.75f);
                            emissionOverride = new Color(0f, 0f, 0.4f, 1f);
                        } else if (bridge.ForceColor == 254) {
                            colorOverride = new Color(0f, 0.75f, 0f, 0.75f);
                            emissionOverride = new Color(0f, 0.4f, 0f, 1f);
                        }

                        overrideColor = true;
                        overrideEmission = true;
                    } else {
                        byte topBottomTexture = (byte)(bridge.TopBottomTextures & (byte)ObjectInstance.Decoration.Bridge.TextureMask.Texture);
                        byte sideTexture = (byte)(bridge.SideTextures & (byte)ObjectInstance.Decoration.Bridge.TextureMask.Texture);

                        Material topBottomMaterial = bridge.TopBottomTextures > 0 ?
                                (bridge.TopBottomTextures & (byte)ObjectInstance.Decoration.Bridge.TextureMask.MapTexture) == (byte)ObjectInstance.Decoration.Bridge.TextureMask.MapTexture ?
                                textureLibrary.GetLevelTexture(textureMap[topBottomTexture]) :
                                textureLibrary.GetResource((ushort)(KnownChunkId.DynamicModelTexturesStart + topBottomTexture)) :
                                textureLibrary.GetLevelTexture(textureMap[0]);

                        Material sideMaterial = bridge.SideTextures > 0 ?
                                (bridge.SideTextures & (byte)ObjectInstance.Decoration.Bridge.TextureMask.MapTexture) == (byte)ObjectInstance.Decoration.Bridge.TextureMask.MapTexture ?
                                textureLibrary.GetLevelTexture(textureMap[sideTexture]) :
                                textureLibrary.GetResource((ushort)(KnownChunkId.DynamicModelTexturesStart + sideTexture)) :
                                textureLibrary.GetLevelTexture(textureMap[0]);

                        meshRenderer.sharedMaterials = new Material[] {
                            topBottomMaterial,
                            sideMaterial
                        };
                    }

                    MeshFilter meshFilter = GetComponent<MeshFilter>();
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
            if (Array.IndexOf<Material>(sharedMaterials, nullModelMaterial) != -1) {
                ObjectInstance.Decoration.MaterialOverride materialOverride = ClassData.Data.Read<ObjectInstance.Decoration.MaterialOverride>();

                List<int> nullMaterialIndices = new List<int>();
                for (int i = 0; i < sharedMaterials.Length; ++i) {
                    if (sharedMaterials[i] == nullModelMaterial)
                        nullMaterialIndices.Add(i);
                }

                Material overridingMaterial = null;

                bool isAnimated = false;

                bool isScreen = properties.Base.DrawType == Resource.DrawType.Screen;

                if (materialOverride.StartFrameIndex < 0x007F && (materialOverride.Frames > 0 || isScreen)) { // Animated texture
                    isAnimated = true;
                    overridingMaterial = textureLibrary.GetAnimationTexture(materialOverride.StartFrameIndex);
                } else if (materialOverride.StartFrameIndex == 0x00F6) { // Noise + shodan
                    isAnimated = true;

                    materialOverride.Frames = 6;
                    //materialOverride.AnimationType = 2;
                    materialOverride.StartFrameIndex = 63;

                    Material tmpRef = null;
                    NoiseScreen noiseScreen = gameObject.AddComponent<NoiseScreen>();
                    noiseScreen.SetupMaterial(ref tmpRef, nullMaterialIndices.ToArray());

                    overridingMaterial = textureLibrary.GetAnimationTexture(63);
                    gameObject.AddComponent<ShodanScreen>();
                } else if (materialOverride.StartFrameIndex == 0x00F7) { // Noise
                    NoiseScreen noiseScreen = gameObject.AddComponent<NoiseScreen>();
                    noiseScreen.SetupMaterial(ref overridingMaterial, nullMaterialIndices.ToArray());
                } else if (materialOverride.StartFrameIndex >= 0x00F8 && materialOverride.StartFrameIndex <= 0x00FF) { // Surveillance
                    SurveillanceScreen surveillance = gameObject.AddComponent<SurveillanceScreen>();
                    surveillance.SetupMaterial(ref overridingMaterial, nullMaterialIndices.ToArray());
                } else if (materialOverride.StartFrameIndex > 0x00FF) { // Text screen
                    StringLibrary stringLibrary = StringLibrary.GetLibrary();

                    int stringIndex = materialOverride.StartFrameIndex & 0x7F;
                    bool scrollVertically = (materialOverride.StartFrameIndex & 0x80) == 0x80;
                    bool smallText = (materialOverride.StartFrameIndex & 0x800) == 0x800;
                    bool isRandomScreen = stringIndex == 0x7F || stringIndex == 0x7E;

                    if (isRandomScreen) { // Random number in level before CPU is destroyed
                        materialOverride.Frames = 10;
                        stringIndex = 52;
                    }

                    TextScreen textScreen = gameObject.AddComponent<TextScreen>();
                    textScreen.Frames = materialOverride.Frames;
                    textScreen.Texts = new string[] { stringLibrary.GetResource(KnownChunkId.ScreenTexts)[(uint)stringIndex] };
                    textScreen.Texture = new RenderTexture(128, 128, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                    textScreen.Texture.DiscardContents(false, true);
                    textScreen.Alignment = isRandomScreen ? TextAnchor.MiddleCenter : scrollVertically ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft;
                    textScreen.FPS = 2.5f;
                    textScreen.Type = isRandomScreen ? TextScreen.AnimationType.Random : TextScreen.AnimationType.Normal;
                    textScreen.SmallText = smallText;

                    if (scrollVertically || isRandomScreen) {
                        int linesNeeded = scrollVertically ? materialOverride.Frames + TextScreen.LinesNeeded : materialOverride.Frames;
                        Array.Resize(ref textScreen.Texts, linesNeeded);
                        for (int i = 0; i < linesNeeded; ++i)
                            textScreen.Texts[i] = stringLibrary.GetResource(KnownChunkId.ScreenTexts)[(uint)(stringIndex + i)];
                    }

                    // TODO get global screen material.

                    overridingMaterial = new Material(Shader.Find(@"Standard"));
                    overridingMaterial.color = Color.black;
                    overridingMaterial.SetFloat(@"_Glossiness", 0.75f); // Add little gloss to screens
                    overridingMaterial.SetTexture(@"_EmissionMap", textScreen.Texture);
                    overridingMaterial.SetColor(@"_EmissionColor", Color.white);
                    overridingMaterial.EnableKeyword(@"_EMISSION");
                } else { // Model texture
                    if (SubClass == 2 && Type == 7) {
                        ushort[] textureMap = objectFactory.LevelInfo.TextureMap;
                        overridingMaterial = textureLibrary.GetLevelTexture(textureMap[materialOverride.StartFrameIndex & 0x7F]);
                    } else {
                        overridingMaterial = textureLibrary.GetResource(KnownChunkId.DynamicModelTexturesStart + (ushort)(materialOverride.StartFrameIndex & 0x7F));
                    }
                }

                if (meshProjector != null) {
                    Texture projectedTexture = overridingMaterial.mainTexture ?? overridingMaterial.GetTexture(@"_EmissionMap");
                    meshProjector.Size = properties.Base.GetRenderSize(projectedTexture != null ? projectedTexture.GetSize() : new Vector2(64f, 64f));
                }

                foreach (int nullMaterialIndex in nullMaterialIndices)
                    sharedMaterials[nullMaterialIndex] = overridingMaterial;

                if (isAnimated) {
                    Material[] frames = textureLibrary.GetAnimationTextures(materialOverride.StartFrameIndex, materialOverride.Frames);
                    AnimateMaterial animate = gameObject.GetComponent<AnimateMaterial>() ?? gameObject.AddComponent<AnimateMaterial>();

                    LoopConfiguration loopConfiguration;
                    if (objectFactory.LevelInfo.LoopConfigurations.TryGetValue(ObjectId, out loopConfiguration)) {
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

