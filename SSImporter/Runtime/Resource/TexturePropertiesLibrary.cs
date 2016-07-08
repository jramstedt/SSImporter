using UnityEngine;

using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace SystemShock.Resource {
    public class TexturePropertiesLibrary : AbstractResourceLibrary<ushort, TextureProperties> {
        public ushort[] GetAnimation(byte animationGroup) {
            SortedDictionary<byte, ushort> animationFrames = new SortedDictionary<byte, ushort>();
            for (int i = 0; i < Resources.Count; ++i) {
                TextureProperties properties = Resources[i];
                if (properties.AnimationGroup == animationGroup)
                    animationFrames.Add(properties.AnimationIndex, IndexMap[i]);
            }

            return animationFrames.Values.ToArray();
        }
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class TextureProperties {
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