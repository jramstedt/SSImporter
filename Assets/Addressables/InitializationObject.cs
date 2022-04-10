using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.Util;
using static SS.Resources.ResourceFile;

namespace SS.Resources {
  [CreateAssetMenu(fileName = "InitializationObject.asset", menuName = "Addressables/Initialization/System Shock")]
  public class InitializationObject : ScriptableObject, IObjectInitializationDataProvider {
    public string Name => @"System Shock";

    public InitializationData Data = new InitializationData();

    public ObjectInitializationData CreateObjectInitializationData() {
      return ObjectInitializationData.CreateSerializedInitializationData<Initialization>(nameof(Initialization), Data);
    }
  }

  public class Initialization : IInitializableObject {
    public const string rootPath = @"C:\Users\Janne\Downloads\SYSTEMSHOCK-Portable-v1.2.3\RES";
    public const string dataPath = rootPath + @"\DATA\";

    private static readonly ChunkResourceLocator resourceLocator = new ChunkResourceLocator();

    private static Dictionary<ContentType, string> contentProviders = new Dictionary<ContentType, string> {
        { ContentType.Palette, typeof(PaletteProvider).FullName },
        { ContentType.String, @"String" },
        { ContentType.Image, typeof(BitmapProvider).FullName },
        { ContentType.Font, @"Font" },
        { ContentType.Animation, @"Animation" },
        { ContentType.Voc, typeof(AudioClipProvider).FullName },
        { ContentType.Obj3D, typeof(MeshProvider).FullName },
        { ContentType.Movie, @"Movie" },
        { ContentType.Map, @"Map" }
    };

    private static Dictionary<ContentType, Type> contentTypes = new Dictionary<ContentType, Type> {
      { ContentType.Palette, typeof(Palette) },
      { ContentType.Image, typeof(BitmapSet) },
      { ContentType.Voc, typeof(AudioClip) },
      { ContentType.Obj3D, typeof(MeshInfo) },
    };

    public bool Initialize(string id, string data) {
      return true;
    }

    public AsyncOperationHandle<bool> InitializeAsync(ResourceManager rm, string id, string data) {
      rm.ResourceProviders.Add(new ObjectPropertiesProvider());
      rm.ResourceProviders.Add(new ResourceFileProvider());
      rm.ResourceProviders.Add(new AudioClipProvider());
      rm.ResourceProviders.Add(new BitmapProvider());
      rm.ResourceProviders.Add(new PaletteProvider());
      rm.ResourceProviders.Add(new MeshProvider());
      rm.ResourceProviders.Add(new RawDataProvider()); // Generic data loader

      Addressables.AddResourceLocator(resourceLocator);
      Addressables.AddResourceLocator(new BlockResourceLocator());

      var resourceFiles = new List<string> {
        "gamepal.res",

        "handart.res",
        "gamescr.res",
        "mfdart.res",
        "obj3d.res",
        "cybstrng.res",

        "texture.res",
        "citmat.res",

        "objart.res",
        "objart2.res",
        "objart3.res",
        "sideart.res",

        "digifx.res",
        "citbark.res",
        "citalog.res",

        "vidmail.res",
      }.ConvertAll<IResourceLocation>(resFile => new ResourceLocationBase(resFile, dataPath + resFile, typeof(ResourceFileProvider).FullName, typeof(ResourceFile)));

      var resourceLocationLoadOps = new List<AsyncOperationHandle>();
      foreach (var resourceLocation in resourceFiles) {
        // Debug.Log($"InitializeResourceManager {resourceLocation.PrimaryKey} {resourceLocation.InternalId} {resourceLocation.ResourceType} {resourceLocation.Dependencies}");
        var loadOp = rm.ProvideResource<ResourceFile>(resourceLocation);
        loadOp.Completed += op => {
          foreach (var (resId, resource) in op.Result.ResourceEntries) {
            if (!contentTypes.TryGetValue(resource.info.ContentType, out Type resourceType))
              resourceType = typeof(object);

            resourceLocator.Add(resId, new ResourceLocationBase($"{resId}", $"{resId}", contentProviders[resource.info.ContentType], resourceType, resourceLocation));
          }
        };

        resourceLocationLoadOps.Add(loadOp);
      }

      var resourceLocatorBuild = rm.CreateGenericGroupOperation(resourceLocationLoadOps);

      var op = new InitOp(() => Initialize(id, data));
      return rm.StartOperation(op, resourceLocatorBuild);
    }

    class InitOp : AsyncOperationBase<bool>, IUpdateReceiver {
      private readonly Func<bool> InitializeCallback;
      public InitOp(Func<bool> initializeCallback) : base() {
        InitializeCallback = initializeCallback;
      }

      public void Update(float unscaledDeltaTime) {
        if (InitializeCallback != null)
          Complete(InitializeCallback(), true, @"");
        else
          Complete(true, true, @"");
      }

      protected override void Execute() {
        Update(0.0f);
      }
    }
  }

  [Serializable]
  public class InitializationData { }
}