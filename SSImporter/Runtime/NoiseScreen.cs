using UnityEngine;
using System.Collections;

namespace SystemShock {
    public class NoiseScreen : MonoBehaviour {
        private Texture2D NoiseTexture;
        private byte[] Hash;

        private Renderer Renderer;

        [Range(2, 1024)]
        public int Resolution = 128;

        [Range(1f, 256f)]
        public float Frequency = 32f;

        public float FPS = 30f;

        private float Step;

        private uint currentFrame;

        private double timeAccumulator;

        private void Awake() {
            Renderer = GetComponentInChildren<Renderer>();

            if (NoiseTexture == null) {
                NoiseTexture = new Texture2D(Resolution, Resolution, TextureFormat.RGB24, false, true);
                NoiseTexture.name = @"Noise";
                NoiseTexture.anisoLevel = 9;
            } else if (NoiseTexture.width != Resolution || NoiseTexture.height != Resolution) {
                NoiseTexture.Resize(Resolution, Resolution);
            }

            Hash = new byte[256];
            for (int i = 0; i < 256; ++i)
                Hash[i] = (byte)(Random.value * 256);
        }

        private void OnEnable() {
            Material material = Renderer.material; // Should we create material? 
            material.SetTexture(@"_EmissionMap", NoiseTexture);
            material.SetColor(@"_EmissionColor", Color.white);
            material.EnableKeyword(@"_EMISSION");
            Renderer.material = material;
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

                DynamicGI.UpdateMaterials(Renderer);
            }
        }

        private float Value(Vector2 uv) {
            uv *= Frequency;
            return Hash[Hash[(int)uv.x * currentFrame & 0xFF] + (int)uv.y * currentFrame & 0xFF] / 256f;
        }
    }
}