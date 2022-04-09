using System;
using System.ComponentModel;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.Exceptions;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.ResourceManagement.Util;
using AsyncOperation = UnityEngine.AsyncOperation;

namespace SS.Resources {
  [DisplayName("System Shock Raw Data Provider")]
  public class RawDataProvider<T> : ResourceProviderBase {
    private bool OperationComplete = false;
    private UnityWebRequestAsyncOperation webRequestOp;
    private ProvideHandle provideHandle;

    private float ProgressHandler() => webRequestOp != null ? webRequestOp.progress : 0.0f;

    public override void Provide(ProvideHandle provideHandle) {
      // Debug.Log($"RawDataProvider Provide {typeof(T)} {provideHandle.Location.PrimaryKey} {provideHandle.Location.InternalId} {provideHandle.Location.ResourceType} {provideHandle.Location.Dependencies}");

      this.provideHandle = provideHandle;
      provideHandle.SetWaitForCompletionCallback(WaitForCompletionHandler);
      provideHandle.SetProgressCallback(ProgressHandler);

      var path = provideHandle.ResourceManager.TransformInternalId(provideHandle.Location);
      if (ResourceManagerConfig.ShouldPathUseWebRequest(path)) {
        var webRequest = new UnityWebRequest(path, UnityWebRequest.kHttpVerbGET, new DownloadHandlerBuffer(), null);

        provideHandle.ResourceManager.WebRequestOverride?.Invoke(webRequest);
        webRequestOp = webRequest.SendWebRequest();
        if (webRequestOp.isDone)
          webRequestOpCompleted(webRequestOp);
        else
          webRequestOp.completed += webRequestOpCompleted;
      } else if (File.Exists(path)) {
        provideHandle.SetProgressCallback(() => 0f);
#if NET_4_6
        if (path.Length >= 260)
            path = @"\\?\" + path;
#endif

        var data = File.ReadAllBytes(path); // TODO async
        var result = Convert(provideHandle.Type, data);
        
        provideHandle.Complete(result, result != null, result == null ? new Exception($"Unable to load asset of type {provideHandle.Type} from location {provideHandle.Location}.") : null);
      }
    }

    private bool WaitForCompletionHandler() {
      if (OperationComplete)
        return true;

      if (webRequestOp != null) {
        if (webRequestOp.isDone && !OperationComplete)
          webRequestOpCompleted(webRequestOp);
        else if (!webRequestOp.isDone)
          return false;
      }

      return OperationComplete;
    }

    private void webRequestOpCompleted(AsyncOperation op) {
      if (OperationComplete)
        return;

      Exception exception = null;
      T result = default;

      var webOp = op as UnityWebRequestAsyncOperation;
      if (op != null) {
        var webReq = webOp.webRequest;
        if (webReq.result == UnityWebRequest.Result.InProgress || webReq.result == UnityWebRequest.Result.Success) {
          result = Convert(provideHandle.Type, webReq.downloadHandler.data);
        } else {
          exception = new RemoteProviderException($"{nameof(RawDataProvider<T>)} : unable to load from url : {webReq.url}", provideHandle.Location, new UnityWebRequestResult(webReq));
        }

        provideHandle.Complete(result, result != null, exception);
      }
    }

    public virtual T Convert(Type type, byte[] data) {
      using (MemoryStream ms = new MemoryStream(data)) {
        BinaryReader msbr = new BinaryReader(ms);
        return msbr.Read<T>();
      }
    }
  }

  public class RawDataProvider : RawDataProvider<object> {
    public override object Convert(Type type, byte[] data) {
      using (MemoryStream ms = new MemoryStream(data)) {
        BinaryReader msbr = new BinaryReader(ms);
        return msbr.Read(type);
      }
    }
  }
}
