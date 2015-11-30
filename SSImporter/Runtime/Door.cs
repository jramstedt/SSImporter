﻿using UnityEngine;

using System;
using System.Collections;

using SystemShock.Object;
using SystemShock.Resource;

namespace SystemShock {
    [ExecuteInEditMode]
    public class Door : StateMachine<Door.DoorState> {
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

        public Door PairedDoor;
        public ObjectInstance.DoorAndGrating ClassData;

        public float FPS = 10f;
        public SpriteDefinition[] Frames;

        public event Action OnOpened;
        public event Action OnClosed;

        // Access 255 = shodan security

        protected virtual void Awake() {
            Renderer = GetComponent<Renderer>();
            Collider = GetComponent<Collider>();
            propertyBlock = new MaterialPropertyBlock();

            propertyId = Shader.PropertyToID(@"_MainTex_ST");

            State = DoorState.Closed;
        }

        private void Start() {
            ObjectFactory objectFactory = ObjectFactory.GetController();

            //StringLibrary stringLibrary = StringLibrary.GetLibrary(@"cybstrng.res");
            //CyberString decalWords = stringLibrary.GetStrings(KnownChunkId.StateMessages);

            //if (ClassData.Lock != 0)
            //    Debug.Log("DOOR MESSAGE " + decalWords[(uint)ClassData.LockMessage + 0x07]);

            if(ClassData.ObjectToTrigger != 0 && PairedDoor == null)
                PairedDoor = objectFactory.Get<Door>(ClassData.ObjectToTrigger);

            UpdateFrame();
        }

        private void Reset() {
            Start();
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
        }

        private void UpdateFrame() {
            SpriteDefinition currentSprite = Frames[currentFrame];
            Rect rect = currentSprite.Rect;

            if (propertyBlock == null)
                propertyBlock = new MaterialPropertyBlock();

            Renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetVector(propertyId, new Vector4(rect.width, rect.height, rect.x, rect.y));
            Renderer.SetPropertyBlock(propertyBlock);

            DynamicGI.UpdateMaterials(Renderer);

            if (currentFrame >= (Frames.Length - 1))
                State = DoorState.Open;
            else if (currentFrame <= 0)
                State = DoorState.Closed;
        }

        public void Open() {
            if (State == DoorState.Closed || State == DoorState.Closing)
                State = DoorState.Opening;

            if (PairedDoor != null && PairedDoor.State != State)
                PairedDoor.Activate();
        }

        public void Close() {
            if (State == DoorState.Open || State == DoorState.Opening)
                State = DoorState.Closing;

            if (PairedDoor != null && PairedDoor.State != State)
                PairedDoor.Activate();
        }

        public void Activate() {
            if (State == DoorState.Closed || State == DoorState.Closing)
                Open();
            else if (State == DoorState.Open || State == DoorState.Opening)
                Close();

            if (PairedDoor != null && PairedDoor.State != State)
                PairedDoor.Activate();
        }

        protected override void ShowState(DoorState previousState) {
            if (State == DoorState.Open) {
                Collider.isTrigger = true;

                if (OnOpened != null)
                    OnOpened();
            } else if (State == DoorState.Closed) {
                Collider.isTrigger = false;

                if (OnClosed != null)
                    OnClosed();
            } else if (State == DoorState.Opening || State == DoorState.Closing) {
                timeAccumulator = 0.0;
            }
        }

        protected override void HideState(DoorState nextState) { }

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

#if UNITY_EDITOR
        private void OnDrawGizmos() {
            if (PairedDoor != null)
                Gizmos.DrawLine(transform.position, PairedDoor.transform.position);
        }

        private void OnDrawGizmosSelected() {

        }
#endif
    }
}