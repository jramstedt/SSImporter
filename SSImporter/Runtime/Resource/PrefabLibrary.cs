using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

using SystemShock.Resource;
using SystemShock.Object;

namespace SystemShock.Resource {
    public class PrefabLibrary : AbstractResourceLibrary<PrefabLibrary, uint, GameObject> {
        public GameObject GetPrefab(ObjectClass Class, byte Subclass, byte Type) {
            return GetResource((uint)Class << 16 | (uint)Subclass << 8 | Type);
        }
    }
}