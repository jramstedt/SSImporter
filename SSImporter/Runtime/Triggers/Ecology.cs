using UnityEngine;

using SystemShock.Resource;

namespace SystemShock.Triggers {
    public class Ecology : Null {
        private TriggerAction triggerable;

        protected override void Awake() {
            base.Awake();

            triggerable = GetComponent<TriggerAction>();

            // TODO monitor ecology
            // TODO trigger if out of limits
        }
    }
}