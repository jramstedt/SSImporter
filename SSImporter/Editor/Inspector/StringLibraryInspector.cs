using UnityEngine;
using UnityEditor;
using System.Collections;

using SystemShock.Resource;

[CustomEditor(typeof(StringLibrary))]
public class StringLibraryInspector : InspectorBase<StringLibrary> {
    private bool[] foldOuts;

    public override void OnInspectorGUI() {
        EditorGUIUtility.hierarchyMode = true;
        EditorGUIUtility.labelWidth = 50;

        CyberString[] Strings = Target.Strings;
        uint[] ChunkIds = Target.ChunkIds;

        if (foldOuts == null)
            foldOuts = new bool[ChunkIds.Length];

        for (int chunkIndex = 0; chunkIndex < ChunkIds.Length; ++chunkIndex) {
            string[] chunkStrings = Strings[chunkIndex].Strings;
            uint chunkId = ChunkIds[chunkIndex];

            if (foldOuts[chunkIndex] = EditorGUILayout.Foldout(foldOuts[chunkIndex], chunkId.ToString())) {
                ++EditorGUI.indentLevel;

                for (int stringIndex = 0; stringIndex < chunkStrings.Length; ++stringIndex) {
                    string chunkString = chunkStrings[stringIndex];
                    string removedBreaks = chunkString.Replace("\r\n", @"\n").Replace("\n", @"\n").Replace("\r", @"\n");
                    EditorGUILayout.LabelField(stringIndex.ToString(), removedBreaks);
                }

                --EditorGUI.indentLevel;
            }
        }
    }
}