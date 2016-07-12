using UnityEngine;

using System;
using System.Collections;
using System.Collections.Generic;

namespace SystemShock.Resource {
    public class PaletteLibrary : AbstractResourceLibrary<PaletteLibrary, ushort /*KnownChunkId*/, Palette> {
        public Palette GetResource(KnownChunkId chunkId) {
            return GetResource((ushort)chunkId);
        }

#if UNITY_EDITOR
        public virtual void AddResource(KnownChunkId identifier, Palette resource) {
            AddResource((ushort)identifier, resource);
        }
#endif
    }

    [Serializable]
    public class Palette : IEnumerable<Color32> {
        [SerializeField]
        private Color32[] colors;

        public Color32[] Colors { get { return colors; } }

        public static explicit operator Palette(Color32[] colors) {
            Palette palette = new Palette();
            palette.colors = colors;
            return palette;
        }

        public Color this[uint index] {
            get { return colors[index]; }
        }

        public IEnumerator<Color32> GetEnumerator() {
            return ((IEnumerable<Color32>)colors).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return colors.GetEnumerator();
        }
    }
}