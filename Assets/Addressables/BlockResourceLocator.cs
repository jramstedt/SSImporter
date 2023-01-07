using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace SS.Resources {
  public class BlockResourceLocator : IResourceLocator {
    public string LocatorId => nameof(BlockResourceLocator);
    public IEnumerable<object> Keys => new object[0];

    public BlockResourceLocator() { }

    public bool Locate(object key, Type type, out IList<IResourceLocation> locations) {
      ushort resId, block;
      if (Utils.ExtractResourceIdAndBlock(key, out resId, out block)) {
        HashSet<IResourceLocation> chunkLocations = new HashSet<IResourceLocation>();
        foreach (var locator in Addressables.ResourceLocators) {
          if (locator == this) continue;

          if (locator.Locate(resId, type, out IList<IResourceLocation> locs))
            chunkLocations.UnionWith(locs);
        }

        locations = new List<IResourceLocation>(chunkLocations.Count);
        foreach (var location in chunkLocations) {
          if (location.HasDependencies)
            locations.Add(new ResourceLocationBase($"{location.PrimaryKey}", $"{location.InternalId}:{block}", location.ProviderId, type, location.Dependencies.ToArray()));
          else
            locations.Add(new ResourceLocationBase($"{location.PrimaryKey}", $"{location.InternalId}:{block}", location.ProviderId, type));
        }

        return locations.Count > 0;
      }

      locations = null;
      return false;
    }
  }
}
