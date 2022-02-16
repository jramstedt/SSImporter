using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.SceneManagement;
using static SS.Resources.ResourceFile;

namespace SS.Resources {
  public static class Initialize {
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
      { ContentType.Obj3D, typeof(Mesh) },
    };

    private static readonly ChunkResourceLocator resourceLocator = new ChunkResourceLocator();

    [RuntimeInitializeOnLoadMethod]
    private static void Init() {
      Addressables.InitializeAsync().Completed += AddressablesInitializeCompleted;
    }

    private static void AddressablesInitializeCompleted(AsyncOperationHandle<UnityEngine.AddressableAssets.ResourceLocators.IResourceLocator> obj) {
      InitializeResourceManager();
    }
    
    private static void InitializeResourceManager() {
      var rootPath = @"C:\Users\Janne\Downloads\SYSTEMSHOCK-Portable-v1.2.3\RES";

      Addressables.ResourceManager.ResourceProviders.Add(new ResourceFileProvider());
      Addressables.ResourceManager.ResourceProviders.Add(new AudioClipProvider());
      Addressables.ResourceManager.ResourceProviders.Add(new BitmapProvider());
      Addressables.ResourceManager.ResourceProviders.Add(new PaletteProvider());
      Addressables.ResourceManager.ResourceProviders.Add(new RawDataProvider()); // Generic data loader

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
      }.ConvertAll<IResourceLocation>(resPath => new ResourceLocationBase(resPath, rootPath + @"\DATA\" + resPath, typeof(ResourceFileProvider).FullName, typeof(ResourceFile)));

      var resourceLocationLoadOps = new List<AsyncOperationHandle>();

      foreach (var resourceLocation in resourceFiles) {
        // Debug.Log($"InitializeResourceManager {resourceLocation.PrimaryKey} {resourceLocation.InternalId} {resourceLocation.ResourceType} {resourceLocation.Dependencies}");

        var loadOp = Addressables.ResourceManager.ProvideResource<ResourceFile>(resourceLocation);
        loadOp.Completed += op => {
          var resFile = op.Result;

          foreach (var (resId, resource) in resFile.ResourceEntries) {
            Type resourceType;
            if (!contentTypes.TryGetValue(resource.info.ContentType, out resourceType))
              resourceType = typeof(object);

            resourceLocator.Add(resId, new ResourceLocationBase($"{resId}", $"{resId}", contentProviders[resource.info.ContentType], resourceType, resourceLocation));
          }
        };

        resourceLocationLoadOps.Add(loadOp);
      }

      var resourceLocatorBuild = Addressables.ResourceManager.CreateGenericGroupOperation(resourceLocationLoadOps);

      // Tests:
      resourceLocatorBuild.Completed += op => {
        var audioOp = Addressables.LoadAssetAsync<AudioClip>(0x00C9);
        audioOp.Completed += loadOp => AudioSource.PlayClipAtPoint(loadOp.Result, Vector3.zero);

        var paletteOp = Addressables.LoadAssetAsync<Palette>(0x02BC);
        paletteOp.Completed += loadOp => Debug.Log($"{loadOp.Status} {loadOp.Result}");

        var textureOp = Addressables.LoadAssetAsync<BitmapSet>($"{0x004D}:{10}");
        textureOp.Completed += loadOp => Debug.Log($"{loadOp.Status} {loadOp.Result}");
      };

      #region Load archive.dat
      resourceLocatorBuild.Completed += op => {
        SaveLoader.LoadMap(1, rootPath + @"\DATA\ARCHIVE.DAT", rootPath + @"\DATA\SHADTABL.DAT");
      };
      #endregion
    }
  }
}
