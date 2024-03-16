using static SS.Resources.ResourceFile;

namespace SS.Resources {
  public class ByteProvider : IResProvider<byte[]> {
    private class ByteLoader : LoaderBase<byte[]> {
      public ByteLoader(ResourceFile resFile, ResourceInfo resInfo, ushort blockIndex) {
        var data = resFile.GetResourceData(resInfo, blockIndex);
        InvokeCompletionEvent(data);
      }
    }

    public IResHandle<byte[]> Provide(ResourceFile resFile, ResourceFile.ResourceInfo resInfo, ushort blockIndex) {
      return new ByteLoader(resFile, resInfo, blockIndex);
    }
  }
}
