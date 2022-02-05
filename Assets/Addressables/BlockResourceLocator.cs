using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace SS.Resources {
  public class BlockResourceLocator : IResourceLocator {
    public string LocatorId => nameof(BlockResourceLocator);
    private readonly Dictionary<ushort, IList<IResourceLocation>> Locations;
    public IEnumerable<object> Keys => Locations.Keys.Cast<object>();

    public BlockResourceLocator(int capacity = 100) {
      Locations = new Dictionary<ushort, IList<IResourceLocation>>(capacity);
    }

    public bool Locate(object key, Type type, out IList<IResourceLocation> locations) {
      locations = null;

      // Debug.Log($"BlockResourceLocator Locate {key} {type}");

      ushort resId, block;
      if (!Utils.ExtractResourceIdAndBlock(key, out resId, out block))
        return false;

      // Debug.Log($"BlockResourceLocator SS {resId} {block}");

      if (Locations.TryGetValue(resId, out locations)) {
        locations = locations.Where(loc => loc.ResourceType.IsAssignableFrom(type)).ToList();
        return true;
      }

      return false;
    }

    public void Add(ushort resourceId, IResourceLocation location) {
      IList<IResourceLocation> locations;
      if (!Locations.TryGetValue(resourceId, out locations))
        Locations.Add(resourceId, locations = new List<IResourceLocation>());

      locations.Add(location);
    }

    public void Add(ushort resourceId, IList<IResourceLocation> locations) {
      foreach (var location in locations)
        Add(resourceId, location);
    }
  }
}
