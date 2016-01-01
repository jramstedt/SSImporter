using UnityEngine;

using System;
using System.Collections;
using System.Collections.Generic;

namespace SystemShock.Resource {
    public class SoundLibrary : AbstractResourceLibrary<SoundLibrary> {
        [SerializeField]
        private List<AudioClip> audioClips;

        [SerializeField, HideInInspector]
        private List<KnownChunkId> indexMap;

        public SoundLibrary() {
            audioClips = new List<AudioClip>();
            indexMap = new List<KnownChunkId>();
        }

        public void AddSound(KnownChunkId chunkId, AudioClip audioClip) {
            if (indexMap.Contains(chunkId))
                throw new ArgumentException(string.Format(@"Sound {0} already set.", chunkId));

            indexMap.Add(chunkId);
            audioClips.Add(audioClip);
        }

        public AudioClip GetSound(KnownChunkId chunkId) {
            int index = indexMap.IndexOf(chunkId);
            return index < 0 ? null : audioClips[index];
        }

        public KnownChunkId GetSoundChunkId(AudioClip audioClip) {
            return indexMap[audioClips.IndexOf(audioClip)];
        }
    }
}