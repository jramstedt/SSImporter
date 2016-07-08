using UnityEngine;

namespace SystemShock.Resource {
    public class ModelLibrary : AbstractResourceLibrary<ushort /*KnownChunkId*/, GameObject> {
        public GameObject GetResource(KnownChunkId chunkId) {
            return GetResource((ushort)chunkId);
        }

#if UNITY_EDITOR
        public virtual void AddResource(KnownChunkId identifier, GameObject resource) {
            AddResource((ushort)identifier, resource);
        }
#endif
    }
}
