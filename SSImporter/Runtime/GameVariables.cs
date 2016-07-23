using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

using SystemShock.Object;

namespace SystemShock.Gameplay {
    public class GameVariables : AbstractGameController<GameVariables>, IDictionary<ushort, ushort> {
        private Dictionary<ushort, ushort> variableDictionary = new Dictionary<ushort, ushort>();

        private const ushort INTERNAL_VARIABLEMASK = 0x1FFF;

        public const ushort VARIABLEMASK = 0x01FF;

        public const ushort ACCUM = 0x1000;
        // 0x2000 Unknown
        public const ushort LESSTHAN = 0x4000;
        public const ushort INVERT = 0x8000;

        [NonSerialized] private Hacker hacker;

        public Hacker Hacker {
            get { return hacker ?? (hacker = GameObject.FindObjectOfType<Hacker>()); }
        }

        private void Start() {
            Add(20497, 255); // Medical shodan security
        }
        
        public ushort this[ushort key] {
            get { return variableDictionary[(ushort)(key & INTERNAL_VARIABLEMASK)]; }
            set { variableDictionary[(ushort)(key & INTERNAL_VARIABLEMASK)] = value; }
        }

        public int Count {
            get { return variableDictionary.Count; }
        }

        public bool IsReadOnly {
            get { return false; }
        }

        public ICollection<ushort> Keys {
            get { return variableDictionary.Keys; }
        }

        public ICollection<ushort> Values {
            get { return variableDictionary.Values; }
        }

        public void Add(KeyValuePair<ushort, ushort> item) {
            Add(item.Key, item.Value);
        }

        public void Add(ushort key, ushort value) {
            variableDictionary.Add((ushort)(key & INTERNAL_VARIABLEMASK), value);
        }

        public void Clear() {
            variableDictionary.Clear();
        }

        public bool Contains(KeyValuePair<ushort, ushort> item) {
            return ContainsKey(item.Key);
        }

        public bool ContainsKey(ushort key) {
            return variableDictionary.ContainsKey((ushort)(key & INTERNAL_VARIABLEMASK));
        }

        public void CopyTo(KeyValuePair<ushort, ushort>[] array, int arrayIndex) {
            IEnumerator<KeyValuePair<ushort, ushort>> enumerator = GetEnumerator();

            for (int i = 0; i < Count; ++i) {
                array[arrayIndex + i] = enumerator.Current;
                enumerator.MoveNext();
            }
        }

        public IEnumerator<KeyValuePair<ushort, ushort>> GetEnumerator() {
            return variableDictionary.GetEnumerator();
        }

        public bool Remove(KeyValuePair<ushort, ushort> item) {
            return Remove(item.Key);
        }

        public bool Remove(ushort key) {
            return variableDictionary.Remove((ushort)(key & INTERNAL_VARIABLEMASK));
        }

        public bool TryGetValue(ushort key, out ushort value) {
            return variableDictionary.TryGetValue((ushort)(key & INTERNAL_VARIABLEMASK), out value);
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return variableDictionary.GetEnumerator();
        }
    }
}