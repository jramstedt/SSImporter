using UnityEngine;
using System.Collections;

using SystemShock.Object;
using SystemShock.Resource;
using System;

namespace SystemShock.InstanceObjects {
    public partial class Decoration : SystemShockObject<ObjectInstance.Decoration> {
        public override void InitializeInstance() {
            SystemShockObjectProperties properties = GetComponent<SystemShockObjectProperties>();

            if (properties.Base.DrawType == Resource.DrawType.NoDraw)
                return;

            SystemShockObject ssobject = GetComponent<SystemShockObject>();
            MeshProjector meshProjector = GetComponent<MeshProjector>();
            MeshFilter meshFilter = GetComponent<MeshFilter>();
            MeshRenderer meshRenderer = GetComponentInChildren<MeshRenderer>();

            TextureLibrary modelTextureLibrary = TextureLibrary.GetLibrary(@"citmat.res");
            Material nullMaterial = modelTextureLibrary.GetMaterial(0);

            ObjectFactory objectFactory = ObjectFactory.GetController();

            if (ssobject.SubClass == 2) {
                if (ssobject.Type == 6 || ssobject.Type == 8 || ssobject.Type == 9 || ssobject.Type == 10) {
                    // Nothing
                } else if (ssobject.Type == 7) { // Texture map
                    Destroy(meshProjector);
                    Debug.Log("Texture map", gameObject);
                } else if (ssobject.Type == 3) { // Text
#if UNITY_EDITOR
                    DestroyImmediate(meshProjector);
                    DestroyImmediate(GetComponent<MeshFilter>());
#else
                    Destroy(meshProjector);
                    Destroy(GetComponent<MeshFilter>());
#endif

                    PaletteLibrary paletteLibrary = PaletteLibrary.GetLibrary(@"gamepal.res");
                    StringLibrary stringLibrary = StringLibrary.GetLibrary(@"cybstrng.res");
                    FontLibrary fontLibrary = FontLibrary.GetLibrary(@"gamescr.res");

                    ObjectInstance.Decoration.Text text = ClassData.Data.Read<ObjectInstance.Decoration.Text>();

                    Palette gamePalette = paletteLibrary.GetPalette(KnownChunkId.Palette);

                    ushort[] fontMap = new ushort[] { 606, 609, 602, 605, 607 };

                    float[] sizeMap = new float[] { 0.155f, 0.0775f, 0.0385f, 0.08f, 0.15f, 0.15f };

                    Font font = fontLibrary.GetFont((KnownChunkId)fontMap[text.Font & 0x000F]);

                    CyberString decalWords = stringLibrary.GetStrings(KnownChunkId.DecalWords);

                    TextMesh textMesh = gameObject.AddComponent<TextMesh>();
                    textMesh.offsetZ = -0.0001f;
                    textMesh.font = font;
                    textMesh.alignment = TextAlignment.Center;
                    textMesh.anchor = TextAnchor.MiddleCenter;
                    textMesh.color = gamePalette[text.Color != 0 ? (uint)text.Color : 60];
                    textMesh.richText = false;
                    textMesh.characterSize = sizeMap[(text.Font & 0x00F0) >> 4];
                    textMesh.text = decalWords[text.TextIndex];

                    meshRenderer.sharedMaterial = font.material;

                } else { // Sprite
                    SpriteLibrary objartLibrary = SpriteLibrary.GetLibrary(@"objart.res");
                    SpriteLibrary objart3Library = SpriteLibrary.GetLibrary(@"objart3.res");
                    ObjectPropertyLibrary objectPropertyLibrary = ObjectPropertyLibrary.GetLibrary(@"objprop.dat");

                    SpriteLibrary selectedLibrary = null;

                    ushort spriteIndex = 0;
                    uint animationIndex = ssobject.AnimationState;

                    if (ssobject.Type == 1) { // Icon
                        selectedLibrary = objart3Library;
                        spriteIndex = 311;
                    } else if (ssobject.Type == 2) { // Graffiti
                        selectedLibrary = objart3Library;
                        spriteIndex = 312;
                    } else if (ssobject.Type == 10) { // Repulsor
                        selectedLibrary = objart3Library;
                        spriteIndex = 313;
                    } else { // Sign
                        selectedLibrary = objartLibrary;

                        animationIndex += objectPropertyLibrary.GetSpriteOffset(ssobject.Class, ssobject.SubClass, ssobject.Type);
                        animationIndex += 1; // World sprite
                    }

                    SpriteDefinition sprite = selectedLibrary.GetSpriteAnimation(spriteIndex)[animationIndex];
                    Material material = selectedLibrary.GetMaterial();

                    meshProjector.Size = properties.Base.GetRenderSize(Vector2.Scale(sprite.Rect.size, material.mainTexture.GetSize()));
                    meshProjector.UVRect = sprite.Rect;
                    meshRenderer.sharedMaterial = material;
                }
            } else if (ssobject.SubClass == 7) { // Bridges, catwalks etc.
                if (properties.Base.DrawType == DrawType.Special) {
                    ushort[] textureMap = objectFactory.levelInfo.TextureMap;

                    TextureLibrary textureLibrary = TextureLibrary.GetLibrary(@"texture.res");

                    ObjectInstance.Decoration.Bridge bridge = ClassData.Data.Read<ObjectInstance.Decoration.Bridge>();

                    int bridgeWidth = bridge.Size & (byte)ObjectInstance.Decoration.Bridge.SizeMask.X;
                    int bridgeLength = (bridge.Size & (byte)ObjectInstance.Decoration.Bridge.SizeMask.Y) >> 4;

                    float width = bridgeWidth > 0 ? (float)bridgeWidth / (float)0x04 : properties.Base.Size.x;
                    float length = bridgeLength > 0 ? (float)bridgeLength / (float)0x04 : properties.Base.Size.y;
                    float height = bridge.Height > 0 ? (float)bridge.Height / 32f : 1f / 32f;

                    if (ssobject.Type == 1) { // FIXME 
                        width = 0.5f;
                        length = 1f;
                    }

                    meshFilter.sharedMesh = MeshUtils.CreateCubeTopPivot(width, length, height);

                    if (ssobject.Type == 7 || ssobject.Type == 9) {
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
            }

            Material[] sharedMaterials = meshRenderer.sharedMaterials;
            if (Array.IndexOf<Material>(sharedMaterials, nullMaterial) != -1) {
                ObjectInstance.Decoration.MaterialOverride materialOverride = ClassData.Data.Read<ObjectInstance.Decoration.MaterialOverride>();

                TextureLibrary animationLibrary = TextureLibrary.GetLibrary(@"texture.res.anim");
                
                Material overridingMaterial;

                bool isSurveillance = materialOverride.StartFrameIndex >= 0xF8 && materialOverride.StartFrameIndex <= 0xFF;
                if (materialOverride.StartFrameIndex < 0x007F && materialOverride.Frames > 0) { // Animated texture
                    overridingMaterial = animationLibrary.GetMaterial(materialOverride.StartFrameIndex);
                } else if (materialOverride.StartFrameIndex == 0x007F) { // Random Number
                    overridingMaterial = nullMaterial;
                } else { // > 0x7F
                    if (materialOverride.StartFrameIndex == 246) { // Noise + shodan
                        overridingMaterial = animationLibrary.GetMaterial(63);  // FIXME
                    } else if (materialOverride.StartFrameIndex == 247) { // Noise
                        overridingMaterial = animationLibrary.GetMaterial(63);  // FIXME
                    } else if (isSurveillance) { // Surveillance
                        overridingMaterial = new Material(Shader.Find(@"Standard"));
                    } else if(materialOverride.StartFrameIndex > 0x00FF) { // Text
                        int stringStartIndex = materialOverride.StartFrameIndex & 0x7F;
                        overridingMaterial = nullMaterial;
                    } else { // Model texture
                        overridingMaterial = modelTextureLibrary.GetMaterial((ushort)(51 + (materialOverride.StartFrameIndex & 0x7F)));
                    }
                }

                if (isSurveillance) {
                    Camera camera = objectFactory.levelInfo.SurveillanceCamera[materialOverride.StartFrameIndex & 0x07];
                    overridingMaterial.mainTexture = camera.targetTexture;

                    Surveillance surveillance = gameObject.AddComponent<Surveillance>();
                    surveillance.Camera = camera;
                }

                if (meshProjector != null)
                    meshProjector.Size = properties.Base.GetRenderSize(overridingMaterial.mainTexture.GetSize());

                for (int i = 0; i < sharedMaterials.Length; ++i)
                    if (sharedMaterials[i] == nullMaterial)
                        sharedMaterials[i] = overridingMaterial;

                meshRenderer.sharedMaterials = sharedMaterials;
            }
        }
    }
}

