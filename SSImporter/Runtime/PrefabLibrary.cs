﻿using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

using SystemShock.Resource;
using SystemShock.Object;

namespace SystemShock {
    public class PrefabLibrary : AbstractResourceLibrary<PrefabLibrary> {
        [SerializeField]
        public List<GameObject> Prefabs;

        [SerializeField, HideInInspector]
        private List<uint> indexMap;

        public PrefabLibrary() {
            Prefabs = new List<GameObject>();
            indexMap = new List<uint>();
        }

        public void AddPrefab(uint combinedId, GameObject prefab) {
            if (indexMap.Contains(combinedId))
                throw new ArgumentException(string.Format(@"Prefab {0} already set.", combinedId));

            indexMap.Add(combinedId);
            Prefabs.Add(prefab);
        }

        public GameObject GetPrefab(uint combinedId) {
            int index = indexMap.IndexOf(combinedId);
            return index < 0 ? null : Prefabs[index];
        }

        public GameObject GetPrefab(ObjectClass Class, byte Subclass, byte Type) {
            return GetPrefab((uint)Class << 16 | (uint)Subclass << 8 | Type);
        }

        public int GetIndex(uint combinedId) {
            return indexMap.IndexOf(combinedId);
        }

        public int GetIndex(ObjectClass Class, byte Subclass, byte Type) {
            return GetIndex((uint)Class << 16 | (uint)Subclass << 8 | Type);
        }
    }
}