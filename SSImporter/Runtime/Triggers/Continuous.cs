using UnityEngine;

using SystemShock.Resource;

namespace SystemShock.Triggers {
    public class Continuous : Null {
        private TriggerAction triggerable;

        private double timeAccumulator;
        private double interval;

        protected override void Awake() {
            base.Awake();
            triggerable = GetComponent<TriggerAction>();
        }

        private void Start() {
            timeAccumulator = 0.0;
        }

        private void Update() {
            timeAccumulator += Time.deltaTime;
            /*
            if (timeAccumulator >= interval) {
                timeAccumulator = 0.0;

                triggerable.Act();
            }
            */
        }
    }
}