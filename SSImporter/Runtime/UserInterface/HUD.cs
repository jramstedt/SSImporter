using UnityEngine;
using System.Collections;
using SystemShock.Resource;

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
            messageBus.Receive<InterfaceMessage>(msg => Debug.LogFormat("Interface Message: {0}", stringLibrary.GetResource(KnownChunkId.InterfaceMessages)[msg.Payload]));
            messageBus.Receive<ShodanSecurityMessage>(msg => Debug.LogFormat("Shodan Security: {0}", msg.Payload));
            messageBus.Receive<ItemInspectionMessage>(msg => Debug.LogFormat("Item inspection: {0}", stringLibrary.GetResource(KnownChunkId.ObjectNames)[objectPropertyLibrary.GetResource(msg.Payload).Index]));
        }
    }

    public class TrapMessage : GenericMessage<byte> {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="payload">Message index</param>
        public TrapMessage(byte payload) : base(payload) { }
    }

    public class InterfaceMessage : GenericMessage<byte> {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="payload">Message index</param>
        public InterfaceMessage(byte payload) : base(payload) { }
    }

    public class ShodanSecurityMessage : GenericMessage<byte> {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="payload">Security needed to open (delta)</param>
        public ShodanSecurityMessage(byte payload) : base(payload) { }
    }

    public class ItemInspectionMessage : GenericMessage<uint> {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="payload">Combined item id</param>
        public ItemInspectionMessage(uint payload) : base(payload) { }
    }
}