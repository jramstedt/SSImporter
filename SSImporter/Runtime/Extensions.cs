using UnityEngine;

using System;
using System.IO;
using System.Runtime.InteropServices;

namespace SystemShock.Resource {
    public static class Extensions {
        public static T Read<T>(this BinaryReader binaryReader) {
            byte[] bytes = binaryReader.ReadBytes(Marshal.SizeOf(typeof(T)));
            GCHandle gcHandle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            T structure = (T)Marshal.PtrToStructure(gcHandle.AddrOfPinnedObject(), typeof(T));
            gcHandle.Free();

            return structure;
        }

        public static object Read(this BinaryReader binaryReader, Type type) {
            byte[] bytes = binaryReader.ReadBytes(Marshal.SizeOf(type));
            GCHandle gcHandle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            object structure = Marshal.PtrToStructure(gcHandle.AddrOfPinnedObject(), type);
            gcHandle.Free();

            return structure;
        }

        public static T Read<T>(this byte[] bytes) {
            GCHandle gcHandle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            T structure = (T)Marshal.PtrToStructure(gcHandle.AddrOfPinnedObject(), typeof(T));
            gcHandle.Free();

            return structure;
        }

        public static object Read(this byte[] bytes, Type type) {
            GCHandle gcHandle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            object structure = Marshal.PtrToStructure(gcHandle.AddrOfPinnedObject(), type);
            gcHandle.Free();

            return structure;
        }

        public static byte[] Write<T>(T obj) {
            int size = Marshal.SizeOf(obj);
            
            IntPtr ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(obj, ptr, false);

            byte[] bytes = new byte[size];
            Marshal.Copy(ptr, bytes, 0, size);

            Marshal.FreeHGlobal(ptr);
            return bytes;
        }

        public static T[] RotateRight<T>(this T[] array, uint shift) {
            T[] ret = new T[array.Length];
            for (int i = 0; i < ret.Length; ++i)
                ret[i] = array[(((i - shift) % array.Length) + array.Length) % array.Length];

            return ret;
        }

        public static float ReadFixed1616(this BinaryReader binaryReader) {
            return binaryReader.ReadInt32() / 65536f;
        }

        public static void Fill(this Texture2D texture, Color32 color) {
            Color32[] pixels = texture.GetPixels32();
            for (int i = 0; i < pixels.Length; ++i)
                pixels[i] = color;
            texture.SetPixels32(pixels);
        }

        public static Vector2 GetSize(this Texture texture) {
            return new Vector2(texture.width, texture.height);
        }

        public static void PrintBinary(uint bin) {
            Debug.Log(Convert.ToString(bin, 2).PadLeft(32, '0'));
        }

        public static void PrintBinary(int bin) {
            Debug.Log(Convert.ToString(bin, 2).PadLeft(32, '0'));
        }

        public static void PrintBinary(ulong bin) {
            Debug.Log(Convert.ToString((long)bin, 2).PadLeft(64, '0'));
        }

        public static void PrintBinary(long bin) {
            Debug.Log(Convert.ToString(bin, 2).PadLeft(64, '0'));
        }
    }
}