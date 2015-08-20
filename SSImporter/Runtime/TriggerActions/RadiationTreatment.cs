using UnityEngine;

using SystemShock.Object;

namespace SystemShock.TriggerActions {
    public class RadiationTreatment : Triggerable<ObjectInstance.Trigger.RadiationTreatment> {
        public override void Trigger() {
            if (!CanActivate)
                return;

        }
    }
}