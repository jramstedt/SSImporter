using UnityEngine;

using SystemShock.Resource;

namespace SystemShock.Triggers {
    public class Shodan : Null {
        private ITriggerable triggerable;

        protected override void Awake() {
            base.Awake();

            triggerable = GetComponent<ITriggerable>();

            // TODO wait for condition variable to be condition value then trigger
        }
    }
}