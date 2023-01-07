
using System;
using System.ComponentModel;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace SS.Resources {
  [DisplayName("System Shock Resource File Provider")]
  public class ResourceFileProvider : RawDataProvider<ResourceFile> {
    public override void Release(IResourceLocation location, object obj) {
      ResourceFile resFile = obj as ResourceFile;
      resFile.Dispose();
    }

    public override Type GetDefaultType(IResourceLocation location) => typeof(ResourceFile);

    public override ResourceFile Convert(Type type, byte[] data) => new ResourceFile(data);
  }
}
