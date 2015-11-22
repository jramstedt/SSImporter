using UnityEngine;
using UnityEditor;

using SystemShock.Resource;
using SystemShock.Object;
using SystemShock;

[CustomPropertyDrawer(typeof(ObjectReferenceAttribute))]
public class ObjectReferenceDrawer : PropertyDrawer {
    public LevelInfo LevelInfo;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
        if (LevelInfo == null)
            LevelInfo = GameObject.FindObjectOfType<LevelInfo>();

        if (property.propertyType == SerializedPropertyType.Integer && property.intValue != 0) {
            SystemShockObject obj;
            if(LevelInfo.Objects.TryGetValue((ushort)property.intValue, out obj)) {
                EditorGUI.ObjectField(position, property.displayName, obj, typeof(SystemShockObject), EditorUtility.IsPersistent(obj));
            } else {
                EditorGUI.LabelField(position, property.displayName, property.intValue.ToString(), EditorStyles.boldLabel);
            }
        } else {
            EditorGUI.LabelField(position, property.displayName, property.intValue.ToString(), EditorStyles.boldLabel);
        }
    }
}