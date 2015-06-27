using UnityEngine;
using UnityEditor;

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using SystemShock.Object;
using SystemShock.Resource;

namespace SSImporter.Resource {
    public class SoundEffectImport {
        [MenuItem("Assets/System Shock/11. Import Sound Effects", false, 1011)]
        public static void Init() {
            CreateSoundEffectAssets();
        }

        [MenuItem("Assets/System Shock/11. Import Sound Effects", true)]
        public static bool Validate() {
            return PlayerPrefs.HasKey(@"SSHOCKRES");
        }

        private static void CreateSoundEffectAssets()
        {
            string filePath = PlayerPrefs.GetString(@"SSHOCKRES");

            string resourcePath = filePath + @"\DATA\digifx.res";

            if (!File.Exists(resourcePath))
                return;

            ResourceFile soundEffectResource = new ResourceFile(resourcePath);

            if (!Directory.Exists(@"Assets/SystemShock/" + @"soundeffects.res"))
                AssetDatabase.CreateFolder(@"Assets/SystemShock", @"soundeffects.res");

            try {
                AssetDatabase.StartAssetEditing();

                Dictionary<uint, SoundEffectSet> soundDictionary = new Dictionary<uint, SoundEffectSet>();
                foreach (KnownChunkId chunkId in soundEffectResource.GetChunkList())
                {
                    SoundEffectSet sfx = soundEffectResource.ReadSoundEffects(chunkId);  
                    soundDictionary.Add((uint)chunkId, sfx);

                    ConvertToWave((uint)chunkId, soundDictionary[(uint)chunkId], "soundeffects.res");
                }
                
            } finally {
                AssetDatabase.StopAssetEditing();
                EditorApplication.SaveAssets();
            }

            AssetDatabase.Refresh();
        }

        private static void ConvertToWave(uint chunkId, SoundEffectSet sfx, string libraryAssetPath)
        {
            int filesize = 0;

            byte[] wave_header = new byte[44];
            byte[] wave_data = new byte[wave_header.Length + sfx.Data.Length - 0x20]; //0x20 Header size
            WriteBytes(wave_header, "RIFF", 0, 4);
            WriteBytes(wave_header, "WAVE", 8, 4);
            WriteBytes(wave_header, "fmt ", 12, 4);
            WriteBytes(wave_header, (int)16, 16);
            WriteBytes(wave_header, (ushort)1, 20);
            WriteBytes(wave_header, (ushort)1, 22);
            WriteBytes(wave_header, (int)sfx.SampleRate, 24);
            WriteBytes(wave_header, (int)((sfx.SampleRate * 8 * 1) / 8), 28);
            WriteBytes(wave_header, (ushort)1, 32);
            WriteBytes(wave_header, (ushort)8, 34);
            WriteBytes(wave_header, "data", 36, 4);

            int channels = 1;
            int bitsPerSample = 8;
            int samples = sfx.Data.Length - 0x20;
            filesize = samples * channels * bitsPerSample / 8;

            WriteBytes(wave_header, (int)(filesize - 8), 4);
            WriteBytes(wave_header, (int)(filesize - 44), 40);

            Array.Copy(wave_header, 0, wave_data, 0, wave_header.Length);
            Array.Copy(sfx.Data, 0x20, wave_data, wave_header.Length, samples);

            Debug.Log(String.Format("converting sfx {0}", chunkId));


            File.WriteAllBytes(Application.dataPath + "/SystemShock/" + libraryAssetPath + "/" + chunkId.ToString() + ".wav", wave_data);
        }

        private static void WriteBytes(byte[] array, string str, int offset, int length)
        {
            for (int i = 0; i < length; i++)
                array[offset + i] = (byte)str[i];
        }

        private static void WriteBytes(byte[] array, ushort value, int offset)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            Array.Copy(bytes, 0, array, offset, bytes.Length);
        }

        private static void WriteBytes(byte[] array, int value, int offset)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            Array.Copy(bytes, 0, array, offset, bytes.Length);
        }

    }
}
