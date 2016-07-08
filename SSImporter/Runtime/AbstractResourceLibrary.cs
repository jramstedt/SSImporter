using System;
using UnityEngine;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SystemShock.Resource {
    public abstract class AbstractResourceLibrary<K, T> : ScriptableObject
        where K : struct
        where T : class {

        [SerializeField, HideInInspector]
        protected List<K> IndexMap;

        [SerializeField]
        protected List<T> Resources;

        public AbstractResourceLibrary() {
            IndexMap = new List<K>();
            Resources = new List<T>();
        }

#if UNITY_EDITOR
        public virtual void AddResource(K identifier, T resource) {
            if (IndexMap.Contains(identifier))
                throw new ArgumentException(string.Format(@"Resource {0} already set.", identifier));

            IndexMap.Add(identifier);
            Resources.Add(resource);
        }
#endif

        public virtual T GetResource(K identifier) {
            int index = IndexMap.IndexOf(identifier);
            return index < 0 ? null : Resources[index];
        }

        public virtual K GetIdentifier(T resource) {
            return IndexMap[Resources.IndexOf(resource)];
        }

        public virtual ReadOnlyCollection<K> GetChunkIds() {
            return IndexMap.AsReadOnly();
        }

        public virtual ReadOnlyCollection<T> GetResources() {
            return Resources.AsReadOnly();
        }
    }
}