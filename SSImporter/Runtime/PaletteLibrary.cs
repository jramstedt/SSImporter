using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

namespace SystemShock.Resource {
    public class PaletteLibrary : AbstractResourceLibrary<PaletteLibrary> {
        [SerializeField]
        private List<Palette> palettes;

        [SerializeField, HideInInspector]
        private List<ushort> chunkIdMap;

        public PaletteLibrary() {
            palettes = new List<Palette>();
            chunkIdMap = new List<ushort>();
        }

        public void AddPalette(KnownChunkId chunkId, Color[] colors) {
            palettes.Add((Palette)colors);
            chunkIdMap.Add((ushort)chunkId);
        }

        public Palette GetPalette(int paletteIndex) {
            return palettes[paletteIndex];
        }

        public Palette GetPalette(KnownChunkId chunkId) {
            return palettes[chunkIdMap.IndexOf((ushort)chunkId)];
        }
    }

    [Serializable]
    public class Palette : IEnumerable<Color> {
        [SerializeField]
        private Color[] colors;

        public Color[] Colors { get { return colors; } }

        public static explicit operator Palette(Color[] colors) {
            Palette palette = new Palette();
            palette.colors = colors;
            return palette;
        }

        public Color this[uint index] {
            get { return colors[index]; }
        }

        public IEnumerator<Color> GetEnumerator() {
            return ((IEnumerable<Color>)colors).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return colors.GetEnumerator();
        }
    }
}