using UnityEngine;

using System;
using System.Collections;
using System.Collections.Generic;

namespace SystemShock.Resource {
    public class SpriteLibrary : AbstractResourceLibrary<SpriteLibrary> {
        [SerializeField]
        private SpriteAnimation[] animations;

        [SerializeField]
        private Material material;

        public SpriteLibrary() {
        }

        public void SetSprites(Material spriteMaterial, SpriteDefinition[][] sprites) {
            animations = new SpriteAnimation[sprites.Length];
            for (int i = 0; i < sprites.Length; ++i)
                animations[i] = (SpriteAnimation)sprites[i];

            material = spriteMaterial;
        }

        public Texture2D GetAtlas() {
            return material.mainTexture as Texture2D;
        }

        public Material GetMaterial() {
            return material;
        }

        public SpriteAnimation GetSpriteAnimation(ushort spriteId) {
            return animations[spriteId];
        }
    }

    [Serializable]
    public class SpriteAnimation : IEnumerable<SpriteDefinition> {
        [SerializeField]
        private SpriteDefinition[] sprites;

        public SpriteDefinition[] Sprites { get { return sprites; } }

        public static explicit operator SpriteAnimation(SpriteDefinition[] sprites) {
            SpriteAnimation spriteAnimation = new SpriteAnimation();
            spriteAnimation.sprites = sprites;
            return spriteAnimation;
        }

        public static explicit operator SpriteDefinition(SpriteAnimation spriteAnimation) {
            return spriteAnimation.sprites[0];
        }

        public SpriteDefinition this[uint index] {
            get { return sprites[index]; }
        }

        public IEnumerator<SpriteDefinition> GetEnumerator() {
            return ((IEnumerable<SpriteDefinition>)sprites).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return sprites.GetEnumerator();
        }
    }

    [Serializable]
    public struct SpriteDefinition {
        public string Name;
        public Rect Rect;
        public Vector2 Pivot;
    }
}
