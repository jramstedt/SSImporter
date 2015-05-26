using UnityEngine;
using System.Collections;
using SystemShock.Resource;
using System;

namespace SystemShock {
    [ExecuteInEditMode]
    public class Door : StateMachine<Door.DoorState>, ITriggerable {
        public enum DoorState {
            Closed,
            Closing,
            Open,
            Opening
        }

        private Renderer Renderer;
        private Collider Collider;

        private int propertyId;
        private MaterialPropertyBlock propertyBlock;

        private double timeAccumulator;
        
        public float FPS = 10f;
        public SpriteDefinition[] Frames;

        public event Action OnOpened;
        public event Action OnClosed;

        private void Awake() {
            Renderer = GetComponent<Renderer>();
            Collider = GetComponent<Collider>();
            propertyBlock = new MaterialPropertyBlock();

            propertyId = Shader.PropertyToID(@"_MainTex_ST");

            State = DoorState.Closed;
        }

        private void Start() {
            UpdateFrame();
        }

        private void Update() {
            if (State == DoorState.Closed || State == DoorState.Open)
                return;

            timeAccumulator += Time.deltaTime;

            if (!Renderer.isVisible)
                return;

            float frameLength = 1f / FPS;

            while (timeAccumulator >= frameLength) {
                timeAccumulator -= frameLength;

                if (State == DoorState.Opening)
                    ++CurrentFrame;
                else if (State == DoorState.Closing)
                    --CurrentFrame;
            }

            if (State == DoorState.Opening && currentFrame >= (Frames.Length - 1))
                State = DoorState.Open;
            else if (State == DoorState.Closing && currentFrame <= 0)
                State = DoorState.Closed;
        }

        private void UpdateFrame() {
            int clampedFrame = Mathf.Clamp(currentFrame, 0, Frames.Length - 1);

            SpriteDefinition currentSprite = Frames[currentFrame];
            Rect rect = currentSprite.Rect;

            Renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetVector(propertyId, new Vector4(rect.width, rect.height, rect.x, rect.y));
            Renderer.SetPropertyBlock(propertyBlock);

            DynamicGI.UpdateMaterials(Renderer);
        }

        public void Trigger() {
            if(State == DoorState.Closed || State == DoorState.Closing)
                State = DoorState.Opening;
            else if(State == DoorState.Open || State == DoorState.Opening)
                State = DoorState.Closing;
        }

        protected override void ShowState(Door.DoorState previousState) {
            if (State == DoorState.Open) {
                Collider.isTrigger = true;
            } else if (State == DoorState.Closed) {
                Collider.isTrigger = false;
            } else if (State == DoorState.Opening || State == DoorState.Closing) {
                timeAccumulator = 0.0;
            }
        }

        protected override void HideState(Door.DoorState nextState) { }

        [SerializeField, HideInInspector]
        private int currentFrame = -1;
        public int CurrentFrame {
            get { return currentFrame; }
            set {
                int clampedValue = Mathf.Clamp(value, 0, Frames.Length - 1);
                if (currentFrame != clampedValue) {
                    currentFrame = clampedValue;
                    UpdateFrame();
                }
            }
        }

        private void OnMouseDown() {
            Trigger();
        }
    }
}