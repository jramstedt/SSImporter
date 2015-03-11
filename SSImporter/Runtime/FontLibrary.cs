using UnityEngine;

using System;
using System.Collections;
using System.Collections.Generic;

namespace SystemShock.Resource {
    public class FontLibrary : AbstractResourceLibrary<FontLibrary> {
        [SerializeField]
        private Font[] fonts;

        public Font[] Fonts { get { return fonts; } }

        [SerializeField]
        private uint[] chunkIds;

        public uint[] ChunkIds { get { return chunkIds; } }

        public void SetFonts(Dictionary<uint, Font> fontDictionary) {
            chunkIds = new uint[fontDictionary.Keys.Count];
            fontDictionary.Keys.CopyTo(chunkIds, 0);

            fonts = new Font[fontDictionary.Values.Count];
            for (int i = 0; i < fonts.Length; ++i)
                fonts[i] = (Font)fontDictionary[chunkIds[i]];
        }

        public Font GetFont(KnownChunkId chunkId) {
            return fonts[Array.IndexOf(chunkIds, (uint)chunkId)];
        }
    }
}