using UnityEngine;

using SystemShock.Object;

namespace SystemShock.TriggerActions {
    public class ResurrectPlayer : Triggerable<ObjectInstance.Trigger.Resurrect> {
        public override void Trigger() {
            if (!CanActivate)
                return;

        }
    }
}