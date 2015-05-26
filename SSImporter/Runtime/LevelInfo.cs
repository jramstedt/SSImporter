using UnityEngine;

using System;
using System.Collections.Generic;

using SystemShock.Object;

namespace SystemShock {
    public class LevelInfo : MonoBehaviour, ISerializationCallbackReceiver {
        public float HeightFactor;
        public ushort[] TextureMap;
        public Camera[] SurveillanceCamera;
        public Dictionary<uint, SystemShockObject> Objects = new Dictionary<uint, SystemShockObject>();
        public TextScreenRenderer TextScreenRenderer;

        [SerializeField, HideInInspector]
        private List<SSKvp> serializedObjectsDictionary;

        public void OnAfterDeserialize() {
            Objects = new Dictionary<uint, SystemShockObject>();
            foreach (SSKvp kvp in serializedObjectsDictionary)
                Objects.Add(kvp.Key, kvp.Value);
        }

        public void OnBeforeSerialize() {
            serializedObjectsDictionary = new List<SSKvp>();
            foreach (KeyValuePair<uint, SystemShockObject> kvp in Objects)
                serializedObjectsDictionary.Add(new SSKvp(kvp));
        }

        [Serializable]
        private struct SSKvp {
            public uint Key;
            public SystemShockObject Value;

            public SSKvp(KeyValuePair<uint, SystemShockObject> kvp) {
                Key = kvp.Key;
                Value = kvp.Value;
            }
        }
    }
}