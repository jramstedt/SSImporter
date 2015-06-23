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
        private int currentFrame;

        public event Action LoopCompleted;

        private void Awake() {
            Renderer = GetComponentInChildren<Renderer>();
        }

        private void OnEnable() {
            timeAccumulator = 0.0;
            currentFrame = -1;
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

            int nextFrame = (int)(timeAccumulator * FPS);

            int previousFrame = currentFrame;
            bool loopComplete = false;

            if (AnimationType == 0) { // Loop
                loopComplete = nextFrame == Frames.Length;
                currentFrame = nextFrame % Frames.Length;
            } else if(AnimationType == 1) { // PingPong
                loopComplete = nextFrame == (Frames.Length << 1);
                int bounceFrame = nextFrame % (Frames.Length << 1);

                if (bounceFrame >= Frames.Length)
                    currentFrame = (Frames.Length - 1) - (bounceFrame % Frames.Length);
                else
                    currentFrame = nextFrame % Frames.Length;
            } else if(AnimationType == 2) { // Reverse loop
                loopComplete = nextFrame == Frames.Length;
                currentFrame = (Frames.Length - 1) - (nextFrame % Frames.Length);
            } else if (AnimationType == 3) { // Once only
                loopComplete = nextFrame == Frames.Length;
                currentFrame = Mathf.Min(nextFrame, Frames.Length - 1);
            } else if (AnimationType == 4) { // Once only reverse
                loopComplete = nextFrame == Frames.Length;
                currentFrame = (Frames.Length - 1) - Mathf.Min((int)nextFrame, Frames.Length - 1);
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

        public void AddAnimation(int[] materialIndices, Material[] frames, ushort animationType, float fps) {
            AddAnimation(new AnimationSet {
                MaterialIndices = materialIndices,
                Frames = frames,
                AnimationType = animationType,
                FPS = fps
            });
        }

        public void AddAnimation(AnimationSet animationSet) {
            int index = animationSets.Length;
            Array.Resize(ref animationSets, index + 1);
            animationSets[index] = animationSet;
        }

        public void SetAnimation(int[] materialIndices, Material[] frames, ushort animationType, float fps) {
            animationSets = new AnimationSet[0];
            AddAnimation(materialIndices, frames, animationType, fps);
            OnEnable();
        }

        public void SetAnimation(AnimationSet animationSet) {
            animationSets = new AnimationSet[0];
            AddAnimation(animationSet);
            OnEnable();
        }

        public AnimationSet GetAnimationSet(int index = 0) {
            return animationSets[index];
        }

        [Serializable]
        public struct AnimationSet {
            public int[] MaterialIndices;
            public Material[] Frames;
            public ushort AnimationType;
            public float FPS;
        }
    }
}
