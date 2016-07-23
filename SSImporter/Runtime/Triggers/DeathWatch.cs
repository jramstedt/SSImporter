using UnityEngine;

using System;
using SystemShock.Object;
using SystemShock.Resource;

namespace SystemShock.Triggers {
    public class DeathWatch : MonoBehaviour, IActionPermission {
        private ObjectInstance.Trigger trigger;
        private ITriggerable triggerable;

        private MessageBus messageBus;

        private MessageBusToken destroyingToken;

        private void Awake() {
            trigger = GetComponent<InstanceObjects.Trigger>().ClassData;
            triggerable = GetComponent<ITriggerable>();
        }

        private void Start() {
            messageBus = MessageBus.GetController();

            destroyingToken = messageBus.Receive<ObjectDestroying>(OnObjectDestroying);
        }

        public void OnDestroy() {
            messageBus.StopReceiving(destroyingToken);
        }

        private void OnObjectDestroying(ObjectDestroying msg) {
            uint combinedType = (uint)(trigger.ConditionValue << 16) | (uint)trigger.ConditionVariable;
            bool IsId = ((combinedType >> 24) & 0xFF) != 0;

            if(IsId) {
                ushort objectIndex = (ushort)(combinedType & 0x0FFF);

                if (triggerable != null && msg.Payload.ObjectId == objectIndex)
                    triggerable.Trigger();
            } else {
                uint Class = (combinedType >> 16) & 0xFF;
                uint Subclass = (combinedType >> 8) & 0xFF;
                uint Type = combinedType & 0xFF;

                if (triggerable != null &&
                    msg.Payload.ObjectInstance.Class == (ObjectClass)Class &&
                    msg.Payload.ObjectInstance.SubClass == (byte)Subclass &&
                    msg.Payload.ObjectInstance.Type == (byte)Type)
                    triggerable.Trigger();
            }
        }

        public bool CanAct() { return true; }
    }
}