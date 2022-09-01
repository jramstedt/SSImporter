using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace SS.Resources {
  public class ChunkResourceLocator : IResourceLocator {
    public string LocatorId => nameof(ChunkResourceLocator);
    public IEnumerable<object> Keys => Locations.Keys.Cast<object>();
    private readonly Dictionary<ushort, IList<IResourceLocation>> Locations;

    public ChunkResourceLocator(int capacity = 1024) {
      Locations = new Dictionary<ushort, IList<IResourceLocation>>(capacity);
    }

    public bool Locate(object key, Type type, out IList<IResourceLocation> locations) {
      locations = null;

      ushort resId, block;
      if (Utils.ExtractResourceIdAndBlock(key, out resId, out block)) {
        if (block != 0) return false;

        if (Locations.TryGetValue(resId, out locations)) {
          var locationsList = locations.Where(loc => loc.ResourceType.IsAssignableFrom(type)).ToList();
          if (locationsList.Count > 0) {
            locations = locationsList;
            return true;
          }
        }
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
