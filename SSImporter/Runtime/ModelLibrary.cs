using UnityEngine;

using System;
using System.Collections;
using System.Collections.Generic;

namespace SystemShock.Resource {
    public class ModelLibrary : AbstractResourceLibrary<ModelLibrary> {
        [SerializeField]
        private List<GameObject> modelPrefabs;

        [SerializeField, HideInInspector]
        private List<ushort> indexMap;

        public ModelLibrary() {
            modelPrefabs = new List<GameObject>();
            indexMap = new List<ushort>();
        }

        public void AddModel(ushort modelId, GameObject modelPrefab) {
            if (indexMap.Contains(modelId))
                throw new ArgumentException(string.Format(@"Model {0} already set.", modelId));

            indexMap.Add(modelId);
            modelPrefabs.Add(modelPrefab);
        }

        public GameObject GetModel(ushort modelId) {
            return modelPrefabs[indexMap.IndexOf(modelId)];
        }
    }
}
