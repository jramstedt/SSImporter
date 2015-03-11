using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

namespace SystemShock.Resource {
    public class StringLibrary : AbstractResourceLibrary<StringLibrary> {

        [SerializeField]
        private CyberString[] strings;

        public CyberString[] Strings { get { return strings; } }

        [SerializeField]
        private uint[] chunkIds;

        public uint[] ChunkIds { get { return chunkIds; } }

        public StringLibrary() {

        }

        public void SetStrings(Dictionary<uint, string[]> stringDictionary) {
            chunkIds = new uint[stringDictionary.Keys.Count];
            stringDictionary.Keys.CopyTo(chunkIds, 0);

            strings = new CyberString[stringDictionary.Values.Count];
            for (int i = 0; i < strings.Length; ++i)
                strings[i] = (CyberString)stringDictionary[chunkIds[i]];
        }

        public CyberString GetStrings(KnownChunkId chunkId) {
            return strings[Array.IndexOf(chunkIds, (uint)chunkId)];
        }
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