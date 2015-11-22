using UnityEngine;
using System.Collections;

namespace SystemShock {
    public class NoiseScreen : Screen {
        private Texture2D NoiseTexture;

        private byte[] Hash;

        [Range(2, 1024)]
        public int Resolution = 128;

        [Range(1f, 256f)]
        public float Frequency = 32f;

        public float FPS = 30f;

        private float Step;

        private uint currentFrame;

        private double timeAccumulator;

        protected override void Awake() {
            base.Awake();

            NoiseTexture = new Texture2D(Resolution, Resolution, TextureFormat.RGB24, false, true);
            NoiseTexture.name = @"Noise";
            NoiseTexture.anisoLevel = 9;

            Hash = new byte[256];
            for (int i = 0; i < 256; ++i)
                Hash[i] = (byte)(Random.value * 256);
        }

        public void OnDestroy() {
            Destroy(NoiseTexture);
        }

        private void Start() {
            OnEnable();
        }

        protected override void OnEnable() {
            base.OnEnable();

            if (NoiseTexture.width != Resolution || NoiseTexture.height != Resolution)
                NoiseTexture.Resize(Resolution, Resolution);

            if(Material != null)
                Material.SetTexture(@"_EmissionMap", NoiseTexture);
        }

        private void Update() {
            timeAccumulator += Time.deltaTime;

            if (!Renderer.isVisible)
                return;

            if (NoiseTexture.width != Resolution || NoiseTexture.height != Resolution)
                NoiseTexture.Resize(Resolution, Resolution);

            uint nextFrame = (uint)(timeAccumulator * FPS);

            if (nextFrame != currentFrame) {
                currentFrame = nextFrame;

                Step = 1f / Resolution;
                Vector2 uv = new Vector2();
                for (int y = 0; y < Resolution; ++y) {
                    for (int x = 0; x < Resolution; ++x) {
                        uv.Set(x * Step, y * Step);

                        NoiseTexture.SetPixel(x, y, Color.white * Value(uv));
                    }
                }

                NoiseTexture.Apply();
            }
        }

        private float Value(Vector2 uv) {
            uv *= Frequency;
            return Hash[Hash[(int)uv.x * currentFrame & 0xFF] + (int)uv.y * currentFrame & 0xFF] / 256f;
        }
    }
}