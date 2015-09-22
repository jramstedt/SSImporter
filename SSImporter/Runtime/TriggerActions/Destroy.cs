using UnityEngine;

using SystemShock.Object;

namespace SystemShock.TriggerActions {
    [ExecuteInEditMode]
    public class Destroy : TriggerAction<ObjectInstance.Trigger.Disable> {
        public SystemShockObject Target;

        private void Start() {
            Target = ObjectFactory.Get((ushort)ActionData.ObjectId);
        }

        protected override void DoAct() {
            Target.gameObject.SetActive(false);
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