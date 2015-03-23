using UnityEngine;
using System.Collections;

namespace SystemShock {
    public class AnimateMaterial : MonoBehaviour {
        private Renderer Renderer;

        public int[] MaterialIndices;
        public Material[] Frames;
        public ushort AnimationType;
        public float FPS;

        private uint currentFrame;

        private void Awake() {
            Renderer = GetComponentInChildren<Renderer>();
        }

        private void Update() {
            if (!Renderer.isVisible || Frames.Length <= 1 || MaterialIndices.Length == 0)
                return;

            uint nextFrame = (uint)Mathf.FloorToInt(Time.time * FPS);

            if (nextFrame != currentFrame) {
                uint previousFrame = currentFrame;

                if (AnimationType == 0) {
                    currentFrame = nextFrame % (uint)Frames.Length;
                } else {
                    uint bounceFrame = nextFrame % (uint)(Frames.Length * 2);

                    if (bounceFrame >= Frames.Length)
                        currentFrame = (uint)(Frames.Length - 1u) - (uint)(bounceFrame % Frames.Length);
                    else
                        currentFrame = nextFrame % (uint)Frames.Length;
                }

                if (currentFrame != previousFrame) {
                    Material[] sharedMaterials = Renderer.sharedMaterials;

                    for (int i = 0; i < MaterialIndices.Length; ++i)
                        sharedMaterials[MaterialIndices[i]] = Frames[currentFrame];

                    Renderer.sharedMaterials = sharedMaterials;
                }
            }
        }

        public void Setup(int[] materialIndices, Material[] frames, ushort animationType, float fps) {
            MaterialIndices = materialIndices;
            Frames = frames;
            AnimationType = animationType;
            FPS = fps;
        }
    }

}
