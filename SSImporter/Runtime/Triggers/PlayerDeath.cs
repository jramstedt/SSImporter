using UnityEngine;

using SystemShock.Resource;

namespace SystemShock.Triggers {
    public class PlayerDeath : Null {
        private TriggerAction triggerable;

        protected override void Awake() {
            base.Awake();

            triggerable = GetComponent<TriggerAction>();

            // TODO Listen player death
        }
    }
}