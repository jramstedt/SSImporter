using UnityEngine;

using SystemShock.Object;
using SystemShock.Gameplay;
using UnityEngine.EventSystems;
using System;

namespace SystemShock.Interfaces {
    public class EnergyChargeStation : Interactable<ObjectInstance.Interface.ChargeStation>, IPointerClickHandler {
        public void OnPointerClick(PointerEventData eventData) {
            if (PermissionProvider.CanAct()) {
                if (ActionData.RechargeTime == 0) {
                    WaitAndTrigger(ActionData.ObjectToTrigger1, ActionData.Delay1);
                    WaitAndTrigger(ActionData.ObjectToTrigger2, ActionData.Delay2);
                } else {
                    MessageBus.Send(new ChargePlayerMessage(GetComponent<SystemShockObject>(), (ushort)ActionData.Charge));
                }
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos() {
            if (ObjectFactory == null)
                return;

            TriggerAction Target1 = ObjectFactory.Get<TriggerAction>(ActionData.ObjectToTrigger1);
            if (Target1 != null)
                Gizmos.DrawLine(transform.position, Target1.transform.position);

            TriggerAction Target2 = ObjectFactory.Get<TriggerAction>(ActionData.ObjectToTrigger2);
            if (Target2 != null)
                Gizmos.DrawLine(transform.position, Target2.transform.position);
        }

        private void OnDrawGizmosSelected() {

        }


#endif
    }
}