using UnityEngine;
using System.Collections;
using SystemShock.Object;
using UnityEngine.EventSystems;
using System;

namespace SystemShock.Interfaces {
    [ExecuteInEditMode]
    public class CircuitAccess : Interactable<ObjectInstance.Interface.CircuitAccess> {
        public ITriggerable Target;

        private void Start() {
            Target = ObjectFactory.Get<ITriggerable>(ActionData.ObjectToTrigger);
        }

        protected override bool DoInteraction() {
            return Target.Trigger();
        }

#if UNITY_EDITOR
        private void OnDrawGizmos() {
            if (Target != null)
                Gizmos.DrawLine(transform.position, Target.transform.position);
        }

        private void OnDrawGizmosSelected() {

        }
#endif
    }
}