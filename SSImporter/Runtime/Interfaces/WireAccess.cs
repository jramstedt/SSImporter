using UnityEngine;
using System.Collections;
using SystemShock.Object;

namespace SystemShock.Interfaces {
    [ExecuteInEditMode]
    public class WireAccess : Interactable<ObjectInstance.Interface.WireAccess> {
        public TriggerAction Target;

        private void Start() {
            Target = ObjectFactory.Get<TriggerAction>(ActionData.ObjectToTrigger);
        }

        private void OnMouseDown() {
            if (Target != null)
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