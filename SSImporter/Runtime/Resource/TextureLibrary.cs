using UnityEngine;

using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace SystemShock.Resource {
    public class TextureLibrary : AbstractResourceLibrary<TextureLibrary, ushort /*KnownChunkId*/, Material> {
        public KnownChunkId ModelTextureIdToChunk(ushort textureId) {
            return KnownChunkId.ModelTexturesStart + textureId;
        }
        /*
        public Material GetModelTexture(ushort textureId) {
            return GetResource(ModelTextureIdToChunk(textureId));
        }
        */
        public KnownChunkId LevelTextureIdToChunk(ushort textureId) {
            return KnownChunkId.Textures128x128Start + textureId;
        }

        public Material GetLevelTexture(ushort textureId) {
            return GetResource(LevelTextureIdToChunk(textureId));
        }

        public ushort GetLevelTextureIdentifier(Material texture) {
            return (ushort)(GetIdentifier(texture) - (ushort)KnownChunkId.Textures128x128Start);
        }

        public Material[] GetLevelTextures(ushort[] texureIds) {
            Material[] frames = new Material[texureIds.Length];
            for (ushort i = 0; i < frames.Length; ++i)
                frames[i] = GetLevelTexture(texureIds[i]);

            return frames;
        }

        public KnownChunkId AnimationTextureIdToChunk(ushort textureId) {
            return KnownChunkId.AnimationsStart + textureId;
        }

        public Material GetAnimationTexture(ushort textureId) {
            return GetResource(AnimationTextureIdToChunk(textureId));
        }

        public Material[] GetAnimationTextures(ushort startTexureId, ushort count) {
            Material[] frames = new Material[count];
            for (ushort i = 0; i < count; ++i)
                frames[i] = GetAnimationTexture((ushort)(startTexureId + i));

            return frames;
        }

        public Material GetResource(KnownChunkId chunkId) {
            return GetResource((ushort)chunkId);
        }

#if UNITY_EDITOR
        public virtual void AddResource(KnownChunkId identifier, Material resource) {
            AddResource((ushort)identifier, resource);
        }
#endif
    }
}
