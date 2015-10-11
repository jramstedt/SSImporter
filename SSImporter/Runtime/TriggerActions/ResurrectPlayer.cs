using UnityEngine;

using SystemShock.Object;

namespace SystemShock.TriggerActions {
    public class ResurrectPlayer : TriggerAction<ObjectInstance.Trigger.Resurrect> {
        private MessageBus messageBus;

        private void Start() {
            messageBus = MessageBus.GetController();
        }

        protected override void DoAct() {
            messageBus.Send(new ResurrectPlayerMessage());
        }
    }

    public class ResurrectPlayerMessage : BaseMessage {
        public ResurrectPlayerMessage() { }
    }
}