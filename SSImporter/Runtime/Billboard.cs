using UnityEngine;
using System.Collections;

public class Billboard : MonoBehaviour {
    private void OnWillRenderObject() {
        transform.rotation = Camera.main.transform.rotation;
    }
}
