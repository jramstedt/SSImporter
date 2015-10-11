using UnityEngine;

using SystemShock.Resource;
using SystemShock.Gameplay;

namespace SystemShock.Triggers {
    public class PlayerDeath : Null {
        private TriggerAction triggerable;
        private MessageBus messageBus;

        private MessageBusToken playerDeathMessageToken;

        protected override void Awake() {
            base.Awake();

            triggerable = GetComponent<TriggerAction>();
        }

        private void Start() {
            messageBus = MessageBus.GetController();
            playerDeathMessageToken = messageBus.Receive<PlayerDeathMessage>((msg) => {
                if (triggerable != null)
                    triggerable.Act();
            });
        }

        private void OnDestroy() {
            messageBus.StopReceiving(playerDeathMessageToken);
        }
    }
}