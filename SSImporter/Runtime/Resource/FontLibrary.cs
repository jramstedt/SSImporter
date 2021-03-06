﻿using UnityEngine;

namespace SystemShock.Resource {
    public class FontLibrary : AbstractResourceLibrary<FontLibrary, ushort /*KnownChunkId*/, Font> {
        public Font GetResource(KnownChunkId chunkId) {
            return GetResource((ushort)chunkId);
        }

#if UNITY_EDITOR
        public virtual void AddResource(KnownChunkId identifier, Font resource) {
            AddResource((ushort)identifier, resource);
        }
#endif
        }
}