using UnityEngine;
using System;

using SystemShock.Resource;

using Random = UnityEngine.Random;

namespace SystemShock {
    public class TextScreen : MonoBehaviour {
        public const int LinesNeeded = 7;

        public enum AnimationType {
            Normal,
            Random
        }

        private Renderer Renderer;
        private LevelInfo levelInfo;

        public int Frames;
        public string[] Texts;
        public RenderTexture Texture;
        public TextAnchor Alignment;
        public float FPS;
        public AnimationType Type;
        public bool SmallText;

        private double timeAccumulator;
        private uint currentFrame;

        private void Awake() {
            levelInfo = GameObject.FindObjectOfType<LevelInfo>();
            Renderer = GetComponentInChildren<Renderer>();
        }

        private void OnEnable() {
            currentFrame = uint.MaxValue;
            timeAccumulator = 0.0;
        }

        private void Start() {
            levelInfo.TextScreenRenderer.Render(this);
            DynamicGI.UpdateMaterials(Renderer);
        }

        private void Update() {
            timeAccumulator += Time.deltaTime;

            if (!Renderer.isVisible)
                return;

            uint nextFrame = (uint)(timeAccumulator * FPS);

            if (nextFrame != currentFrame) {
                uint previousFrame = currentFrame;

                currentFrame = Frames > 0 ? nextFrame % (uint)Frames : 1;

                if (currentFrame != previousFrame) {
                    levelInfo.TextScreenRenderer.Render(this);
                    DynamicGI.UpdateMaterials(Renderer);
                }
            }
        }

        public string Text {
            get {
                if (Texts.Length == 0)
                    return string.Empty;

                if (Type == TextScreen.AnimationType.Random)
                    return Texts[Random.Range(0, Texts.Length)];

                if (Frames <= 1)
                    return Texts[0];

                string fullText = string.Empty;
                for (int i = 0; i < TextScreen.LinesNeeded; ++i)
                    fullText += Texts[(currentFrame + i) % Texts.Length] + Environment.NewLine;

                return fullText;
            }
        }
    }
}