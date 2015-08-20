using UnityEngine;

using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;

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
        public SurveillanceCamera[] SurveillanceCameras;
        public Tile[,] Tiles;
        public List<TextureAnimation> TextureAnimations;
        public IClassData[] ClassDataTemplates;
        public Dictionary<ushort, SystemShockObject> Objects = new Dictionary<ushort, SystemShockObject>();
        public Dictionary<ushort, LoopConfiguration> LoopConfigurations = new Dictionary<ushort, LoopConfiguration>();
        public TextScreenRenderer TextScreenRenderer;

        public float Radiation;
        public float BioContamination;
        public float Gravity;

        [SerializeField, HideInInspector]
        private SerializedTiles serializedTiles;

        [SerializeField, HideInInspector]
        private byte[] serializedClassDataTemplates;

        [SerializeField]
        private List<SSKvp> serializedObjects = new List<SSKvp>();

        [SerializeField, HideInInspector]
        private List<LoopConfiguration> serializedLoopConfigurations = new List<LoopConfiguration>();

        public void OnAfterDeserialize() {
            Objects = new Dictionary<ushort, SystemShockObject>();
            foreach (SSKvp kvp in serializedObjects)
                Objects.Add(kvp.Key, kvp.Value);

            LoopConfigurations = new Dictionary<ushort, LoopConfiguration>();
            foreach (LoopConfiguration loopConfiguration in serializedLoopConfigurations)
                LoopConfigurations.Add(loopConfiguration.ObjectId, loopConfiguration);

            Tiles = new Tile[serializedTiles.Width, serializedTiles.Height];
            for (int i = 0; i < serializedTiles.Tiles.Length; ++i)
                Tiles[i / serializedTiles.Width, i % serializedTiles.Width] = serializedTiles.Tiles[i];

            using (MemoryStream ms = new MemoryStream(serializedClassDataTemplates)) {
                BinaryFormatter bf = new BinaryFormatter();
                ClassDataTemplates = (IClassData[])bf.Deserialize(ms);
            }
        }

        public void OnBeforeSerialize() {
            serializedObjects.Clear();
            foreach (KeyValuePair<ushort, SystemShockObject> kvp in Objects)
                serializedObjects.Add(new SSKvp(kvp));

            serializedLoopConfigurations.Clear();
            foreach (KeyValuePair<ushort, LoopConfiguration> kvp in LoopConfigurations)
                serializedLoopConfigurations.Add(kvp.Value);

            Tile[] serializedTilesArray = new Tile[Tiles.Length];

            int index = 0;
            foreach (Tile tile in Tiles)
                serializedTilesArray[index++] = tile;

            serializedTiles = new SerializedTiles() {
                Width = Tiles.GetLength(0),
                Height = Tiles.GetLength(1),
                Tiles = serializedTilesArray
            };

            using (MemoryStream ms = new MemoryStream()) {
                BinaryFormatter bf = new BinaryFormatter();
                bf.Serialize(ms, ClassDataTemplates);
                serializedClassDataTemplates = ms.ToArray();
            }
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
        private struct SerializedTiles {
            public int Width;
            public int Height;
            public Tile[] Tiles;
        }

        [Serializable]
        public class Tile {
            public int Floor;
            public int Ceiling;

            public GameObject GameObject;
        }

        [Serializable]
        public class SurveillanceCamera {
            public Camera Camera;
            public SystemShockObject DeathwatchObject;
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

        public ushort Frametime;
    }
}