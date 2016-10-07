using UnityEngine;

using System;
using System.IO;
using System.Collections;
using System.Runtime.InteropServices;

namespace SSImporter.Resource {
    public enum ContentType : byte {
        Palette,
        Text,
        Bitmap,
        Font,
        Video,
        Sound = 0x07,
        Model = 0x0F,
        MOVI = 0x11,
        Map = 0x30
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PaletteChunk {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256 * 3)]
        private byte[] palette; // RGB

        public PaletteChunk(PaletteChunk copy) {
            palette = new byte[256 * 3];
            for (int i = 0; i < palette.Length; ++i)
                palette[i] = copy.palette[i];
        }

        public Color32 this[int key] {
            get {
                bool opaque = key != 0;

                key = key * 3;

                if (key > palette.Length)
                    throw new IndexOutOfRangeException();

                byte r = palette[key];
                byte g = palette[++key];
                byte b = palette[++key];

                return new Color32(r, g, b, opaque ? (byte)0xFF : (byte)0x00);
            }

            set {
                key = key * 3;

                if (key > palette.Length)
                    throw new IndexOutOfRangeException();

                palette[key] = value.r;
                palette[++key] = value.g;
                palette[++key] = value.b;
            }
        }

        public Color32[] Color32s {
            get {
                Color32[] colors = new Color32[256];
                for(int i = 0; i < colors.Length; ++i)
                    colors[i] = this[i];

                return colors;
            }
        }
        
        public Color[] Colors {
            get {
                Color[] colors = new Color[256];
                for (int i = 0; i < colors.Length; ++i)
                    colors[i] = (Color)this[i];

                return colors;
            }
        }
        
        public static PaletteChunk Grayscale() {
            PaletteChunk ret = new PaletteChunk();
            ret.palette = new byte[256 * 3];

            for (int i = 0; i < 256; ++i) {
                int key = i * 3;
                ret.palette[key] = ret.palette[++key] = ret.palette[++key] = (byte)(i & 0xFF);
            }

            return ret;
        }

        public PaletteChunk RotateSlots(int steps) {
            short[] rotatingSlots = new short[] {
                0x0305,
                0x0803,
                0x0B05,
                0x1004,
                0x1404,
                0x1804,
                0x1C04
            };

            PaletteChunk ret = new PaletteChunk(this);
            for (int slotIndex = 0; slotIndex < rotatingSlots.Length; ++slotIndex) {
                int count = rotatingSlots[slotIndex] & 0xFF;
                int startIndex = rotatingSlots[slotIndex] >> 8;

                for (int i = 0; i < count; ++i) {
                    int newIndex = (i + steps) % count;
                    ret[startIndex + i] = this[startIndex + newIndex];
                }
            }
            
            return ret;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Bitmap {
        public int Unknown1;
        public BitmapType BitmapType;
        public ushort Unknown2;
        public ushort Width;
        public ushort Height;
        public ushort Stride;
        public byte LogWidth;
        public byte LogHeight;
        public ushort PivotMinX;
        public ushort PivotMinY;
        public ushort PivotMaxX;
        public ushort PivotMaxY;
        public int PaletteOffset;

        public override string ToString() {
            return string.Format("BitmapType = {0}, Width = {1}, Height = {2}, Stride = {3}, LogWidth = {4}, LogHeight = {5}", BitmapType, Width, Height, Stride, LogWidth, LogHeight);
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct BitmapFont {
        public enum BitmapDataType : ushort {
            BlackWhite = 0x0000,
            Color = 0xCCCC
        }

        public BitmapDataType DataType;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 34)]
        public byte[] Unknown1;

        public ushort FirstAscii;
        public ushort LastAscii;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] Unknown2;

        public uint xOffset;
        public uint bitsOffset;

        public ushort Width;
        public ushort Height;

        public override string ToString() {
            string str = "C: " + DataType;

            str += " u1: " + ByteArrayToString(Unknown1);

            str += " fa: " + FirstAscii;
            str += " la: " + LastAscii;

            str += " u2: " + ByteArrayToString(Unknown2);

            str += " xo: " + xOffset;
            str += " bo: " + bitsOffset;

            str += " w: " + Width;
            str += " h: " + Height;

            return str;
        }

        public static string ByteArrayToString(byte[] ba) {
            string hex = BitConverter.ToString(ba);
            return hex.Replace("-", "");
        }
    }

    public enum BitmapType : ushort {
        Uncompressed = 0x00,
        Texture = 0x02,
        Compressed = 0x04
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SoundEffect {
        public enum BlockType : byte {
            Terminator,
            SoundData,
            SoundDataContinuation,
            Silence,
            Marker,
            Text,
            RepeatStart,
            RepeatEnd,
            ExtraInfo,
            SoundDataNew
        }

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 19)]
        private byte[] MagicIdentifier;
        private byte MagicIdentifierTerminator;
        public ushort HeaderLength;
        public ushort Version;
        public ushort VersionValidation;
    }
}
