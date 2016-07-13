using UnityEngine;

using SystemShock.Object;

namespace SystemShock.TriggerActions {
    public class RadiationTreatment : Triggerable<ObjectInstance.Trigger.RadiationTreatment> {
        private MessageBus messageBus;

        private void Start() {
            messageBus = MessageBus.GetController();
        }

        protected override bool DoTrigger() {
            messageBus.Send(new RadiationTreatmentMessage());

            return true;
        }
    }

    public class RadiationTreatmentMessage : BaseMessage {
        public RadiationTreatmentMessage() : base() { }
    }
}