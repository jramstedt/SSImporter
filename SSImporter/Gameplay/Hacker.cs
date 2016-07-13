using UnityEngine;

using SystemShock.Object;
using SystemShock.TriggerActions;
using System;
using SystemShock.InstanceObjects;
using SystemShock.Resource;
using Item = SystemShock.InstanceObjects.Item;
using SystemShock.UserInterface;

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

        public SystemShockObject ObjectInHand;

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

            messageBus.Receive<ChargePlayerMessage>(msg => {
                ChargePlayer(msg.Payload);
            });

            messageBus.Receive<UseObjectMessage>(UseObjectHandler);

            //Cursor.lockState = CursorLockMode.Locked;
        }

        private void UseObjectHandler(UseObjectMessage msg) {
            Interactable interactable = msg.Sender.GetComponent<Interactable>();
            if (interactable != null) {
                interactable.Interact();
                return;
            }

            if (ObjectInHand != null) {
                messageBus.Send(new CantUseObjectMessage(msg.Sender.CombinedId));
                return;
            }

            SystemShockObjectProperties properties = msg.Sender.GetComponent<SystemShockObjectProperties>();

            if (((Flags)properties.Base.Flags & Flags.Interactable) != Flags.Interactable ||
                ((Flags)properties.Base.Flags & Flags.NoPickup) == Flags.NoPickup) {
                messageBus.Send(new CantUseObjectMessage(msg.Sender.CombinedId));
                return;
            }

            ObjectInHand = msg.Sender;
            ObjectInHand.gameObject.SetActive(false);
        }

        private void AddAccess() {
            // 1. Check if inventory has current access item
            // 2. Create access item, Add to inventory as first item.
            // 3. Check access bit
            // 4. Add bit or send message
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
            Debug.Log("Charged! Power: " + Power);
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

    public class UseObjectMessage : BusMessage {
        public UseObjectMessage(SystemShockObject sender) : base(sender) { }
    }
}
