using UnityEngine;
using System.Collections;
using SystemShock.Object;

namespace SystemShock.Interfaces {
    [ExecuteInEditMode]
    public class KeyPad : Interactable<ObjectInstance.Interface.KeyPad> {
        public TriggerAction Target1;
        public TriggerAction Target2;

        private void Start() {
            Target1 = ObjectFactory.Get<TriggerAction>(ActionData.ObjectToTrigger1);
            Target2 = ObjectFactory.Get<TriggerAction>(ActionData.ObjectToTrigger2);
        }

        private void OnMouseDown() {
            if (Target1 != null)
                Target1.Act();

            if (Target2 != null)
                Target2.Act();
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