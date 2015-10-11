using UnityEngine;

using SystemShock.Object;
using SystemShock.Resource;

namespace SystemShock.TriggerActions {
    public class EmailPlayer : TriggerAction<ObjectInstance.Trigger.EmailPlayer> {
        private MessageBus messageBus;

        protected override void DoAct() {
            ObjectFactory.MessageBus.Send(new EmailReceived(ActionData.Message));
        }
    }

    public sealed class EmailReceived : GenericMessage<ushort> {
        public EmailReceived(ushort emailId) : base(emailId) { }
    }
}