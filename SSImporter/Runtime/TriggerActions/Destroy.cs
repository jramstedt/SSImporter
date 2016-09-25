using UnityEngine;

using SystemShock.Object;
using System.Collections;

namespace SystemShock.TriggerActions {
    [ExecuteInEditMode]
    public class Destroy : Triggerable<ObjectInstance.Trigger.Destroy> {
        protected override bool DoTrigger() {
            StartCoroutine(this.WaitAndDestroy(ActionData.ObjectToDestroy1, ActionData.Delay1));
            StartCoroutine(this.WaitAndDestroy(ActionData.ObjectToDestroy2, ActionData.Delay2));
            StartCoroutine(this.WaitAndDestroy(ActionData.ObjectToDestroy3, ActionData.Delay3));

            return true;
        }

        public IEnumerator WaitAndDestroy(ushort objectId, ushort delay) {
            if (delay > 0)
                yield return new WaitForSeconds(delay / 10f);

            ObjectFactory.Destroy(objectId);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos() {
            if (ObjectFactory == null)
                return;

            SystemShockObject Target1 = ObjectFactory.Get(ActionData.ObjectToDestroy1);
            if (Target1 != null)
                Gizmos.DrawLine(transform.position, Target1.transform.position);

            SystemShockObject Target2 = ObjectFactory.Get(ActionData.ObjectToDestroy2);
            if (Target2 != null)
                Gizmos.DrawLine(transform.position, Target2.transform.position);

            SystemShockObject Target3 = ObjectFactory.Get(ActionData.ObjectToDestroy3);
            if (Target3 != null)
                Gizmos.DrawLine(transform.position, Target3.transform.position);
        }

        private void OnDrawGizmosSelected() {

        }
#endif
    }
}