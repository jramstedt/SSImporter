using UnityEngine;
using UnityEngine.UI;

namespace SystemShock {
    public class TextScreenRenderer : MonoBehaviour {
        new private Camera camera;
        private CanvasScaler canvasScaler;
        private Text text;

        private void Awake() {
            camera = GetComponentInChildren<Camera>();
            canvasScaler = GetComponentInChildren<CanvasScaler>();
            text = GetComponentInChildren<Text>();
        }

        public void Render(TextScreen textScreen) {
            canvasScaler.scaleFactor = textScreen.Texture.height / 64f;
            text.text = textScreen.Text;
            text.alignment = textScreen.Alignment;

            camera.enabled = true;
            camera.targetTexture = textScreen.Texture;
            camera.Render();
            camera.enabled = false;
        }
    }
}