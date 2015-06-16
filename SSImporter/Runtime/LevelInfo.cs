using UnityEngine;

using System;
using System.Collections.Generic;

using SystemShock.Object;

namespace SystemShock {
    public class LevelInfo : MonoBehaviour, ISerializationCallbackReceiver {
        public enum LevelType {
            Normal,
            Cyberspace
        }

        public LevelType Type;
        public float HeightFactor;
        public float MapScale;
        public ushort[] TextureMap;
        public Camera[] SurveillanceCamera;
        public GameObject[,] Tile;
        public Dictionary<uint, SystemShockObject> Objects = new Dictionary<uint, SystemShockObject>();
        public TextScreenRenderer TextScreenRenderer;

        [SerializeField]
        private List<SSKvp> serializedObjectsDictionary;

        [SerializeField, HideInInspector]
        private Tiles serializedTiles;

        public void OnAfterDeserialize() {
            Objects = new Dictionary<uint, SystemShockObject>();
            foreach (SSKvp kvp in serializedObjectsDictionary)
                Objects.Add(kvp.Key, kvp.Value);

            Tile = new GameObject[serializedTiles.Width, serializedTiles.Height];
            for (int i = 0; i < serializedTiles.Tile.Length; ++i)
                Tile[i / serializedTiles.Width, i % serializedTiles.Width] = serializedTiles.Tile[i];
        }

        public void OnBeforeSerialize() {
            serializedObjectsDictionary = new List<SSKvp>();
            foreach (KeyValuePair<uint, SystemShockObject> kvp in Objects)
                serializedObjectsDictionary.Add(new SSKvp(kvp));

            GameObject[] serializedTilesArray = new GameObject[Tile.Length];
            int index = 0;

            foreach (GameObject tile in Tile)
                serializedTilesArray[index++] = tile;

            serializedTiles = new Tiles() {
                Width = Tile.GetLength(0),
                Height = Tile.GetLength(1),
                Tile = serializedTilesArray
            };
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

        [Serializable]
        private struct Tiles {
            public int Width;
            public int Height;
            public GameObject[] Tile;
        }
    }
}