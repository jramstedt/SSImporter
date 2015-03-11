using UnityEngine;
using System.Collections;

#if UNITY_EDITOR
using UnityEditor;
using System;
#endif

namespace SystemShock.Resource {
    public abstract class AbstractResourceLibrary<T> : ScriptableObject where T : AbstractResourceLibrary<T> {
/*#if UNITY_EDITOR
        public const string ResourceRoot = @"Assets/SystemShock/";

        public static T GetLibrary(string ResourceFile) {
            return AssetDatabase.LoadAssetAtPath(ResourceRoot + ResourceFile + @".asset", typeof(T)) as T;
        }

        public static T CreateLibrary(string ResourceFile) {
            T modelLibrary = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(modelLibrary, ResourceRoot + ResourceFile + @".asset");
            return modelLibrary;
        }
#else*/
        public static T GetLibrary(string ResourceFile) {
            ObjectFactory objectFactory = ObjectFactory.GetController();
            return objectFactory.GetLibrary<T>(ResourceFile);
        }
//#endif
    }
}