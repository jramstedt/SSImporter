
using System;
using System.ComponentModel;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace SS.Resources {
  [DisplayName("System Shock Object Property Data Provider")]
  public class ObjectPropertiesProvider : RawDataProvider<ObjectProperties> {
    public override void Release(IResourceLocation location, object obj) {
      ObjectProperties objectPropertiesFile = obj as ObjectProperties;
      objectPropertiesFile.Dispose();
    }

    public override Type GetDefaultType(IResourceLocation location) => typeof(ObjectProperties);

    public override ObjectProperties Convert(Type type, byte[] data) => new ObjectProperties(data);
  }
}
