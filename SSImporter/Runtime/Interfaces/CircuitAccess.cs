using UnityEngine;
using System.Collections;
using SystemShock.Object;

namespace SystemShock.Interfaces {
    [ExecuteInEditMode]
    public class CircuitAccess : Interactable<ObjectInstance.Interface.CircuitAccess> {
        public TriggerAction Target;

        private void Start() {
            Target = ObjectFactory.Get<TriggerAction>(ActionData.ObjectToTrigger);
        }

        private void OnMouseDown() {
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