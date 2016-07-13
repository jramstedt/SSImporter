using UnityEngine;

using SystemShock.Object;

namespace SystemShock.TriggerActions {
    public class Effect : Triggerable<ObjectInstance.Trigger.Effect> {
        private MessageBus messageBus;

        private void Start() {
            messageBus = MessageBus.GetController();
        }

        protected override bool DoTrigger() {
            messageBus.Send(new ShowEffect());

            return true;
        }
    }

    public sealed class ShowEffect : BaseMessage { }
}