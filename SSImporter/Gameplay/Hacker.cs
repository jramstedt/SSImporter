using UnityEngine;

using SystemShock.Object;
using SystemShock.TriggerActions;
using System;

namespace SystemShock.Gameplay {
    public class Hacker : MonoBehaviour {
        private MessageBus messageBus;

        public int Health;
        public int Radiation;
        public int Contamiantion;

        public int Power;
        public int PowerUsage;

        public byte[] HardwareVersion;
        public byte[] SoftwareVersion;

        public byte[] FullClips;
        public byte[] LooseAmmunition;

        public byte[] Patches;
        public byte[] Explosives;

        public WeaponState[] Weapons;

        public ushort[] Inventory;

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

            //Cursor.lockState = CursorLockMode.Locked;
        }

        public void OnApplicationFocus(bool focus) {
            //Cursor.lockState = focus ? CursorLockMode.Locked : CursorLockMode.None;
        }

        public void DamagePlayer(ushort amount) {
            Health -= amount;
            Debug.Log("Damaged! Health: " + Health);

            if (Health <= 0)
                messageBus.Send(new PlayerDeathMessage());
        }

        public void RadiatePlayer(ushort amount) {
            Radiation = Mathf.Max(Radiation, amount);
            Debug.Log("Radiated! Radiation: " + Radiation);
        }

        public void ContaminatePlayer(ushort amount) {
            Contamiantion = Mathf.Max(Contamiantion, amount);
            Debug.Log("Contaminated! Contamination: " + Contamiantion);
        }

        public void ChargePlayer(ushort amount) {
            Power += amount;
        }

        [Serializable]
        public struct WeaponState {
            byte Subclass;
            byte Type;
            byte Rounds;
            byte Ammunition;
            byte Condition;
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

    public class ChargePlayerMessage : GenericBusMessage<ushort> {
        public ChargePlayerMessage(SystemShockObject sender, ushort payload) : base(sender, payload) { }
    }
}
