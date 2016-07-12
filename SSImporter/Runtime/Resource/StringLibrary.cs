using UnityEngine;

using System;
using System.Collections;
using System.Collections.Generic;

namespace SystemShock.Resource {
    public class StringLibrary : AbstractResourceLibrary<StringLibrary, ushort /*KnownChunkId*/, CyberString> {
        public CyberString[] Strings { get { return Resources.ToArray(); } }
        public ushort[] ChunkIds { get { return IndexMap.ToArray(); } }

        public CyberString GetResource(KnownChunkId chunkId) {
            return GetResource((ushort)chunkId);
        }

#if UNITY_EDITOR
        public virtual void AddResource(KnownChunkId identifier, CyberString resource) {
            AddResource((ushort)identifier, resource);
        }
#endif
    }

    [Serializable]
    public class CyberString : IEnumerable<string> {
        [SerializeField]
        private string[] strings;

        public string[] Strings { get { return strings; } }

        public static implicit operator string(CyberString cyberString) {
            string retVal = string.Empty;

            foreach (string substring in cyberString.strings) {
                if (!string.IsNullOrEmpty(retVal))
                    retVal += Environment.NewLine;

                retVal += substring;
            }

            return retVal;
        }

        public static explicit operator CyberString(string[] strings) {
            CyberString retVal = new CyberString();
            retVal.strings = strings;
            return retVal;
        }

        public string this[uint index] {
            get { return strings[index]; }
        }

        public IEnumerator<string> GetEnumerator() {
            return ((IEnumerable<string>)strings).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return strings.GetEnumerator();
        }
    }
}