using UnityEngine;

using SystemShock.Object;

namespace SystemShock.TriggerActions {
    public class Message : TriggerAction<ObjectInstance.Trigger.Message> {
        protected override void DoAct() {
            if(ActionData.Type == 0xFFFFFFFF) {
                // Shodan
            }
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