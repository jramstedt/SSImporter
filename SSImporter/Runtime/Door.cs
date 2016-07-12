using UnityEngine;

using System;

using DoorAndGrating = SystemShock.InstanceObjects.DoorAndGrating;
using SystemShock.Resource;
using SystemShock.UserInterface;

namespace SystemShock {
    [ExecuteInEditMode]
    public class Door : StateMachine<Door.DoorState>, IActionPermission, IRaycastFilter {
        public enum DoorState {
            Closed,
            Closing,
            Open,
            Opening
        }

        private MessageBus messageBus;

        private DoorAndGrating doorAndGrating;
        private Renderer Renderer;
        private Collider Collider;

        private int propertyId;
        private MaterialPropertyBlock propertyBlock;

        private double timeAccumulator;

        public Door PairedDoor;

        public float FPS = 10f;
        public SpriteDefinition[] Frames;

        public event Action OnOpened;
        public event Action OnClosed;

        // Access 255 = shodan security

        protected virtual void Awake() {
            messageBus = MessageBus.GetController();

            Renderer = GetComponent<Renderer>();
            Collider = GetComponent<Collider>();
            propertyBlock = new MaterialPropertyBlock();

            propertyId = Shader.PropertyToID(@"_MainTex_ST");

            State = DoorState.Closed;
        }

        private void Start() {
            doorAndGrating = GetComponent<DoorAndGrating>();

            if(doorAndGrating.ClassData.ObjectToTrigger != 0 && PairedDoor == null)
                PairedDoor = ObjectFactory.GetController().Get<Door>(doorAndGrating.ClassData.ObjectToTrigger);

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
        }

        private void UpdateFrame() {
            SpriteDefinition currentSprite = Frames[CurrentFrame];
            Rect rect = currentSprite.UVRect;

            if (propertyBlock == null)
                propertyBlock = new MaterialPropertyBlock();

            Renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetVector(propertyId, new Vector4(rect.width, rect.height, rect.x, rect.y));
            Renderer.SetPropertyBlock(propertyBlock);

            DynamicGI.UpdateMaterials(Renderer);

            if (CurrentFrame >= (Frames.Length - 1))
                State = DoorState.Open;
            else if (CurrentFrame <= 0)
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

            // Clear lock status
            doorAndGrating.ClassData.Lock = 0;
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

        public byte CurrentFrame {
            get { return doorAndGrating.State; }
            set {
                byte clampedValue = (byte)Mathf.Clamp(value, 0, Frames.Length - 1);
                if (doorAndGrating.State != clampedValue) {
                    doorAndGrating.State = clampedValue;
                    UpdateFrame();
                }
            }
        }

        public bool CanAct() {
            if (doorAndGrating.ClassData.Lock == 0) // TODO check access cards
                return true;

            if (doorAndGrating.ClassData.AccessRequired == 255) {
                messageBus.Send(new ShodanSecurityMessage(byte.MaxValue));
            } else if(doorAndGrating.ClassData.AccessRequired != 0) {
                // TODO Access card required
            } else {
                messageBus.Send(new InterfaceMessage((byte)(doorAndGrating.ClassData.LockMessage + 7u)));
            }

            return false;
        }

        public bool TestRaycast(RaycastHit hit) {
            if (!Renderer.isVisible)
                return false;

            Vector3 localPoint = transform.InverseTransformPoint(hit.point);
            Vector2 normalizedCoordinates = new Vector2(Mathf.InverseLerp(-0.5f, 0.5f, localPoint.x), Mathf.InverseLerp(-0.5f, 0.5f, localPoint.y));
            Vector2 textureCoordinates = Rect.NormalizedToPoint(Frames[CurrentFrame].UVRect, normalizedCoordinates);

            Texture2D texture = (Renderer.sharedMaterial ?? Renderer.material).mainTexture as Texture2D;
            return texture.GetPixelBilinear(textureCoordinates.x, textureCoordinates.y).a >= 0.5f;
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