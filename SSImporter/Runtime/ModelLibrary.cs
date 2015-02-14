using UnityEngine;

using System;
using System.Collections;
using System.Collections.Generic;

namespace SystemShock.Resource {
    public class ModelLibrary : ScriptableObject {
        [SerializeField]
        [HideInInspector]
        private List<string> modelGuids;

        [SerializeField]
        [HideInInspector]
        private List<ushort> indexMap;

        public ModelLibrary() {
            modelGuids = new List<string>();
            indexMap = new List<ushort>();
        }

        public void SetModel(ushort modelId, string guid) {
            if (indexMap.Contains(modelId))
                throw new ArgumentException(string.Format(@"Model {0} already set.", modelId));

            indexMap.Add(modelId);
            modelGuids.Add(guid);
        }

        public string GetModelGuid(ushort modelId) {
            return modelGuids[indexMap.IndexOf(modelId)];
        }
    }
}
