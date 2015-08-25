﻿using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

using SystemShock.Object;

namespace SystemShock.Resource {
    public sealed class ObjectFactory : AbstractGameController<ObjectFactory>, ISerializationCallbackReceiver {
        [SerializeField]
        private List<ScriptableObject> Libraries;

        private Dictionary<string, ScriptableObject> LibraryMap;

        public LevelInfo LevelInfo { get; private set; }

        private void Awake() {
            UpdateLevelInfo();
        }

        private void OnLevelWasLoaded(int level) {
            UpdateLevelInfo();
        }

        public void Reset() {
            if (Libraries != null)
                Libraries.Clear();

            if (LibraryMap != null)
                LibraryMap.Clear();

            UpdateLevelInfo();
        }

        public void UpdateLevelInfo() {
            LevelInfo = GameObject.FindObjectOfType<LevelInfo>();
        }

        public void AddLibrary<T>(AbstractResourceLibrary<T> library) where T : AbstractResourceLibrary<T> {
            Libraries.Add(library);
            LibraryMap.Add(library.name, library);

#if UNITY_EDITOR
            UnityEditor.PrefabUtility.ReplacePrefab(gameObject, UnityEditor.PrefabUtility.GetPrefabParent(gameObject), UnityEditor.ReplacePrefabOptions.ConnectToPrefab);
#endif
        }

        public T GetLibrary<T>(string ResourceFile) where T : AbstractResourceLibrary<T> {
            return LibraryMap[ResourceFile] as T;
        }

        public void OnAfterDeserialize() {
            LibraryMap = new Dictionary<string, ScriptableObject>();
            foreach (ScriptableObject scriptableObject in Libraries) {
                if (scriptableObject != null)
                    LibraryMap[scriptableObject.name] = scriptableObject;
            }
        }

        public void OnBeforeSerialize() { }

        public ushort NextFreeId() {
            ushort id = 1;
            while (LevelInfo.Objects.ContainsKey(id))
                ++id;

            return id;
        }

        public SystemShockObject Instantiate(ObjectInstance objectInstance, IClassData instanceData) {
            if (objectInstance.InUse == 0) {
                Debug.LogWarning(@"Instance not in use.");
                return null;
            }

            PrefabLibrary prefabLibrary = GetLibrary<PrefabLibrary>(@"objprefabs");

            GameObject prefab = prefabLibrary.GetPrefab(objectInstance.Class, objectInstance.SubClass, objectInstance.Type);

            if (prefab == null) {
                Debug.LogWarningFormat(@"Prefab not found {0}:{1}:{2}", objectInstance.Class, objectInstance.SubClass, objectInstance.Type);
                return null;
            }

#if UNITY_EDITOR
            GameObject gameObject = UnityEditor.PrefabUtility.InstantiatePrefab(prefab) as GameObject;
#else
            GameObject gameObject = GameObject.Instantiate(prefab);
#endif

            gameObject.transform.localPosition = new Vector3(Mathf.Round(128f * objectInstance.X / 256f) / 128f, objectInstance.Z * LevelInfo.HeightFactor, Mathf.Round(128f * objectInstance.Y / 256f) / 128f);
            //gameObject.transform.localPosition = new Vector3(objectInstance.X / 256f, objectInstance.Z * LevelInfo.HeightFactor, objectInstance.Y / 256f);
            gameObject.transform.localRotation = Quaternion.Euler(-objectInstance.Pitch / 256f * 360f, objectInstance.Yaw / 256f * 360f, -objectInstance.Roll / 256f * 360f);
            gameObject.transform.localScale = Vector3.one;

            SystemShockObject ssObject = gameObject.AddComponent(Type.GetType(@"SystemShock.InstanceObjects." + objectInstance.Class + @", Assembly-CSharp")) as SystemShockObject;
            ssObject.Setup(objectInstance, instanceData);

            LevelInfo.Objects.Add(instanceData.ObjectId, ssObject);

            return ssObject;
        }

        public void Destroy(ushort objectId) {
            SystemShockObject ssObject;
            if (LevelInfo.Objects.TryGetValue(objectId, out ssObject)) {
                Destroy(ssObject.gameObject);
                LevelInfo.Objects.Remove(objectId);
            }
        }
    }
}