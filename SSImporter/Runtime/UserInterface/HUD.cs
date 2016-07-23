using UnityEngine;
using System.Collections;
using SystemShock.Resource;
using SystemShock.Object;

namespace SystemShock.UserInterface {
    public class HUD : MonoBehaviour {
        private MessageBus messageBus;
        private StringLibrary stringLibrary;
        private ObjectPropertyLibrary objectPropertyLibrary;

        private void Awake() {
            messageBus = MessageBus.GetController();
            stringLibrary = StringLibrary.GetLibrary();
            objectPropertyLibrary = ObjectPropertyLibrary.GetLibrary();

            messageBus.Receive<TrapMessage>(msg => Debug.LogFormat("Trap Message: {0}", stringLibrary.GetResource(KnownChunkId.TrapMessages)[msg.Payload]));
            messageBus.Receive<InterfaceMessage>(msg => Debug.LogFormat("Interface Message: {0}", stringLibrary.GetResource(KnownChunkId.UserInterfaceMessages)[msg.Payload]));
            messageBus.Receive<ShodanSecurityMessage>(msg => Debug.LogFormat("Shodan Security: {0}", msg.Payload));
            messageBus.Receive<ItemInspectionMessage>(msg => {
                string description = string.Empty;

                IDescriptionProvider customDescription = msg.Payload.GetComponentInChildren<IDescriptionProvider>();

                if (customDescription != null)
                    description = customDescription.GetCustomDescription();

                if (string.IsNullOrEmpty(description))
                    description = stringLibrary.GetResource(KnownChunkId.ObjectNames)[objectPropertyLibrary.GetResource(msg.Payload.CombinedType).Index];

                Debug.LogFormat("Item inspection: {0}", description);
            });
            messageBus.Receive<CantUseObjectMessage>(msg => {
                string objectName = stringLibrary.GetResource(KnownChunkId.ObjectNames)[objectPropertyLibrary.GetResource(msg.Payload).Index];
                string message = stringLibrary.GetResource(KnownChunkId.UserInterfaceMessages)[96];
                Debug.LogFormat("Cant use: {0}", message.Replace("%s", objectName));
            });
            messageBus.Receive<AddAccessMessage>(msg => {
                string message = stringLibrary.GetResource(KnownChunkId.UserInterfaceMessages)[73];
                CyberString accessNames = stringLibrary.GetResource(KnownChunkId.AccessNames);
                for (int i = 0; i < 32; ++i) { // sizeof(uint) * 8 = 32
                    uint access = (uint)1 << i;
                    if ((msg.Payload & access) == access)
                        message += " " + accessNames[(uint)(i + 1) << 1];
                }

                Debug.LogFormat("Add Access Message: {0}", message);
            });
            messageBus.Receive<RequireAccessMessage>(msg => {
                string message = string.Empty;
                CyberString accessNames = stringLibrary.GetResource(KnownChunkId.AccessNames);
                for (int i = 0; i < 32; ++i) { // sizeof(uint) * 8 = 32
                    uint access = (uint)1 << i;
                    if ((msg.Payload & access) == access) {
                        if (!string.IsNullOrEmpty(message))
                            message += ' ';

                        message += accessNames[(uint)(i + 1) << 1];
                    }
                }

                message += stringLibrary.GetResource(KnownChunkId.UserInterfaceMessages)[13];

                Debug.LogFormat("Require Access Message: {0}", message);
            });
        }
    }

    public class TrapMessage : GenericMessage<byte> {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="message">Message index</param>
        public TrapMessage(byte message) : base(message) { }
    }

    public class InterfaceMessage : GenericMessage<byte> {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="message">Message index</param>
        public InterfaceMessage(byte message) : base(message) { }
    }

    public class ShodanSecurityMessage : GenericMessage<byte> {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="securityDelta">Security needed to open (delta)</param>
        public ShodanSecurityMessage(byte securityDelta) : base(securityDelta) { }
    }

    public class ItemInspectionMessage : GenericMessage<SystemShockObject> {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="ssobject">Inspected object</param>
        public ItemInspectionMessage(SystemShockObject ssobject) : base(ssobject) { }
    }

    public class CantUseObjectMessage : GenericMessage<uint> {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="combinedType">Combined item type</param>
        public CantUseObjectMessage(uint combinedType) : base(combinedType) { }
    }

    public class AddAccessMessage : GenericMessage<uint> {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="newAccessBits">New access bits</param>
        public AddAccessMessage(uint newAccessBits) : base(newAccessBits) { }
    }

    public class RequireAccessMessage : GenericMessage<uint> {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="accessBits">Required access bits</param>
        public RequireAccessMessage(uint accessBits) : base(accessBits) { }
    }

    public class ObjectInHandMessage : GenericMessage<SystemShockObject> {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="ssobject">Object in hand</param>
        public ObjectInHandMessage(SystemShockObject ssobject) : base(ssobject) { }
    }

    public interface IDescriptionProvider {
        string GetCustomDescription();
    }
}