using UnityEngine;
using System.Collections;

namespace SystemShock {
    [RequireComponent(typeof(AnimateMaterial))]
    [RequireComponent(typeof(NoiseScreen))]
    public class ShodanScreen : MonoBehaviour {
        private AnimateMaterial AnimateMaterial;
        private NoiseScreen NoiseScreen;

        private float shodanCountdown;

        private void Awake() {
            AnimateMaterial = GetComponentInChildren<AnimateMaterial>();
            AnimateMaterial.LoopCompleted += OnEnable;

            NoiseScreen = GetComponentInChildren<NoiseScreen>();
        }

        private void OnEnable() {
            shodanCountdown = Random.Range(2f, 35f);
            AnimateMaterial.enabled = false;
            NoiseScreen.enabled = true;
        }

        private void Update() {
            shodanCountdown -= Time.deltaTime;

            if (shodanCountdown <= 0f) {
                AnimateMaterial.enabled = true; 
                NoiseScreen.enabled = false;
                return;
            }
        }
    }
}