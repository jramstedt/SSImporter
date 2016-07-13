using UnityEngine;

using SystemShock.Object;
using SystemShock.Gameplay;
using UnityEngine.EventSystems;
using System;

namespace SystemShock.Interfaces {
    public class EnergyChargeStation : Interactable<ObjectInstance.Interface.ChargeStation> {

        protected override bool DoInteraction() {
            WaitAndTrigger(ActionData.ObjectToTrigger1, ActionData.Delay1);
            WaitAndTrigger(ActionData.ObjectToTrigger2, ActionData.Delay2);

            MessageBus.Send(new ChargePlayerMessage(GetComponent<SystemShockObject>(), (ushort)ActionData.Charge));

            return true;

            // TODO cooldown
        }

#if UNITY_EDITOR
        private void OnDrawGizmos() {
            if (ObjectFactory == null)
                return;

            ITriggerable Target1 = ObjectFactory.Get<ITriggerable>(ActionData.ObjectToTrigger1);
            if (Target1 != null)
                Gizmos.DrawLine(transform.position, Target1.transform.position);

            ITriggerable Target2 = ObjectFactory.Get<ITriggerable>(ActionData.ObjectToTrigger2);
            if (Target2 != null)
                Gizmos.DrawLine(transform.position, Target2.transform.position);
        }

        private void OnDrawGizmosSelected() {

        }


#endif
    }
}