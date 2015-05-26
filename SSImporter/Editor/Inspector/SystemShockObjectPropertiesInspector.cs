using UnityEngine;
using UnityEditor;

using SystemShock.Object;

[CustomEditor(typeof(SystemShockObjectProperties), true)]
public class SystemShockObjectPropertiesInspector : InspectorBase<SystemShockObjectProperties> {
    private Editor editor;
    private bool fold;

    public override void OnInspectorGUI() {
        DrawPropertiesExcluding(serializedObject, @"Properties");

        SerializedProperty dataProp = serializedObject.FindProperty(@"Properties");

        if (editor == null)
            editor = Editor.CreateEditor(dataProp.objectReferenceValue);

        ++EditorGUI.indentLevel;
        fold = EditorGUILayout.InspectorTitlebar(fold, dataProp.objectReferenceValue);
        if (fold) {
            GUI.enabled = false;
            editor.OnInspectorGUI();
            GUI.enabled = true;
        }
        --EditorGUI.indentLevel;
    }
}
