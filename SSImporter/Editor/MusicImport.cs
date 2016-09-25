using UnityEngine;
using UnityEditor;

using System.IO;
using System;
using System.Runtime.InteropServices;
using System.Text;

using SystemShock.Resource;

public class MusicImport : ScriptableObject {
    [MenuItem("Assets/System Shock/12. Import Music", false, 1012)]
    public static void Init() {
        CreateMusicAssets();
    }

    [MenuItem("Assets/System Shock/12. Import Music", true)]
    public static bool Validate() {
        return PlayerPrefs.HasKey(@"SSHOCKRES");
    }

    private static void CreateMusicAssets() {
        string filePath = PlayerPrefs.GetString(@"SSHOCKRES");

        string soundPath = filePath + @"\SOUND\";

        if (!Directory.Exists(soundPath))
            return;

        string[] dynamicDataFilePaths = Directory.GetFiles(soundPath, "*.DAT");
        foreach (string dataFilePath in dynamicDataFilePaths) {
            Debug.Log(dataFilePath);

            using (FileStream fs = new FileStream(dataFilePath, FileMode.Open)) {
                BinaryReader br = new BinaryReader(fs, Encoding.ASCII);

                ThemeDescriptor themeDescriptor = br.Read<ThemeDescriptor>();
                Debug.Log(themeDescriptor);

                for (int sequenceIndex = 0; sequenceIndex < themeDescriptor.SequenceCount; ++sequenceIndex) {
                    SequenceDescriptor sequenceDescriptor = br.Read<SequenceDescriptor>();
                    Debug.Log(sequenceDescriptor);
                }
            }

            string binFilePath = Path.ChangeExtension(dataFilePath, "BIN");

            Debug.Log(binFilePath);
            using (FileStream fs = new FileStream(binFilePath, FileMode.Open)) {
                BinaryReader br = new BinaryReader(fs, Encoding.ASCII);

                TrackDescriptor trackDescriptor = br.Read<TrackDescriptor>();
                Debug.Log(trackDescriptor);

            }
        }
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ThemeDescriptor {
        public byte SequenceCount;
        public byte Unknown;

        public override string ToString() {
            return string.Format(@"SequenceCount = {0}, Unknown = {1}", SequenceCount, Unknown);
        }
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SequenceDescriptor {
        public byte Unknown1;
        public byte Unknown2;
        public byte Unknown3;
        public byte Unknown4;
        public byte Unknown5;
        public byte DataCount;

        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 10)]
        public byte[] Data;

        public override string ToString() {
            return string.Format(@"Unknown1 = {0}, Unknown2 = {1}, Unknown3 = {2}, Unknown4 = {3}, Unknown5 = {4}, DataCount = {5}, Data = {6}", Unknown1, Unknown2, Unknown3, Unknown4, Unknown5, DataCount, Extensions.ByteArrayToString(Data));
        }
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TrackDescriptor {
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] Quiet;

        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] Tension;

        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] Action;

        public byte Menu;

        public byte Unknown;

        public byte Unknown2;

        public byte Death;

        public byte Resurrection;

        public byte Unknown3;

        public byte Unknown4;

        public byte Unknown5;

        public byte Unknown6;

        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 32)]
        public TrackEntry[] Tracks;

        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 43)]
        public byte[] Unknown7;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TrackEntry {
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 10)]
        public byte[] Sequences;
    }
}