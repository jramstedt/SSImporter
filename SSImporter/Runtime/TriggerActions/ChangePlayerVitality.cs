using UnityEngine;

using SystemShock.Object;

namespace SystemShock.TriggerActions {
    public class ChangePlayerVitality : Triggerable<ObjectInstance.Trigger.ChangePlayerVitality> {
        private MessageBus messageBus;

        private void Start() {
            messageBus = MessageBus.GetController();
        }

        protected override bool DoTrigger() {
            messageBus.Send(new ResurrectPlayerMessage());

            // TODO

            return true;
        }
    }

    public class ResurrectPlayerMessage : BaseMessage {
        public ResurrectPlayerMessage() { }
    }
}