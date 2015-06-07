using UnityEngine;

using SystemShock.Resource;

namespace SystemShock.Triggers {
    public class Continuous : MonoBehaviour {
        private Triggerable triggerable;

        private double timeAccumulator;
        private double interval;

        private void Awake() {
            triggerable = GetComponent<Triggerable>();
        }

        private void Start() {
            timeAccumulator = 0.0;
        }

        private void Update() {
            timeAccumulator += Time.deltaTime;

            if (timeAccumulator >= interval) {
                timeAccumulator = 0.0;

                triggerable.Trigger();
            }
        }
    }
}