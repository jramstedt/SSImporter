using UnityEngine;

using System;
using System.Collections;
using System.Collections.Generic;

namespace SystemShock.Resource {
    public class SpriteLibrary : AbstractResourceLibrary<SpriteLibrary, ushort /*KnownChunkId*/, SpriteAnimation> {
        [HideInInspector]
        public Material Material;

        public Texture2D GetAtlas() {
            return Material.mainTexture as Texture2D;
        }

        public SpriteAnimation GetResource(KnownChunkId chunkId) {
            return GetResource((ushort)chunkId);
        }

#if UNITY_EDITOR
        public virtual void AddResource(KnownChunkId identifier, SpriteAnimation resource) {
            AddResource((ushort)identifier, resource);
        }
#endif
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

        public static implicit operator SpriteDefinition[](SpriteAnimation spriteAnimation) {
            return spriteAnimation.sprites;
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
        public Rect UVRect;
        public Vector2 PivotNormalized;
    }
}
