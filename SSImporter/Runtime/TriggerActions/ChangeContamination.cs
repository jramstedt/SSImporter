using UnityEngine;

using SystemShock.Object;

namespace SystemShock.TriggerActions {
    public class ChangeContamination : Triggerable<ObjectInstance.Trigger.ChangeContamination> {
        private MessageBus messageBus;

        private void Start() {
            messageBus = MessageBus.GetController();
        }

        protected override bool DoTrigger() {
            int amount = ActionData.ChangeOperator == (ushort)ObjectInstance.Trigger.ChangeContamination.ChangeOperators.Add ? ActionData.DeltaValue : -ActionData.DeltaValue;

            if (ActionData.ContaminationType == (int)ObjectInstance.Trigger.ChangeContamination.ContaminationTypes.Radiation)
                messageBus.Send(new RadiationTreatmentMessage(amount));
            else
                messageBus.Send(new ContamiantionTreatmentMessage(amount));

            return true;
        }
    }

    public class RadiationTreatmentMessage : GenericMessage<int> {
        public RadiationTreatmentMessage(int amount) : base(amount) { }
    }

    public class ContamiantionTreatmentMessage : GenericMessage<int> {
        public ContamiantionTreatmentMessage(int amount) : base(amount) { }
    }
}