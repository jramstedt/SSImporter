using System.Runtime.InteropServices;

namespace SS.ObjectProperties {
  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct ThreeOf<T> where T : unmanaged {
    public T x;
    public T y;
    public T z;

    unsafe public T this[int index] {
      get {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        if ((uint)index >= 3)
          throw new global::System.ArgumentException("index must be between[0...3]");
#endif
        fixed (T* array = &x) { return array[index]; }
      }
      set {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        if ((uint)index >= 3)
          throw new global::System.ArgumentException("index must be between[0...3]");
#endif
        fixed (T* array = &x) { array[index] = value; }
      }
    }
  }

  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct FourOf<T> where T : unmanaged {
    public T x;
    public T y;
    public T z;
    public T w;

    unsafe public T this[int index] {
      get {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        if ((uint)index >= 4)
          throw new global::System.ArgumentException("index must be between[0...3]");
#endif
        fixed (T* array = &x) { return array[index]; }
      }
      set {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        if ((uint)index >= 4)
          throw new global::System.ArgumentException("index must be between[0...3]");
#endif
        fixed (T* array = &x) { array[index] = value; }
      }
    }
  }

  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct SixOf<T> where T : unmanaged {
    public T i;
    public T j;
    public T k;
    public T l;
    public T m;
    public T n;

    unsafe public T this[int index] {
      get {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        if ((uint)index >= 6)
          throw new global::System.ArgumentException("index must be between[0...5]");
#endif
        fixed (T* array = &i) { return array[index]; }
      }
      set {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        if ((uint)index >= 6)
          throw new global::System.ArgumentException("index must be between[0...5]");
#endif
        fixed (T* array = &i) { array[index] = value; }
      }
    }
  }

  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct EightOf<T> where T : unmanaged {
    public T i;
    public T j;
    public T k;
    public T l;
    public T m;
    public T n;
    public T o;
    public T p;

    unsafe public T this[int index] {
      get {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        if ((uint)index >= 8)
          throw new global::System.ArgumentException("index must be between[0...7]");
#endif
        fixed (T* array = &i) { return array[index]; }
      }
      set {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        if ((uint)index >= 8)
          throw new global::System.ArgumentException("index must be between[0...7]");
#endif
        fixed (T* array = &i) { array[index] = value; }
      }
    }
  }
}
