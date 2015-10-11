using UnityEngine;

using SystemShock.Object;

namespace SystemShock.TriggerActions {
    [ExecuteInEditMode]
    public class Destroy : TriggerAction<ObjectInstance.Trigger.Disable> {
        protected override void DoAct() {
            ObjectFactory.Destroy((ushort)ActionData.ObjectId);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos() {
            SystemShockObject Target = ObjectFactory.Get((ushort)ActionData.ObjectId);

            if (Target != null)
                Gizmos.DrawLine(transform.position, Target.transform.position);
        }

        private void OnDrawGizmosSelected() {

        }
#endif
    }
}