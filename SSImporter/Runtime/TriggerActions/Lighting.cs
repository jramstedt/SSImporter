using UnityEngine;

using SystemShock.Object;

namespace SystemShock.TriggerActions {
    public class Lighting : Triggerable<ObjectInstance.Trigger.Lighting> {
        protected override bool DoTrigger() { return true; }
    }
}