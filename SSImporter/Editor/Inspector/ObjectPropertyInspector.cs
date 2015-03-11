using UnityEngine;
using UnityEditor;
using System;
using System.Collections;

using SystemShock.Resource;

[CustomEditor(typeof(ObjectPropertyLibrary))]
public class ObjectPropertyInspector : InspectorBase<ObjectPropertyLibrary> {

    private Editor[] editor;

    public override void OnInspectorGUI() {
        serializedObject.Update();

        DrawPropertiesExcluding(serializedObject, @"ObjectDatas");

        SerializedProperty objectDatas = serializedObject.FindProperty(@"ObjectDatas");
        EditorGUILayout.LabelField("Objects: " + objectDatas.arraySize);

        if (editor == null)
            editor = new Editor[objectDatas.arraySize];
        else if (editor.Length != objectDatas.arraySize)
            Array.Resize(ref editor, objectDatas.arraySize);

        for (int i = 0; i < objectDatas.arraySize; ++i) {
            SerializedProperty dataProp = objectDatas.GetArrayElementAtIndex(i);

            string label = (i + 1) + @" " + dataProp.objectReferenceValue.name;
            dataProp.isExpanded = GUILayout.Toggle(dataProp.isExpanded, label, EditorStyles.foldout);

            if (dataProp.isExpanded) {
                Editor drawer = editor[i];

                if (drawer == null)
                    drawer = editor[i] = Editor.CreateEditor(dataProp.objectReferenceValue);

                ++EditorGUI.indentLevel;
                drawer.OnInspectorGUI();
                --EditorGUI.indentLevel;
            }
        }
    }
}
