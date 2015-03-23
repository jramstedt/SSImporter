using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace SystemShock.Resource {
    public class TextureLibrary : AbstractResourceLibrary<TextureLibrary> {
        [SerializeField]
        [HideInInspector]
        private List<Material> materials;

        [SerializeField]
        public List<TextureProperties> textureProperties;

        [SerializeField]
        [HideInInspector]
        private List<ushort> indexMap;

        public TextureLibrary() {
            materials = new List<Material>();
            textureProperties = new List<TextureProperties>();
            indexMap = new List<ushort>();
        }

        public void SetTexture(ushort textureId, Material material, TextureProperties textureProperties) {
            if (indexMap.Contains(textureId))
                throw new ArgumentException(string.Format(@"Texture {0} already set.", textureId));

            indexMap.Add(textureId);
            materials.Add(material);
            this.textureProperties.Add(textureProperties);
        }

        public Material GetMaterial(ushort textureId) {
            int index = indexMap.IndexOf(textureId);
            return materials[index];
        }

        public ushort GetTextureId(Material material) {
            return indexMap[materials.IndexOf(material)];
        }

        private TextureProperties GetTextureProperties(ushort textureId) {
            return textureProperties[indexMap.IndexOf(textureId)];
        }
    }


    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TextureProperties {

        [Flags]
        public enum StartfieldControlMask : byte {
            OverrideAll = 0x01,
            OverrideBlack = 0x02
        }

        public byte IndexA;
        public byte IndexB;
        public uint Unknown1;
        public byte Climbable;

        public byte Unknown2;
        public byte StarfieldControl;
        public byte AnimationGroup;
        public byte AnimationIndex;

        public override string ToString() {
            return string.Format(@"IndexA = {0}, IndexB = {1}, Unknown1 = {2:X8}, Climbable = {3}, Unknown2 = {4}, StarfieldControl = {5}, AnimationGroup = {6}, AnimationIndex = {7}",
                                 IndexA, IndexB, Unknown1, Climbable, Unknown2, StarfieldControl, AnimationGroup, AnimationIndex);
        }
    }
}
