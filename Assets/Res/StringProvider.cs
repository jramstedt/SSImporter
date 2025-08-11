using System;
using System.Text;
using Unity.Collections;
using static SS.Resources.ResourceFile;

namespace SS.Resources {
  public class StringProvider : IResProvider<string> {
    private class StringLoader : LoaderBase<string> {
      public StringLoader(ResourceFile resFile, ResourceInfo resInfo, ushort blockIndex) {
        var data = resFile.GetResourceData(resInfo, blockIndex);
        InvokeCompletionEvent(Encoding.ASCII.GetString(data).TrimEnd('\0'));
      }
    }

    public IResHandle<string> Provide(ResourceFile resFile, ResourceInfo resInfo, ushort blockIndex) {
      if (resInfo.info.ContentType != ContentType.String)
        throw new Exception($"Resource {resInfo.info.Id:X4}:{blockIndex:X4} is not {nameof(ContentType.String)}.");

      return new StringLoader(resFile, resInfo, blockIndex);
    }
  }

  public class NativeTextProvider : IResProvider<NativeText> {
    private class NativeTextLoader : LoaderBase<NativeText> {
      public unsafe NativeTextLoader(ResourceFile resFile, ResourceInfo resInfo, ushort blockIndex) {
        var data = resFile.GetResourceData(resInfo, blockIndex);
        var nativeText = new NativeText(data.Length, Allocator.Persistent);
        fixed (byte* src = data)
          nativeText.Append(src, data.Length);

        InvokeCompletionEvent(nativeText);
      }
    }
    
    public IResHandle<NativeText> Provide(ResourceFile resFile, ResourceInfo resInfo, ushort blockIndex) {
      if (resInfo.info.ContentType != ContentType.String)
        throw new Exception($"Resource {resInfo.info.Id:X4}:{blockIndex:X4} is not {nameof(ContentType.String)}.");

      return new NativeTextLoader(resFile, resInfo, blockIndex);
    }
  }
}
