using UnityEngine;

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

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
        public List<TextureAnimation> TextureAnimations;
        public Dictionary<ushort, SystemShockObject> Objects = new Dictionary<ushort, SystemShockObject>();
        public Dictionary<ushort, LoopConfiguration> LoopConfigurations = new Dictionary<ushort, LoopConfiguration>();
        public TextScreenRenderer TextScreenRenderer;

        public float Radiation;
        public float BioContamination;
        public float Gravity;

        [SerializeField, HideInInspector]
        private Tiles serializedTiles;

        [SerializeField]
        private List<SSKvp> serializedObjects = new List<SSKvp>();

        [SerializeField]
        private List<LoopConfiguration> serializedLoopConfigurations = new List<LoopConfiguration>();

        public void OnAfterDeserialize() {
            Objects = new Dictionary<ushort, SystemShockObject>();
            foreach (SSKvp kvp in serializedObjects)
                Objects.Add(kvp.Key, kvp.Value);

            LoopConfigurations = new Dictionary<ushort, LoopConfiguration>();
            foreach (LoopConfiguration loopConfiguration in serializedLoopConfigurations)
                LoopConfigurations.Add(loopConfiguration.ObjectId, loopConfiguration);

            Tile = new GameObject[serializedTiles.Width, serializedTiles.Height];
            for (int i = 0; i < serializedTiles.Tile.Length; ++i)
                Tile[i / serializedTiles.Width, i % serializedTiles.Width] = serializedTiles.Tile[i];
        }

        public void OnBeforeSerialize() {
            serializedObjects.Clear();
            foreach (KeyValuePair<ushort, SystemShockObject> kvp in Objects)
                serializedObjects.Add(new SSKvp(kvp));

            serializedLoopConfigurations.Clear();
            foreach (KeyValuePair<ushort, LoopConfiguration> kvp in LoopConfigurations)
                serializedLoopConfigurations.Add(kvp.Value);

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
            public ushort Key;
            public SystemShockObject Value;

            public SSKvp(KeyValuePair<ushort, SystemShockObject> kvp) {
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

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TextureAnimation {
        public ushort FrameTime;
        public ushort CurrentFrameTime;
        public byte CurrentFrameIndex;
        public byte FrameCount;
        public byte IsPingPong;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct LoopConfiguration {
        public ushort ObjectId;
        public byte LoopWrapMode;

        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 10)]
        public byte[] Unknown;

        public ushort Unknown2;
    }
}