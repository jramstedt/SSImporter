using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace SystemShock.Resource {
    public class TextureLibrary : AbstractResourceLibrary<TextureLibrary> {
        [SerializeField]
        private List<Material> materials;

        [SerializeField]
        private List<TextureProperties> textureProperties;

        [SerializeField, HideInInspector]
        private List<ushort> indexMap;

        public TextureLibrary() {
            materials = new List<Material>();
            indexMap = new List<ushort>();
            textureProperties = new List<TextureProperties>();
        }

        public void AddTexture(ushort textureId, Material material, TextureProperties textureProperties) {
            if (indexMap.Contains(textureId))
                throw new ArgumentException(string.Format(@"Texture {0} already set.", textureId));

            indexMap.Add(textureId);
            materials.Add(material);
            this.textureProperties.Add(textureProperties);
        }

        public Material GetMaterial(ushort textureId) {
            int index = indexMap.IndexOf(textureId);
            return index < 0 ? null : materials[index];
        }

        public ushort GetTextureId(Material material) {
            return indexMap[materials.IndexOf(material)];
        }

        public TextureProperties GetTextureProperties(ushort textureId) {
            return textureProperties[indexMap.IndexOf(textureId)];
        }

        public TextureProperties GetTextureProperties(Material material) {
            return textureProperties[materials.IndexOf(material)];
        }

        public Material[] GetMaterialAnimation(ushort startTexureId, ushort count) {
            Material[] frames = new Material[count];
            for (ushort i = 0; i < count; ++i)
                frames[i] = GetMaterial((ushort)(startTexureId + i));

            return frames;
        }
        
        public Material[] GetMaterialAnimation(byte animationGroup) {
            SortedDictionary<byte, Material> animationFrames = new SortedDictionary<byte, Material>();
            for(int i = 0; i < textureProperties.Count; ++i) {
                TextureProperties properties = textureProperties[i];
                if(properties.AnimationGroup == animationGroup)
                    animationFrames.Add(properties.AnimationIndex, materials[i]);
            }

            return animationFrames.Values.ToArray();
        }
        
        public Material[] GetMaterialAnimation(Material material) {
            return GetMaterialAnimation(textureProperties[materials.IndexOf(material)].AnimationGroup);
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
