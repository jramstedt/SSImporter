using UnityEngine;
using System.Collections;
using SystemShock.Object;
using UnityEngine.EventSystems;
using System;

namespace SystemShock.Interfaces {
    [ExecuteInEditMode]
    public class CircuitAccess : Interactable<ObjectInstance.Interface.CircuitAccess>, IPointerClickHandler {
        public TriggerAction Target;

        private void Start() {
            Target = ObjectFactory.Get<TriggerAction>(ActionData.ObjectToTrigger);
        }

        public void OnPointerClick(PointerEventData eventData) {
            if (Target != null && PermissionProvider.CanAct())
                Target.Act();
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