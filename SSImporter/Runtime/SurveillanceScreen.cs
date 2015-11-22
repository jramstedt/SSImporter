using UnityEngine;

using SystemShock.Object;
using SystemShock.Resource;

namespace SystemShock {
    public class SurveillanceScreen : Screen {
        private MessageBus messageBus;
        private ObjectFactory objectFactory;

        public Camera Camera;
        public SystemShockObject DeathwatchObject;

        private MessageBusToken deathWatchToken;

        protected override void Awake() {
            base.Awake();

            messageBus = MessageBus.GetController();
            objectFactory = ObjectFactory.GetController();

            deathWatchToken = messageBus.Receive<ObjectDestroying>(msg => {
                if (msg.Payload == DeathwatchObject) ShowNoise();
            });
        }

        public void OnDestroy() {
            messageBus.StopReceiving(deathWatchToken);
        }

        private void Start() {
            OnEnable();
        }

        protected override void OnEnable() {
            base.OnEnable();

            SystemShock.InstanceObjects.Decoration decoration = GetComponent<SystemShock.InstanceObjects.Decoration>();
            ObjectInstance.Decoration.MaterialOverride materialOverride = decoration.ClassData.Data.Read<ObjectInstance.Decoration.MaterialOverride>();
            LevelInfo.SurveillanceCamera surveillanceCamera = objectFactory.LevelInfo.SurveillanceCameras[materialOverride.StartFrameIndex & 0x07];

            if (surveillanceCamera != null && surveillanceCamera.Camera != null) {
                Camera = surveillanceCamera.Camera;
                DeathwatchObject = surveillanceCamera.DeathwatchObject;

                if (Material != null)
                    Material.SetTexture(@"_EmissionMap", Camera.targetTexture);
            } else { // Override with noise.
                ShowNoise();
            }
        }

        private void Update() {
            if (!Renderer.isVisible)
                return;

            Camera.Render();

            DynamicGI.UpdateMaterials(Renderer);
        }

        private void ShowNoise() {
            this.enabled = false;
            messageBus.StopReceiving(deathWatchToken);

            NoiseScreen noiseScreen = gameObject.AddComponent<NoiseScreen>();
            noiseScreen.SetupMaterial(ref Material, MaterialIndices);
        }
    }
}