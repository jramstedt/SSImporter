using UnityEngine;

namespace SystemShock.Resource {
    public class SoundLibrary : AbstractResourceLibrary<ushort /*KnownChunkId*/, AudioClip> {
        public AudioClip GetResource(KnownChunkId chunkId) {
            return GetResource((ushort)chunkId);
        }

#if UNITY_EDITOR
        public virtual void AddResource(KnownChunkId identifier, AudioClip resource) {
            AddResource((ushort)identifier, resource);
        }
#endif
    }
}