using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

using System;
using System.Collections;

namespace SystemShock {
    public class AnimateMaterial : MonoBehaviour {
        private Renderer Renderer;

        [SerializeField]
        private AnimationSet[] animationSets = new AnimationSet[0];

        private double timeAccumulator;
        private uint currentFrame;

        public event Action LoopCompleted;

        private void Awake() {
            Renderer = GetComponentInChildren<Renderer>();
        }

        private void OnEnable() {
            timeAccumulator = 0.0;
        }

        private void Update() {
            timeAccumulator += Time.deltaTime;

            if (!Renderer.isVisible)
                return;

            foreach (AnimationSet animationSet in animationSets)
                UpdateAnimationSet(animationSet);
        }

        private void UpdateAnimationSet(AnimationSet animationSet) {
            int[] MaterialIndices = animationSet.MaterialIndices;
            Material[] Frames = animationSet.Frames;
            ushort AnimationType = animationSet.AnimationType;
            float FPS = animationSet.FPS;

            if (Frames.Length <= 1 || MaterialIndices.Length == 0)
                return;

            uint nextFrame = (uint)(timeAccumulator * FPS);

            if (nextFrame != currentFrame) {
                uint previousFrame = currentFrame;
                bool loopComplete = false;

                if (AnimationType == 0) {
                    loopComplete = nextFrame == Frames.Length;
                    currentFrame = nextFrame % (uint)Frames.Length;
                } else if(AnimationType == 1) {
                    loopComplete = nextFrame == (Frames.Length << 1);
                    uint bounceFrame = nextFrame % (uint)(Frames.Length << 1);

                    if (bounceFrame >= Frames.Length)
                        currentFrame = (uint)(Frames.Length - 1u) - (uint)(bounceFrame % Frames.Length);
                    else
                        currentFrame = nextFrame % (uint)Frames.Length;
                } else if(AnimationType == 2) {
                    loopComplete = nextFrame == Frames.Length;
                    currentFrame = ((uint)Frames.Length - 1) - (nextFrame % (uint)Frames.Length);
                }

                if (currentFrame != previousFrame) {
                    Material[] sharedMaterials = Renderer.sharedMaterials;

                    for (int i = 0; i < MaterialIndices.Length; ++i)
                        sharedMaterials[MaterialIndices[i]] = Frames[currentFrame];

                    Renderer.sharedMaterials = sharedMaterials;

                    DynamicGI.UpdateMaterials(Renderer);
                }

                if (loopComplete && LoopCompleted != null)
                    LoopCompleted();
            }
        }

        public void AddAnimation(int[] materialIndices, Material[] frames, ushort animationType, float fps) {
            int index = animationSets.Length;
            Array.Resize(ref animationSets, index + 1);
            animationSets[index] = new AnimationSet {
                MaterialIndices = materialIndices,
                Frames = frames,
                AnimationType = animationType,
                FPS = fps
            };
        }

        [Serializable]
        private struct AnimationSet {
            public int[] MaterialIndices;
            public Material[] Frames;
            public ushort AnimationType;
            public float FPS;
        }
    }
}
