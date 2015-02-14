using UnityEngine;
using UnityEditor;

public class InspectorBase<T> : Editor where T : UnityEngine.Object {
    protected T Target { get { return target as T; } }
}