using UnityEngine;
using UnityEditor;

using SystemShock.Resource;
using SystemShock.Object;
using SystemShock;

[CustomPropertyDrawer(typeof(ObjectReferenceAttribute))]
public class ObjectReferenceDrawer : PropertyDrawer {
     public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
        ObjectFactory objectFactory = ObjectFactory.GetController();
        objectFactory.UpdateLevelInfo();

        if (property.propertyType == SerializedPropertyType.Integer && property.intValue != 0) {
            Object obj = objectFactory.Get((ushort)property.intValue);
            EditorGUI.ObjectField(position, property.displayName, obj, typeof(SystemShockObject), EditorUtility.IsPersistent(obj));
        } else {
            EditorGUI.LabelField(position, property.displayName, property.intValue.ToString(), EditorStyles.boldLabel);
        }
    }
}