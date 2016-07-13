using UnityEngine;

using SystemShock.Resource;

namespace SystemShock.Triggers {
    public class Ecology : Null {
        private ITriggerable triggerable;

        protected override void Awake() {
            base.Awake();

            triggerable = GetComponent<ITriggerable>();

            // TODO monitor ecology
            // TODO trigger if out of limits
        }
    }
}