using UnityEngine;
using System.Collections;
using SystemShock.Object;
using UnityEngine.EventSystems;
using System;

namespace SystemShock.Interfaces {
    [ExecuteInEditMode]
    public class KeyPad : Interactable<ObjectInstance.Interface.KeyPad> {
        private ITriggerable Target1;
        private ITriggerable Target2;

        private void Start() {
            Target1 = ObjectFactory.Get<ITriggerable>(ActionData.ObjectToTrigger1);
            Target2 = ObjectFactory.Get<ITriggerable>(ActionData.ObjectToTrigger2);
        }

        protected override bool DoInteraction() {
            if (Target1 != null)
                Target1.Trigger();

            if (Target2 != null)
                Target2.Trigger();

            return true;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos() {
            if (Target1 != null)
                Gizmos.DrawLine(transform.position, Target1.transform.position);

            if (Target2 != null)
                Gizmos.DrawLine(transform.position, Target2.transform.position);
        }

        private void OnDrawGizmosSelected() {

        }
#endif
    }
}