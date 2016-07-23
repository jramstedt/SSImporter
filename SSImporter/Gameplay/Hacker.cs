using UnityEngine;

using System;
using System.Linq;

using SystemShock.Object;
using SystemShock.TriggerActions;
using SystemShock.Resource;
using SystemShock.UserInterface;
using Item = SystemShock.InstanceObjects.Item;

namespace SystemShock.Gameplay {
    public class Hacker : MonoBehaviour {
        private MessageBus messageBus;

        public int Health;
        public int Radiation;
        public int Contamiantion;

        public int Power;
        public int PowerUsage;

        public byte[] HardwareVersion = new byte[15];
        public byte[] SoftwareVersion = new byte[15];

        public byte[] FullClips = new byte[15];
        public byte[] LooseAmmunition = new byte[15];

        public byte[] Patches = new byte[7];
        public byte[] Explosives = new byte[7];

        public WeaponState[] Weapons = new WeaponState[7];

        public ushort[] Inventory = new ushort[14];

        public SystemShockObject ObjectInHand;

        private ObjectFactory objectFactory;
        private LevelInfo levelInfo;

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

            objectFactory = ObjectFactory.GetController();
            levelInfo = objectFactory.LevelInfo;

            //Cursor.lockState = CursorLockMode.Locked;
        }

        private void UseObjectHandler(UseObjectMessage msg) {
            Interactable interactable = msg.Sender.GetComponent<Interactable>();

            SystemShockObjectProperties properties = msg.Sender.GetComponent<SystemShockObjectProperties>();

            if (((Flags)properties.Base.Flags & Flags.NoPickup) == 0) { // pickup
                if (ObjectInHand != null) {
                    messageBus.Send(new CantUseObjectMessage(msg.Sender.CombinedType));
                    return;
                }

                ObjectInHand = msg.Sender;
                ObjectInHand.gameObject.SetActive(false);
            } else if (((Flags)properties.Base.Flags & Flags.NoUse) == 0 && interactable != null) { // usable
                interactable.Interact();
            } else {
                messageBus.Send(new CantUseObjectMessage(msg.Sender.CombinedType));
            }
        }

        private void AddAccess(uint access) {
            uint accessCard = (int)ObjectClass.Item << 16 | 4 << 8 | 0;

            SystemShockObject ssobject = null;

            foreach (ushort objectId in Inventory) {
                if (!levelInfo.Objects.TryGetValue(objectId, out ssobject) || ssobject.CombinedType != accessCard)
                    ssobject = null;
            }

            if (ssobject == null) { // Create access card
                ssobject = objectFactory.Instantiate(new ObjectInstance() {
                    InUse = 1,
                    Class = ObjectClass.Item,
                    SubClass = 4,
                    Type = 0,
                    X = 0xFFFF,
                    Y = 0,
                    Z = 0,
                    Flags = (byte)InstanceFlags.LootNoRef
                });

                AddObjectToInventory(ssobject);
            }

            Item item = ssobject.GetComponent<Item>();
            ObjectInstance.Item classData = item.ClassData;
            ObjectInstance.Item.AccessCard accessCardData = classData.Data.Read<ObjectInstance.Item.AccessCard>();

            if((accessCardData.AccessBitmask & access) == access) {
                messageBus.Send(new InterfaceMessage(72)); // No new access gained
            } else {
                messageBus.Send(new AddAccessMessage((accessCardData.AccessBitmask | access) ^ access));
                accessCardData.AccessBitmask |= access;
                item.ClassData.Data = Extensions.Write(accessCardData);
            }
        }

        public bool HasAccess(uint access) {
            uint accessCard = (int)ObjectClass.Item << 16 | 4 << 8 | 0;

            SystemShockObject ssobject = null;

            foreach (ushort objectId in Inventory) {
                if (!levelInfo.Objects.TryGetValue(objectId, out ssobject) || ssobject.CombinedType != accessCard)
                    ssobject = null;
            }

            if (ssobject == null)
                return false;

            Item item = ssobject.GetComponent<Item>();
            ObjectInstance.Item classData = item.ClassData;
            ObjectInstance.Item.AccessCard accessCardData = classData.Data.Read<ObjectInstance.Item.AccessCard>();

            return (accessCardData.AccessBitmask & access) == access;
        }

        public void AddObjectToInventory(SystemShockObject ssobject) {
            ssobject.ObjectInstance.Flags |= (byte)InstanceFlags.LootNoRef;
            ssobject.ObjectInstance.X = 0xFFFF;
            ssobject.ObjectInstance.Y = 0;
            ssobject.ObjectInstance.Z = 0;
            ssobject.ObjectInstance.CrossReferenceTableIndex = 0; // TODO ObjectFactory should handle this

            Inventory.AddOrReplaceLast(ssobject.ObjectId);
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
