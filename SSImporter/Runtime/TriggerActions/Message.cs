using UnityEngine;

using SystemShock.Object;

namespace SystemShock.TriggerActions {
    public class Message : Triggerable<ObjectInstance.Trigger.Message> {
        protected override bool DoTrigger() {
            if (ActionData.BackgroundImage == 0xFFFFFFFF) {
                // Shodan
            } else if (ActionData.BackgroundImage == 0xFFFFFFFE) {
                // Diego
            }

            return true;
        }
    }

    public sealed class MessageToPlayer : BaseMessage {
        public readonly uint Type;
        public readonly uint MessageId;

        public MessageToPlayer(uint type, uint messageId) : base() {
            Type = type;
            MessageId = messageId;
        }
    }
}