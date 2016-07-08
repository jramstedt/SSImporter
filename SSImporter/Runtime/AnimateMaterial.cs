using UnityEngine;

using System;
using System.Collections;
using SystemShock.Resource;
using System.Runtime.InteropServices;

namespace SystemShock {
    public class AnimateMaterial : MonoBehaviour {
        public enum WrapMode : byte {
            FirstFrame,
            Repeat,
            Once,
            ReverseRepeat,
            ReverseOnce,
            PingPong,
            PingPong1,
            PingPong2,

            EnumLength
        }

        private Renderer Renderer;

        [SerializeField]
        private AnimationSet[] animationSets = new AnimationSet[0];

        public event Action LoopCompleted;

        private void Awake() {
            Renderer = GetComponentInChildren<Renderer>();
        }

        private void Update() {
            for (int animationIndex = 0; animationIndex < animationSets.Length; ++animationIndex) {
                AnimationSet animationSet = animationSets[animationIndex];
                animationSet.TimeAccumulator += Time.deltaTime;

                if (Renderer.isVisible)
                    UpdateAnimationSet(animationSet);
            }
        }

        public void Reset() {
            for (int animationIndex = 0; animationIndex < animationSets.Length; ++animationIndex) {
                AnimationSet animationSet = animationSets[animationIndex];
                animationSet.TimeAccumulator = 0.0;
                animationSet.CurrentFrame = -1;
            }
        }

        private void UpdateAnimationSet(AnimationSet animationSet) {
            int[] MaterialIndices = animationSet.MaterialIndices;
            Material[] Frames = animationSet.Frames;
            WrapMode WrapMode = animationSet.WrapMode;
            float FPS = animationSet.FPS;

            if (Frames.Length <= 1 || MaterialIndices.Length == 0)
                return;

            int nextFrame = (int)(animationSet.TimeAccumulator * FPS);

            //Debug.Log(animationSet.TimeAccumulator);

            int currentFrame = animationSet.CurrentFrame;
            int previousFrame = currentFrame;
            bool loopComplete = false;

            if (WrapMode == AnimateMaterial.WrapMode.FirstFrame) {
                loopComplete = true;
                currentFrame = 0;
            } else if (WrapMode == AnimateMaterial.WrapMode.Repeat) {
                currentFrame = nextFrame % Frames.Length;
                loopComplete = previousFrame > currentFrame;
            } else if (WrapMode == AnimateMaterial.WrapMode.Once) {
                int lastFrame = Frames.Length - 1;
                currentFrame = Mathf.Min(nextFrame, lastFrame);
                loopComplete = currentFrame == lastFrame;
            } else if (WrapMode == AnimateMaterial.WrapMode.ReverseRepeat) {
                currentFrame = (Frames.Length - 1) - (nextFrame % Frames.Length);
                loopComplete = previousFrame < currentFrame;
            } else if (WrapMode == AnimateMaterial.WrapMode.ReverseOnce) { // Once only reverse
                int lastFrame = Frames.Length - 1;
                currentFrame = lastFrame - Mathf.Min((int)nextFrame, lastFrame);
                loopComplete = currentFrame == 0;
            } else { // if (WrapMode == AnimateMaterial.WrapMode.PingPong)
                int bounceFrame = nextFrame % (Frames.Length << 1);
                loopComplete = nextFrame > 0 && bounceFrame == 0;

                if (bounceFrame >= Frames.Length)
                    currentFrame = (Frames.Length - 1) - (bounceFrame % Frames.Length);
                else
                    currentFrame = nextFrame % Frames.Length;
            }

            if (currentFrame != previousFrame) {
                animationSet.CurrentFrame = currentFrame;

                Material[] sharedMaterials = Renderer.sharedMaterials;

                for (int i = 0; i < MaterialIndices.Length; ++i)
                    sharedMaterials[MaterialIndices[i]] = Frames[currentFrame];

                Renderer.sharedMaterials = sharedMaterials;

                DynamicGI.UpdateMaterials(Renderer);
            }

            if (loopComplete && LoopCompleted != null)
                LoopCompleted();
        }

        public void AddAnimation(int[] materialIndices, Material[] frames, WrapMode wrapMode, float fps) {
            AddAnimation(new AnimationSet {
                MaterialIndices = materialIndices,
                Frames = frames,
                WrapMode = wrapMode,
                FPS = fps,
                TimeAccumulator = 0.0,
                CurrentFrame = -1
            });
        }

        public void AddAnimation(int[] materialIndices, Material[] frames, TextureAnimation animationData) {
            float fps = 256f / animationData.FrameTime;

            WrapMode wrapMode = animationData.IsPingPong != 0 ? WrapMode.PingPong : WrapMode.Repeat;

            AddAnimation(new AnimationSet {
                MaterialIndices = materialIndices,
                Frames = frames,
                WrapMode = wrapMode,
                FPS = fps,
                TimeAccumulator = animationData.CurrentFrameTime / 1000f,
                CurrentFrame = animationData.CurrentFrameIndex
            });
        }

        public void AddAnimation(AnimationSet animationSet) {
            int index = animationSets.Length;
            Array.Resize(ref animationSets, index + 1);
            animationSets[index] = animationSet;
        }

        public void SetAnimation(int[] materialIndices, Material[] frames, WrapMode wrapMode, float fps) {
            animationSets = new AnimationSet[0];
            AddAnimation(materialIndices, frames, wrapMode, fps);
        }

        public void SetAnimation(AnimationSet animationSet) {
            animationSets = new AnimationSet[0];
            AddAnimation(animationSet);
        }

        public AnimationSet GetAnimationSet(int index = 0) {
            return animationSets[index];
        }

        [Serializable]
        public class AnimationSet {
            public int[] MaterialIndices;
            public Material[] Frames;
            public WrapMode WrapMode;
            public float FPS;
            public double TimeAccumulator;
            public int CurrentFrame;
        }


        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct TextureAnimation {
            public ushort FrameTime;
            public ushort CurrentFrameTime;
            public byte CurrentFrameIndex;
            public byte FrameCount;
            public byte IsPingPong;
        }
    }
}
