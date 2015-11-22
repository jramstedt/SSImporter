using UnityEngine;
using UnityEngine.UI;

namespace SystemShock {
    public class TextScreenRenderer : MonoBehaviour {
        new private Camera camera;
        private CanvasScaler canvasScaler;
        private Text text;
        private RectTransform textRectTransform;

        private void Awake() {
            camera = GetComponentInChildren<Camera>();
            canvasScaler = GetComponentInChildren<CanvasScaler>();
            text = GetComponentInChildren<Text>();
            textRectTransform = text.GetComponent<RectTransform>();
        }

        public void Render(TextScreen textScreen) {
            canvasScaler.scaleFactor = textScreen.Texture.height / 64f;
            text.text = textScreen.Text;
            text.alignment = textScreen.Alignment;
            textRectTransform.localScale = textScreen.SmallText ? Vector3.one * 0.60f : Vector3.one;

            //camera.enabled = true;
            camera.targetTexture = textScreen.Texture;
            camera.Render();
            //camera.enabled = false;
        }
    }
}