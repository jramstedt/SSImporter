using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

using SystemShock.Object;

namespace SystemShock.Resource {
    public sealed class ObjectFactory : AbstractGameController<ObjectFactory>, ISerializationCallbackReceiver {
        [SerializeField]
        private List<ScriptableObject> Libraries;

        private Dictionary<string, ScriptableObject> LibraryMap;

        public LevelInfo levelInfo { get; private set; }

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
            levelInfo = GameObject.FindObjectOfType<LevelInfo>();
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
        
        public GameObject Instantiate(ObjectInstance objectInstance, object instanceData) {
            if (objectInstance.InUse == 0) {
                Debug.LogWarning(@"Instance not in use.");
                return null;
            }
            
            PrefabLibrary prefabLibrary = GetLibrary<PrefabLibrary>(@"objprefabs");

            GameObject prefab = prefabLibrary.GetPrefab(objectInstance.Class, objectInstance.SubClass, objectInstance.Type);

#if UNITY_EDITOR
            GameObject gameObject = UnityEditor.PrefabUtility.InstantiatePrefab(prefab) as GameObject;
#else
            GameObject gameObject = GameObject.Instantiate(prefab);
#endif

            gameObject.transform.localPosition = new Vector3(Mathf.Round(64f * objectInstance.X / 256f) / 64f, objectInstance.Z * levelInfo.HeightFactor, Mathf.Round(64f * objectInstance.Y / 256f) / 64f);
            gameObject.transform.localRotation = Quaternion.Euler(-objectInstance.Pitch / 256f * 360f, objectInstance.Yaw / 256f * 360f, -objectInstance.Roll / 256f * 360f);
            gameObject.transform.localScale = Vector3.one;

            SystemShockObject ssObject = gameObject.AddComponent(Type.GetType(@"SystemShock.InstanceObjects." + objectInstance.Class + @", Assembly-CSharp")) as SystemShockObject;
            ssObject.Class = (SystemShock.Object.ObjectClass)objectInstance.Class;
            ssObject.SubClass = objectInstance.SubClass;
            ssObject.Type = objectInstance.Type;

            ssObject.AIIndex = objectInstance.AIIndex;
            ssObject.Hitpoints = objectInstance.Hitpoints;
            ssObject.AnimationState = objectInstance.AnimationState;

            ssObject.Unknown1 = objectInstance.Unknown1;
            ssObject.Unknown2 = objectInstance.Unknown2;
            ssObject.Unknown3 = objectInstance.Unknown3;

            ssObject.SetClassData(instanceData);

            ssObject.InitializeInstance();

            return gameObject;
        }
    }
}