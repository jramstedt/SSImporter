using System.Collections;
using System.Collections.Generic;
using SystemShock.Resource;
using UnityEngine;
using UnityEngine.UI;

namespace SystemShock.UserInterface {
    [ExecuteInEditMode, RequireComponent(typeof(Text))]
    public class ResString : MonoBehaviour {
        public KnownChunkId chunkId;
        public uint index;

#if UNITY_EDITOR
        private void Start() {
            Text text = GetComponent<Text>();
            text.text = StringLibrary.GetLibrary().GetResource(chunkId)[index];
        }

        private void Update() {
            Start();
        }
#endif
    }
}