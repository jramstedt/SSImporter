using UnityEngine;

using SystemShock.Resource;

namespace SystemShock.Triggers {
    public class Continuous : Null {
        private ITriggerable triggerable;

        private double timeAccumulator;
        private double interval;

        protected override void Awake() {
            base.Awake();
            triggerable = GetComponent<ITriggerable>();
        }

        private void Start() {
            timeAccumulator = 0.0;
        }

        private void Update() {
            timeAccumulator += Time.deltaTime;
            /*
            if (timeAccumulator >= interval) {
                timeAccumulator = 0.0;

                triggerable.Trigger();
            }
            */
        }
    }
}