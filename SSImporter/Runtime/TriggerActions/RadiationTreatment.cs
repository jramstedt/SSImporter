using UnityEngine;

using SystemShock.Object;

namespace SystemShock.TriggerActions {
    public class RadiationTreatment : TriggerAction<ObjectInstance.Trigger.RadiationTreatment> {
        private MessageBus messageBus;

        private void Start() {
            messageBus = MessageBus.GetController();
        }

        protected override void DoAct() {
            messageBus.Send(new RadiationTreatmentMessage());
        }
    }

    public class RadiationTreatmentMessage : BaseMessage {
        public RadiationTreatmentMessage() : base() { }
    }
}