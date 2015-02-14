using UnityEngine;

using System;
using System.Collections;
using System.Collections.Generic;

namespace SystemShock.Resource {
    public class SpriteLibrary : ScriptableObject {
        [SerializeField]
        private SpriteAnimation[] animations;

        [SerializeField]
        private Material material;

        public SpriteLibrary() {
        }

        public void SetSprites(Material spriteMaterial, Sprite[][] sprites) {
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
    public class SpriteAnimation : IEnumerable<Sprite> {
        [SerializeField]
        private Sprite[] sprites;

        public Sprite[] Sprites { get { return sprites; } }

        public static explicit operator SpriteAnimation(Sprite[] sprites) {
            SpriteAnimation spriteAnimation = new SpriteAnimation();
            spriteAnimation.sprites = sprites;
            return spriteAnimation;
        }

        public static explicit operator Sprite(SpriteAnimation spriteAnimation) {
            return spriteAnimation.sprites[0];
        }

        public Sprite this[uint index] {
            get { return sprites[index]; }
        }

        public IEnumerator<Sprite> GetEnumerator() {
            return ((IEnumerable<Sprite>)sprites).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return sprites.GetEnumerator();
        }
    }
}
