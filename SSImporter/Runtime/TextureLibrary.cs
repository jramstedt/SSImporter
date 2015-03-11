using UnityEngine;
using UnityEditor;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace SystemShock.Resource {
    public class TextureLibrary : AbstractResourceLibrary<TextureLibrary> {
        [SerializeField]
        [HideInInspector]
        private List<string> textureGuids;

        [SerializeField]
        public List<TextureProperties> textureProperties;

        [SerializeField]
        [HideInInspector]
        private List<ushort> indexMap;

        public TextureLibrary() {
            textureGuids = new List<string>();
            textureProperties = new List<TextureProperties>();
            indexMap = new List<ushort>();
        }

        public void SetTexture(ushort textureId, string guid, TextureProperties textureProperties) {
            if (indexMap.Contains(textureId))
                throw new ArgumentException(string.Format(@"Texture {0} already set.", textureId));

            indexMap.Add(textureId);
            textureGuids.Add(guid);
            this.textureProperties.Add(textureProperties);
        }

        public string GetTextureGuid(ushort textureId) {
            int index = indexMap.IndexOf(textureId);
            return textureGuids[index];
        }

        private TextureProperties GetTextureProperties(ushort textureId) {
            return textureProperties[indexMap.IndexOf(textureId)];
        }

        public Material GetMaterial(ushort textureId) {
            Material original = AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(GetTextureGuid(textureId)), typeof(Material)) as Material;
            /*
            Material copy = new Material(original);
            copy.shaderKeywords = original.shaderKeywords;

            return copy;*/

            return original;
        }
    }


    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TextureProperties {

        [Flags]
        public enum FlagMask : uint {
            Climbable = 0x01000000
        }

        public byte Unknown1;
        public byte StarfieldControl;
        public byte AnimationGroup;
        public byte AnimationIndex;
        public byte IndexA;
        public byte IndexB;
        public byte Unknown2;
        public uint Flags;

        public override string ToString() {
            return string.Format(@"Unknown1 = {0}, StarfieldControl = {1}, AnimationGroup = {2}, AnimationIndex = {3}, IndexA = {4}, IndexB = {5}, Unknown2 = {6}, Flags = {7:X8}",
                                 Unknown1, StarfieldControl, AnimationGroup, AnimationIndex, IndexA, IndexB, Unknown2, Flags);
        }
    }
}
