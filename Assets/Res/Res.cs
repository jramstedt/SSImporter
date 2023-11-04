using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using static SS.Resources.ResourceFile;

namespace SS.Resources {
  public static class Res {
    public const string rootPath = @"C:\Users\Janne\Downloads\SYSTEMSHOCK-Portable-v1.2.3\RES";
    public const string dataPath = rootPath + @"\DATA\";

    private static readonly InitializationState InitState = new();

    private static readonly Dictionary<string, IResHandle<ResourceFile>> resourceFileHandles = new();

    private static readonly Dictionary<ushort, ResourceFile> resourceRecord = new();

    private static readonly Dictionary<ContentType, IResProvider[]> contentProviders = new() {
        { ContentType.Palette, new[]{ new PaletteProvider() } },
        // String
        { ContentType.Image, new[]{ new BitmapProvider() } },
        { ContentType.Font, new []{ new FontProvider() } }, 
        // Animation
        { ContentType.Voc, new[]{ new AudioClipProvider() } },
        { ContentType.Obj3D, new[]{ new MeshProvider() } },
        // Movie
        // Map
    };

    [RuntimeInitializeOnLoadMethod]
    private static async Awaitable InitAsync() {
      // TODO Settings
      var bilinear = GlobalKeyword.Create(@"_BILINEAR");
      Shader.DisableKeyword(bilinear);
      // Shader.EnableKeyword(bilinear);

      var preciseShade = GlobalKeyword.Create(@"_PRECISE_SHADE");
      // Shader.DisableKeyword(preciseShade);
      Shader.EnableKeyword(preciseShade);

      var smootShade = GlobalKeyword.Create(@"_SMOOTH_SHADE");
      Shader.DisableKeyword(smootShade);
      // Shader.EnableKeyword(smootShade);

      var resourceFileNames = new List<string> {
        "gamepal.res",

        "texture.res",
        "handart.res",
        "objart.res",
        "objart2.res",
        "objart3.res",

        "gamescr.res",
        "mfdart.res",
        "obj3d.res",
        "citmat.res",
        "digifx.res",

        "cybstrng.res",
        "sideart.res",

        "citbark.res",
        "citalog.res",

        "vidmail.res",
      };

      var resourceAwaitables = resourceFileNames.ConvertAll(fileName => Res.Open(Res.dataPath + fileName));
      foreach (var resAwaitable in resourceAwaitables)
        await resAwaitable;

      InitState.InvokeCompletionEvent();

      await Res.TestAllLoadedAsync();

      #region Load archive.dat
      Debug.Log(@"Load archive.dat");
      await SaveLoader.LoadMap(1, Res.rootPath + @"\DATA", @"ARCHIVE.DAT");
      #endregion
    }

    private static async Task TestAllLoadedAsync() {
      // Tests:
      var audioClip = await Res.Load<AudioClip>(0xC9);
      AudioSource.PlayClipAtPoint(audioClip, Vector3.zero);

      var palette = await Res.Load<Palette>(0x02BC);
      Debug.Log($"{palette}");
      
      var bitmapSet = await Res.Load<BitmapSet>(0x004D, 10);
      Debug.Log($"{bitmapSet}");

      var meshInfo = await Res.Load<MeshInfo>(0x8FC);
      Debug.Log($"{meshInfo}");
      
      var fontSet = await Res.Load<FontSet>(0x25D);
      Debug.Log($"{fontSet}");
    }

    public static IResHandle<T> Load<T>(uint id) {
      ushort resId = (ushort)(id >> 16);
      ushort blockIndex = (ushort)(id & 0xFFFF);

      return Load<T>(resId, blockIndex);
    }

    public static IResHandle<T> Load<T>(ushort resId, ushort blockIndex = 0) {
      if (InitState.IsCompleted)
        return Provide<T>(resId, blockIndex);
      else
        return new WaitInitialization<T>(InitState, () => Provide<T>(resId, blockIndex));
    }

