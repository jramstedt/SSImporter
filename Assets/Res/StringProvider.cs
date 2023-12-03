using System;
using System.Text;
using static SS.Resources.ResourceFile;

namespace SS.Resources {
  public class StringProvider : IResProvider<string> {
    private class StringLoader : LoaderBase<string> {
      public StringLoader(ResourceFile resFile, ResourceInfo resInfo, ushort blockIndex) {
        var data = resFile.GetResourceData(resInfo, blockIndex);
        InvokeCompletionEvent(Encoding.ASCII.GetString(data).TrimEnd('\0'));
      }
    }

    public IResHandle<string> Provide(ResourceFile resFile, ResourceFile.ResourceInfo resInfo, ushort blockIndex) {
      if (resInfo.info.ContentType != ResourceFile.ContentType.String)
        throw new Exception($"Resource {resInfo.info.Id:X4}:{blockIndex:X4} is not {nameof(ResourceFile.ContentType.String)}.");

      return new StringLoader(resFile, resInfo, blockIndex);
    }
  }
}
