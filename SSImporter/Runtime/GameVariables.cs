using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

using SystemShock.Object;

namespace SystemShock.Resource {
    public class GameVariables : AbstractGameController<GameVariables>, IDictionary<ushort, ushort> {
        private Dictionary<ushort, ushort> variableDictionary = new Dictionary<ushort, ushort>();

        public ushort this[ushort key] {
            get { return variableDictionary[key]; }
            set { variableDictionary[key] = value; }
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
            variableDictionary.Add(key, value);
        }

        public void Clear() {
            variableDictionary.Clear();
        }

        public bool Contains(KeyValuePair<ushort, ushort> item) {
            return ContainsKey(item.Key);
        }

        public bool ContainsKey(ushort key) {
            return variableDictionary.ContainsKey(key);
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
            return variableDictionary.Remove(key);
        }

        public bool TryGetValue(ushort key, out ushort value) {
            return variableDictionary.TryGetValue(key, out value);
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return variableDictionary.GetEnumerator();
        }
    }
}