    private static IResHandle<T> Provide<T>(ushort resId, ushort blockIndex = 0) {
      if (resourceRecord.TryGetValue(resId, out ResourceFile resourceFile)) {
        var resourceInfo = resourceFile.GetResourceInfo(resId);

        if (contentProviders.TryGetValue(resourceInfo.info.ContentType, out IResProvider[] providers)) {
          foreach (var provider in providers) {
            if (provider is IResProvider<T> matchingProvider)
              return matchingProvider.Provide(resourceFile, resourceInfo, blockIndex);
          }
        }

        Debug.LogError($"No provider found for {resourceInfo.info.ContentType} as {typeof(T)}");
      } else {
        Debug.LogError($"No resource file for {resId:X4} found");
      }

      return default;
    }

    public static IResHandle<ResourceFile> Open(string filePath) {
      if (!resourceFileHandles.TryGetValue(filePath, out IResHandle<ResourceFile> resLoader)) {
        var byteLoader = new FileByteLoader(filePath);
        resLoader = new PickResultLoader<ResourceFile, byte[]>(byteLoader, rawBytes => new ResourceFile(rawBytes));

        if (resourceFileHandles.TryAdd(filePath, resLoader)) {
          resLoader.Completed += resLoader => {
            var resFile = resLoader.Result;

            foreach (var (resId, resource) in resFile.ResourceEntries) {
              Debug.Log($"{(global::System.IO.Path.GetFileName(filePath))}: Adding {resId:X4} {resource.info.Id:X4} {resource.info.ContentType}");

              if (!resourceRecord.TryAdd(resId, resFile))
                Debug.LogWarning($"{(global::System.IO.Path.GetFileName(filePath))}: Resource record already contains {resId:X4}");
            }
          };
        }
      }

      return resLoader;
    }

    public static IResHandle<ObjectProperties> OpenObjectProperties(string filePath) {
      var byteLoader = new FileByteLoader(filePath);
      return new PickResultLoader<ObjectProperties, byte[]>(byteLoader, rawBytes => new ObjectProperties(rawBytes));
    }

    public static IResHandle<T> Open<T>(string filePath) where T : struct {
      return new FileLoader<T>(filePath);
    }

    private class WaitInitialization<T> : IResHandle<T> {
      public bool IsCompleted => realHandle?.IsCompleted ?? false;
      public ExceptionDispatchInfo Error => realHandle?.Error;
      public T Result => realHandle is null ? default : realHandle.Result;

      public event Action<IResHandle<T>> Completed;

      private IResHandle<T> realHandle;

      public WaitInitialization(InitializationState initState, Func<IResHandle<T>> provider) {
        initState.Completed += () => {
          realHandle = provider();
          realHandle.Completed += Completed;
        };
      }
    }

    private class InitializationState {
      public bool IsCompleted { get; private set; } = false;

      private Action CompletedAction;
      public event Action Completed {
        add {
          if (IsCompleted)
            value();
          else
            CompletedAction += value;
        }

        remove {
          CompletedAction -= value;
        }
      }

      internal void InvokeCompletionEvent() {
        IsCompleted = true;
        CompletedAction?.Invoke();
      }
    }
  }

  public interface IResHandle<T> {
    bool IsCompleted { get; }

    event Action<IResHandle<T>> Completed;

    ExceptionDispatchInfo Error { get; }

    T Result { get; }

    public Awaiter GetAwaiter() => new(this);


    public readonly struct Awaiter : ICriticalNotifyCompletion {
      private readonly IResHandle<T> resHandle;

      public Awaiter(IResHandle<T> resHandle) { this.resHandle = resHandle; }

      public readonly void OnCompleted(Action continuation) {
        SynchronizationContext context = SynchronizationContext.Current;
        if (context != null)
          resHandle.Completed += _ => context.Post(state => continuation(), null);
        else
          resHandle.Completed += _ => continuation();
      }

      public void UnsafeOnCompleted(Action continuation) {
        resHandle.Completed += _ => continuation();
      }

      public readonly bool IsCompleted => resHandle.IsCompleted;

      public readonly T GetResult() {
        resHandle.Error?.Throw();
        return resHandle.Result;
      }
    }
  }

  public interface IResProvider { }

  public interface IResProvider<T> : IResProvider {
    Type Provides => typeof(T);

    IResHandle<T> Provide(ResourceFile resFile, ResourceInfo resInfo, ushort blockIndex);
  }
}
