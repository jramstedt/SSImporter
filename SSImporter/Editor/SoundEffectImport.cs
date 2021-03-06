﻿using UnityEngine;
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
        [MenuItem("Assets/System Shock/9. Import Sound Effects", false, 1009)]
        public static void Init() {
            CreateSoundEffectAssets();
        }

        [MenuItem("Assets/System Shock/9. Import Sound Effects", true)]
        public static bool Validate() {
            return PlayerPrefs.HasKey(@"SSHOCKRES");
        }

        private static void CreateSoundEffectAssets() {
            string filePath = PlayerPrefs.GetString(@"SSHOCKRES");

            string resourcePath = filePath + @"\DATA\digifx.res";

            if (!File.Exists(resourcePath))
                return;

            ResourceFile soundEffectResource = new ResourceFile(resourcePath);

            try {
                if (!Directory.Exists(Application.dataPath + @"/SystemShock"))
                    AssetDatabase.CreateFolder(@"Assets", @"SystemShock");

                if (!Directory.Exists(Application.dataPath + @"/SystemShock/digifx.res"))
                    AssetDatabase.CreateFolder(@"Assets/SystemShock", @"digifx.res");

                SoundLibrary soundLibrary = ScriptableObject.CreateInstance<SoundLibrary>();
                AssetDatabase.CreateAsset(soundLibrary, @"Assets/SystemShock/digifx.res.asset");

                float progress = 0f;
                float progressStep = 1f / soundEffectResource.GetChunkList().Count;

                foreach (KnownChunkId chunkId in soundEffectResource.GetChunkList()) {
                    EditorUtility.DisplayProgressBar(@"Import Sound Effects", (uint)chunkId + @".wav", progress);

                    SoundEffectSet sfx = soundEffectResource.ReadSoundEffect(chunkId);
                    ConvertToWave(chunkId, sfx, "digifx.res");

                    string assetPath = string.Format(@"Assets/SystemShock/digifx.res/{0}.wav", (uint)chunkId);

                    AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

                    soundLibrary.AddResource(chunkId, AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath));

                    progress += progressStep;
                }

                EditorUtility.SetDirty(soundLibrary);

                ResourceLibrary.GetController().AddLibrary(soundLibrary);
            } finally {
                EditorUtility.ClearProgressBar();
                AssetDatabase.SaveAssets();
            }

            AssetDatabase.Refresh();
        }

        private static void ConvertToWave(KnownChunkId chunkId, SoundEffectSet sfx, string libraryAssetPath) {
            using (MemoryStream ms = new MemoryStream()) {
                BinaryWriter msbw = new BinaryWriter(ms, Encoding.ASCII);
                msbw.Write(@"RIFF".ToCharArray());
                msbw.Write((uint)0);
                msbw.Write(@"WAVE".ToCharArray());

                msbw.Write(@"fmt ".ToCharArray());
                msbw.Write((uint)16); // fmt chunk data length
                msbw.Write((ushort)1);
                msbw.Write((ushort)sfx.ChannelCount);
                msbw.Write(sfx.SampleRate);
                msbw.Write((uint)(sfx.ChannelCount * sfx.SampleRate * Mathf.Round(sfx.BitsPerSample / 8f)));
                msbw.Write((ushort)(sfx.ChannelCount * Mathf.Round(sfx.BitsPerSample / 8f)));
                msbw.Write((ushort)sfx.BitsPerSample);
                msbw.Write(@"data".ToCharArray());
                msbw.Write((uint)sfx.Data.Length);
                msbw.Write(sfx.Data);

                if((sfx.Data.Length & 1) == 1) // If lenght is odd, add padding byte
                    msbw.Write((byte)0);

                msbw.Flush();

                msbw.Seek(4, SeekOrigin.Begin);
                msbw.Write((uint)(ms.Length - 8)); // 4 + 4

                msbw.Flush();

                using (FileStream file = File.Create(Application.dataPath + "/SystemShock/" + libraryAssetPath + "/" + (uint)chunkId + ".wav")) {
                    ms.Seek(0, SeekOrigin.Begin);
                    ms.WriteTo(file);
                }
            }
        }
    }
}
