using UnityEngine;

using SystemShock.Object;

namespace SystemShock.TriggerActions {
    public class ResurrectPlayer : Triggerable<ObjectInstance.Trigger.Resurrect> {
        private MessageBus messageBus;

        private void Start() {
            messageBus = MessageBus.GetController();
        }

        protected override bool DoTrigger() {
            messageBus.Send(new ResurrectPlayerMessage());

            return true;
        }
    }

    public class ResurrectPlayerMessage : BaseMessage {
        public ResurrectPlayerMessage() { }
    }
}