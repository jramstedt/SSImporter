using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Collections;

//[ExecuteInEditMode]
public class TileUpdater : MonoBehaviour {
    private MaterialPropertyBlock materialPropertyBlock;
    private int nameID;

    private void Awake() {
        nameID = Shader.PropertyToID(@"_MainTex_ST");

        materialPropertyBlock = new MaterialPropertyBlock();

        GetComponent<Renderer>().GetPropertyBlock(materialPropertyBlock);

        Debug.Log(materialPropertyBlock.GetVector(nameID));

        #if UNITY_EDITOR
        EditorUtility.SetDirty(this);
        #endif
    }

    private void Update() {
        if (GetComponent<Renderer>().isVisible) {
            //Debug.Log(materialPropertyBlock);

            //materialPropertyBlock.Clear();
            materialPropertyBlock.SetVector(nameID, new Vector4(0.25f, 1f, Mathf.Floor(Time.time * 5f) * 0.25f, 0f));
            //materialPropertyBlock.AddVector(nameID, new Vector4(0.25f, 1f, Time.time % 1f, 0f));
            //materialPropertyBlock.SetColor(Shader.PropertyToID(@"_Color"), new Color(Time.time % 1f, Time.time % 1f, Time.time % 1f, 1f));

            GetComponent<Renderer>().SetPropertyBlock(materialPropertyBlock);
        }
    }
}
