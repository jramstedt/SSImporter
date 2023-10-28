using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Unity.Collections;

namespace SS {
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

    public static T Read<T>(this byte[] bytes, int offset = 0) {
      GCHandle gcHandle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
      T structure = (T)Marshal.PtrToStructure(IntPtr.Add(gcHandle.AddrOfPinnedObject(), offset), typeof(T));
      gcHandle.Free();

      return structure;
    }

    public static object Read(this byte[] bytes, Type type, int offset = 0) {
      GCHandle gcHandle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
      object structure = Marshal.PtrToStructure(IntPtr.Add(gcHandle.AddrOfPinnedObject(), offset), type);
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

    public static T[] RotateRight<T>(this T[] array, int shift) {
      T[] ret = new T[array.Length];
      for (int i = 0; i < ret.Length; ++i)
        ret[i] = array[(((i - shift) % array.Length) + array.Length) % array.Length];

      return ret;
    }

    public static float ReadFixed1616(this BinaryReader binaryReader) {
      return binaryReader.ReadInt32() / 65536f;
    }

    public static string ByteArrayToString(byte[] ba) {
      StringBuilder hex = new(ba.Length * 2);
      foreach (byte b in ba)
        hex.AppendFormat("{0:x2}:", b);
      return hex.ToString();
    }

    public static unsafe void Deconstruct<TKey, TValue>(this KVPair<TKey, TValue> pair, out TKey key, out TValue value)
      where TKey : unmanaged, IEquatable<TKey>
      where TValue : unmanaged
    {
      pair.GetKeyValue(out key, out value);
    }
  }
}