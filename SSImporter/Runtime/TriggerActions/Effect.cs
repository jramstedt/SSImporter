using UnityEngine;

using SystemShock.Object;

namespace SystemShock.TriggerActions {
    public class Effect : TriggerAction<ObjectInstance.Trigger.Effect> {
        private MessageBus messageBus;

        private void Start() {
            messageBus = MessageBus.GetController();
        }

        protected override void DoAct() {
            messageBus.Send(new ShowEffect());
        }
    }

    public sealed class ShowEffect : BaseMessage { }
}