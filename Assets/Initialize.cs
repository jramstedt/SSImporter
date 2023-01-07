using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace SS.Resources {
  public static class Initialize {
    [RuntimeInitializeOnLoadMethod]
    private static void Init() {
      Addressables.InitializeAsync().Completed += AddressablesInitializeCompleted;
    }

    private static void AddressablesInitializeCompleted(AsyncOperationHandle<UnityEngine.AddressableAssets.ResourceLocators.IResourceLocator> obj) {
      Debug.Log(@"AddressablesInitializeCompleted");
      InitializeResourceManager();
    }

    private async static void InitializeResourceManager() {
      // Tests:
      var audioOp = Addressables.LoadAssetAsync<AudioClip>(0x00C9);
      audioOp.Completed += loadOp => AudioSource.PlayClipAtPoint(loadOp.Result, Vector3.zero);

      var paletteOp = Addressables.LoadAssetAsync<Palette>(0x02BC);
      paletteOp.Completed += loadOp => Debug.Log($"{loadOp.Status} {loadOp.Result}");

      var textureOp = Addressables.LoadAssetAsync<BitmapSet>($"{0x004D}:{10}");
      textureOp.Completed += loadOp => Debug.Log($"{loadOp.Status} {loadOp.Result}");

      #region Load archive.dat
      Debug.Log(@"Load archive.dat");
      await SaveLoader.LoadMap(1, Initialization.rootPath + @"\DATA", @"ARCHIVE.DAT");
      #endregion
    }
  }
}
