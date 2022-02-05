using System;
using UnityEngine;

namespace SS.Resources {
  public static class Utils {
    public static bool ExtractResourceIdAndBlock(object key, out ushort resourceId, out ushort block) {
      if (key is string) {
        var keyString = key as string;
        var parts = keyString.Split(':');
        if (parts.Length == 2) {
          resourceId = ushort.Parse(parts[0]);
          block = ushort.Parse(parts[1]);
        } else {
          resourceId = ushort.Parse(parts[0]);
          block = 0;
        }
        return true;
      } else if (key.IsNumber()) {
        resourceId = Convert.ToUInt16(key);
        block = 0;
        return true;
      }

      resourceId = 0;
      block = 0;
      return false;
    }

    public static bool IsNumber(this object value) {
      return value is sbyte
            || value is byte
            || value is short
            || value is ushort
            || value is int
            || value is uint
            || value is long
            || value is ulong
            || value is float
            || value is double
            || value is decimal;
    }
  }
}
