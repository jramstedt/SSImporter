using UnityEngine;

using SystemShock.Resource;

namespace SystemShock.Triggers {
    public class Continuous : MonoBehaviour {
        private ITriggerable triggerable;

        private double timeAccumulator;
        private double interval;

        private void Awake() {
            triggerable = GetComponent<ITriggerable>();
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