using UnityEngine;
using System.Collections;

using SystemShock.Object;
using SystemShock.Resource;

namespace SystemShock.InstanceObjects {
    public partial class DoorAndGrating : SystemShockObject<ObjectInstance.DoorAndGrating> {
        protected override void InitializeInstance() {
            SystemShockObjectProperties properties = GetComponent<SystemShockObjectProperties>();

            if (properties.Base.DrawType == Resource.DrawType.NoDraw)
                return;

            ObjectPropertyLibrary objectPropertyLibrary = ObjectPropertyLibrary.GetLibrary(@"objprop.dat");
            SpriteLibrary objart3Library = SpriteLibrary.GetLibrary(@"objart3.res");

            MeshFilter meshFilter = GetComponent<MeshFilter>();
            MeshRenderer meshRenderer = GetComponent<MeshRenderer>();

            if (properties.Base.DrawType == Resource.DrawType.ForceDoor) {
                meshFilter.sharedMesh = MeshUtils.CreateTwoSidedPlane(properties.Base.Size);

                Color color = new Color(0.5f, 0f, 0f, 0.75f);
                Color emission = new Color(0.25f, 0f, 0f, 1f);

                if (ClassData.ForceColor == 253) {
                    color = new Color(0f, 0f, 0.75f, 0.75f);
                    emission = new Color(0f, 0f, 0.4f, 1f);
                } else if (ClassData.ForceColor == 254) {
                    color = new Color(0f, 0.75f, 0f, 0.75f);
                    emission = new Color(0f, 0.4f, 0f, 1f);
                }

                Material colorMaterial = new Material(Shader.Find(@"Standard")); // TODO should be screen?
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

                meshRenderer.sharedMaterial = colorMaterial;
            } else {
                int startIndex = objectPropertyLibrary.GetIndex(ObjectClass.DoorAndGrating, 0, 0);
                int spriteIndex = objectPropertyLibrary.GetIndex(Class, SubClass, Type);

                SpriteAnimation spriteAnimation = objart3Library.GetSpriteAnimation((ushort)(270 + (spriteIndex - startIndex)));

                SpriteDefinition sprite = spriteAnimation[AnimationState];
                Material material = objart3Library.GetMaterial();

                meshRenderer.sharedMaterial = material;

                if (((Flags)properties.Base.Flags & Flags.Activable) == Flags.Activable && spriteAnimation.Sprites.Length > 1) {
                    meshFilter.sharedMesh = MeshUtils.CreateTwoSidedPlane(
                        sprite.Pivot,
                        Vector2.one);
                    meshFilter.sharedMesh.name = sprite.Name;

                    Door door = gameObject.AddComponent<Door>();
                    door.Frames = spriteAnimation.Sprites;
                    door.CurrentFrame = AnimationState;

                    gameObject.AddComponent<TriggerOnMouseDown>();
                } else {
                    meshFilter.sharedMesh = MeshUtils.CreateTwoSidedPlane(
                        sprite.Pivot,
                        new Vector2(sprite.Rect.width * material.mainTexture.width / 64f, sprite.Rect.height * material.mainTexture.height / 64f),
                        sprite.Rect);
                    meshFilter.sharedMesh.name = sprite.Name;
                }
            }

            BoxCollider boxCollider = GetComponent<BoxCollider>();
            boxCollider.center = meshFilter.sharedMesh.bounds.center;
            boxCollider.size = meshFilter.sharedMesh.bounds.size;
        }
    }
}

