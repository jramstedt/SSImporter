using UnityEngine;
using UnityEditor;

[CustomPropertyDrawer(typeof(EnumMaskAttribute))]
public class EnumMaskDrawer : PropertyDrawer {
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
        if (property.propertyType == SerializedPropertyType.Enum) {
            property.intValue = EditorGUI.MaskField(position, label, property.intValue, property.enumNames);
        }
    }
}
