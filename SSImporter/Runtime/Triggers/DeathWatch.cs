using UnityEngine;

using System;
using SystemShock.Object;
using SystemShock.Resource;

namespace SystemShock.Triggers {
    public class DeathWatch : MonoBehaviour, IActionPermission {
        private ObjectInstance.Trigger trigger;
        private TriggerAction triggerable;

        private MessageBus messageBus;

        private MessageBusToken destroyingToken;

        private void Awake() {
            trigger = GetComponent<InstanceObjects.Trigger>().ClassData;
            triggerable = GetComponent<TriggerAction>();

            LevelInfo levelInfo = GameObject.FindObjectOfType<LevelInfo>();
        }

        private void Start() {
            messageBus = MessageBus.GetController();

            destroyingToken = messageBus.Receive<ObjectDestroying>(OnObjectDestroying);
        }

        public void OnDestroy() {
            messageBus.StopReceiving(destroyingToken);
        }

        private void OnObjectDestroying(ObjectDestroying msg) {
            uint combinedId = (uint)(trigger.ConditionValue << 16) | (uint)trigger.ConditionVariable;
            bool IsId = ((combinedId >> 24) & 0xFF) != 0;

            if(IsId) {
                ushort objectIndex = (ushort)(combinedId & 0x0FFF);

                if (triggerable != null && msg.Payload.ObjectId == objectIndex)
                    triggerable.Act();
            } else {
                uint Class = (combinedId >> 16) & 0xFF;
                uint Subclass = (combinedId >> 8) & 0xFF;
                uint Type = combinedId & 0xFF;

                if (triggerable != null &&
                    msg.Payload.ObjectInstance.Class == (ObjectClass)Class &&
                    msg.Payload.ObjectInstance.SubClass == (byte)Subclass &&
                    msg.Payload.ObjectInstance.Type == (byte)Type)
                    triggerable.Act();
            }
        }

        public bool CanAct() { return true; }
    }
}