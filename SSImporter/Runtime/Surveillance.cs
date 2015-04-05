using UnityEngine;
using System.Collections;

namespace SystemShock {
    public class Surveillance : MonoBehaviour {
        private Renderer Renderer;
        public Camera Camera;

        private void Awake() {
            Renderer = GetComponent<Renderer>();
        }

        private void Update() {
            if (!Renderer.isVisible)
                return;

            Camera.Render();

            DynamicGI.UpdateMaterials(Renderer);
        }
    }
}