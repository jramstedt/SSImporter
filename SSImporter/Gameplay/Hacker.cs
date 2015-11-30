using UnityEngine;
using System.Collections;
using SystemShock.Object;
using SystemShock.TriggerActions;

namespace SystemShock.Gameplay {
    public class Hacker : MonoBehaviour {
        private MessageBus messageBus;

        public int Health;
        public int Radiation;
        public int Contamiantion;

        private void Start() {
            messageBus = MessageBus.GetController();

            messageBus.Receive<DamagePlayerMessage>(msg => {
                DamagePlayer(msg.Payload);
            });

            messageBus.Receive<RadiatePlayerMessage>(msg => {
                RadiatePlayer(msg.Payload);
            });

            messageBus.Receive<ContaminatePlayerMessage>(msg => {
                ContaminatePlayer(msg.Payload);
            });

            messageBus.Receive<RadiationTreatmentMessage>(msg => {
                Radiation = 0;
            });

            Cursor.lockState = CursorLockMode.Locked;
        }

        public void DamagePlayer(ushort amount) {
            Health -= amount;
            if (Health <= 0)
                messageBus.Send(new PlayerDeathMessage());
        }

        public void RadiatePlayer(ushort amount) {
            Radiation += amount;
        }

        public void ContaminatePlayer(ushort amount) {
            Contamiantion += amount;
        }
    }

    public class PlayerDeathMessage : BaseMessage { }

    public class DamagePlayerMessage : GenericBusMessage<ushort> {
        public DamagePlayerMessage(SystemShockObject sender, ushort amount) : base(sender, amount) { }
    }

    public class RadiatePlayerMessage : GenericBusMessage<ushort> {
        public RadiatePlayerMessage(SystemShockObject sender, ushort amount) : base(sender, amount) { }
    }

    public class ContaminatePlayerMessage : GenericBusMessage<ushort> {
        public ContaminatePlayerMessage(SystemShockObject sender, ushort amount) : base(sender, amount) { }
    }
}
