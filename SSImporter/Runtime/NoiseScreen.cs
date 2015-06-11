using UnityEngine;
using System.Collections;

namespace SystemShock {
    public class NoiseScreen : MonoBehaviour {
        private Material Material;
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

        [HideInInspector]
        public int[] MaterialIndices;

        private void Awake() {
            Renderer = GetComponentInChildren<Renderer>();

            if (NoiseTexture == null)
                SetupMaterial();
            else if (NoiseTexture.width != Resolution || NoiseTexture.height != Resolution)
                NoiseTexture.Resize(Resolution, Resolution);

            Hash = new byte[256];
            for (int i = 0; i < 256; ++i)
                Hash[i] = (byte)(Random.value * 256);
        }

        public Material SetupMaterial() {
            NoiseTexture = new Texture2D(Resolution, Resolution, TextureFormat.RGB24, false, true);
            NoiseTexture.name = @"Noise";
            NoiseTexture.anisoLevel = 9;

            Material = new Material(Shader.Find(@"Standard"));
            Material.color = Color.black;
            Material.SetFloat(@"_Glossiness", 0.75f); // Add little gloss to screens
            Material.SetTexture(@"_EmissionMap", NoiseTexture);
            Material.SetColor(@"_EmissionColor", Color.white);
            Material.EnableKeyword(@"_EMISSION");

            return Material;
        }

        private void OnEnable() {
            Material[] sharedMaterials = Renderer.sharedMaterials;

            for (int i = 0; i < MaterialIndices.Length; ++i)
                sharedMaterials[MaterialIndices[i]] = Material;

            Renderer.sharedMaterials = sharedMaterials;

            DynamicGI.UpdateMaterials(Renderer);
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