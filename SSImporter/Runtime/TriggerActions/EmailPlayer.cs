using UnityEngine;

using SystemShock.Object;
using SystemShock.Resource;

namespace SystemShock.TriggerActions {
    public class EmailPlayer : TriggerAction<ObjectInstance.Trigger.EmailPlayer> {

        protected override void DoAct() {
            MessageBus.Send(new EmailReceived(ActionData.Message));
        }
    }

    public sealed class EmailReceived : GenericMessage<ushort> {
        public EmailReceived(ushort emailId) : base(emailId) { }
    }
}