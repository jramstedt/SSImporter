using UnityEngine;
using System.Collections;
using SystemShock.Resource;
using System;

namespace SystemShock {
    [ExecuteInEditMode]
    public class ToggleSprite : StateMachine<ToggleSprite.ToggleState> {
        public enum ToggleState {
            Closed,
            Open,
            Active
        }

        private Renderer Renderer;

        private int propertyId;
        private MaterialPropertyBlock propertyBlock;

        private TriggerAction Triggerable;

        public SpriteDefinition[] Frames;

        public event Action OnClosed;
        public event Action OnOpened;
        public event Action OnActivated;

        protected virtual void Awake() {
            Renderer = GetComponent<Renderer>();
            Triggerable = GetComponent<TriggerAction>();

            propertyId = Shader.PropertyToID(@"_MainTex_ST");
            propertyBlock = new MaterialPropertyBlock();
        }

        private void Start() {
            UpdateFrame();
        }

        private void UpdateFrame() {
            if (Frames == null)
                return;

            SpriteDefinition currentSprite = Frames[(int)State];
            Rect rect = currentSprite.Rect;

            if(propertyBlock == null)
                propertyBlock = new MaterialPropertyBlock();

            Renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetVector(propertyId, new Vector4(rect.width, rect.height, rect.x, rect.y));
            Renderer.SetPropertyBlock(propertyBlock);

            DynamicGI.UpdateMaterials(Renderer);
        }

        /*
        public override void Trigger() {
            if (State == ToggleState.Closed)
                State = ToggleState.Open;
            else if (State == ToggleState.Open)
                State = ToggleState.Active;
        }
        */

        protected override void ShowState(ToggleState previousState) {
            UpdateFrame();

            if (State == ToggleState.Closed) {
                if (OnClosed != null)
                    OnClosed();
            } else if (State == ToggleState.Open) {
                if (OnOpened != null)
                    OnOpened();
            } else if (State == ToggleState.Active) {
                if (OnActivated != null)
                    OnActivated();
            }
        }

        protected override void HideState(ToggleState nextState) { }

        public void SetFrame(int frame) {
            State = (ToggleState)frame;
        }

        private void OnMouseDown() {
            if (State == ToggleState.Closed)
                State = ToggleState.Open;
            else if (State == ToggleState.Open)
                State = ToggleState.Active;
            else if (State == ToggleState.Active)
                State = ToggleState.Open;

            if (State == ToggleState.Open || State == ToggleState.Active || Triggerable != null)
                Triggerable.Act();
        }
    }
}