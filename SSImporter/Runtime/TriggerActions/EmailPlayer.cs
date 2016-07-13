using UnityEngine;

using SystemShock.Object;
using SystemShock.Resource;

namespace SystemShock.TriggerActions {
    public class EmailPlayer : Triggerable<ObjectInstance.Trigger.EmailPlayer> {

        protected override bool DoTrigger() {
            MessageBus.Send(new EmailReceived(ActionData.Message));

            return true;
        }
    }

    public sealed class EmailReceived : GenericMessage<ushort> {
        public EmailReceived(ushort emailId) : base(emailId) { }
    }
}