using UnityEngine;
using System.Collections;
using SystemShock.Resource;
using System.Collections.Generic;
using System;

namespace SSImporter.Resource {
    public class GraphicsLibrary : AbstractResourceLibrary<GraphicsLibrary, ushort /*KnownChunkId*/, GraphicsChunk> {

        public GraphicsChunk GetResource(KnownChunkId chunkId) {
            return GetResource((ushort)chunkId);
        }

        public KeyValuePair<KnownChunkId, int> GetIdentifiers(Sprite sprite) {
            foreach (ushort identifier in IndexMap) {
                GraphicsChunk sprites = GetResource(identifier);
                int spriteIndex = Array.IndexOf(sprites, sprite);

                if (spriteIndex != -1)
                    return new KeyValuePair<KnownChunkId, int>((KnownChunkId)identifier, spriteIndex);
            }

            throw new Exception("Sprite not found! " + sprite.name);
        }
    }

    [Serializable]
    public class GraphicsChunk : IEnumerable<Sprite> {
        [SerializeField]
        private Sprite[] sprites;

        public static explicit operator GraphicsChunk(Sprite[] sprites) {
            GraphicsChunk graphicsChunk = new GraphicsChunk();
            graphicsChunk.sprites = sprites;
            return graphicsChunk;
        }

        public static implicit operator Sprite[] (GraphicsChunk graphicsChunk) {
            return graphicsChunk.sprites;
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