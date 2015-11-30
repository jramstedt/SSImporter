﻿using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;

using SystemShock.Object;

namespace SystemShock.Resource {
    [ExecuteInEditMode]
    public sealed class ObjectFactory : AbstractGameController<ObjectFactory>, ISerializationCallbackReceiver {
        [SerializeField]
        private List<ScriptableObject> Libraries;

        private Dictionary<string, ScriptableObject> LibraryMap;

        public MessageBus MessageBus { get; private set; }

        private LevelInfo levelInfo;
        public LevelInfo LevelInfo {
            get { return levelInfo ?? UpdateLevelInfo(); }
        }

        private void Awake() {
            UpdateLevelInfo();
        }

        public void Start() {
            MessageBus = MessageBus.GetController();
            UpdateLevelInfo();
        }

        private void OnLevelWasLoaded(int level) {
            UpdateLevelInfo();
        }

        public LevelInfo UpdateLevelInfo() {
            return levelInfo = GameObject.FindObjectOfType<LevelInfo>();
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

        public SystemShockObject Get(ushort objectId) {
            SystemShockObject ssObject = null;

            if (objectId != 0 && !LevelInfo.Objects.TryGetValue(objectId, out ssObject))
                Debug.LogWarningFormat(this, "Unable to find object {0}", objectId);

            return ssObject;
        }

        public SystemShockObject[] GetAll(ObjectClass Class, byte Subclass, byte Type) {
            return (from ssObject in LevelInfo.Objects.Values
                    where ssObject.ObjectInstance.Class == Class && 
                          ssObject.ObjectInstance.SubClass == Subclass &&
                          ssObject.ObjectInstance.Type == Type
                    select ssObject).ToArray();
        }

        public T Get<T>(ushort objectId) where T : class {
            SystemShockObject ssObject = Get(objectId);
            if (ssObject == null)
                return null;

            T component = ssObject.GetComponent<T>();
            if(component == null)
                Debug.LogWarningFormat(this, "Unable to find {0} {1}", typeof(T).FullName, objectId);

            return component;
        }

        public T[] GetAll<T>(ObjectClass Class, byte Subclass, byte Type) where T : class {
            return (from ssObject in LevelInfo.Objects.Values
                    where ssObject.ObjectInstance.Class == Class &&
                          ssObject.ObjectInstance.SubClass == Subclass &&
                          ssObject.ObjectInstance.Type == Type
                    let component = ssObject.GetComponent<T>()
                    where component != null
                    select component).ToArray();
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

            MessageBus.Send(new ObjectCreated(ssObject));

            return ssObject;
        }

        public void Destroy(ushort objectId) {
            SystemShockObject ssObject;
            if (LevelInfo.Objects.TryGetValue(objectId, out ssObject)) {
                LevelInfo.Objects.Remove(objectId);

                MessageBus.Send(new ObjectDestroying(ssObject));
                
                Destroy(ssObject.gameObject);
            }
        }

        public SystemShockObject Replace(ushort objectId, ObjectInstance objectInstance, IClassData instanceData) {
            if (objectId != instanceData.ObjectId)
                throw new ArgumentException("Object Ids must match.", "instanceData");

            Destroy(objectId);
            SystemShockObject ssObject = Instantiate(objectInstance, instanceData);

            MessageBus.Send(new ObjectReplaced(ssObject));

            return ssObject;
        }
    }

    public sealed class ObjectDestroying : GenericMessage<SystemShockObject> {
        public ObjectDestroying(SystemShockObject target) : base(target) { }
    }

    public sealed class ObjectCreated : GenericMessage<SystemShockObject> {
        public ObjectCreated(SystemShockObject target) : base(target) { }
    }

    public sealed class ObjectReplaced : GenericMessage<SystemShockObject> {
        public ObjectReplaced(SystemShockObject target) : base(target) { }
    }
}