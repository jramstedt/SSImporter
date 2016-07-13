using UnityEngine;

using SystemShock.Object;

namespace SystemShock.TriggerActions {
    [ExecuteInEditMode]
    public class Destroy : Triggerable<ObjectInstance.Trigger.Disable> {
        protected override bool DoTrigger() {
            ObjectFactory.Destroy((ushort)ActionData.ObjectId);

            return true;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos() {
            if (ObjectFactory == null)
                ObjectFactory = SystemShock.Resource.ObjectFactory.GetController();

            SystemShockObject Target = ObjectFactory.Get((ushort)ActionData.ObjectId);

            if (Target != null)
                Gizmos.DrawLine(transform.position, Target.transform.position);
        }

        private void OnDrawGizmosSelected() {

        }
#endif
    }
}