using System;
using UnityEngine;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SystemShock.Resource {
    public abstract class AbstractResourceLibrary : ScriptableObject {
        public static T GetLibrary<T>() where T : AbstractResourceLibrary<T> {
            return AbstractResourceLibrary<T>.GetLibrary();
        }
    }

    public abstract class AbstractResourceLibrary<T> : AbstractResourceLibrary where T : AbstractResourceLibrary<T> {
        private static T instance;

        protected virtual void OnEnable() { instance = (T)this; }

        protected virtual void OnDisable() { instance = null; }

        public static T GetLibrary() {
            if (instance == null)
                ResourceLibrary.GetController(); //ResourceLibrary controller should contain all libraries. Libraries are initialized there.

            return instance;
        }
    }

    public abstract class AbstractResourceLibrary<L, K, T> : AbstractResourceLibrary<L>
        where L : AbstractResourceLibrary<L, K, T>
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