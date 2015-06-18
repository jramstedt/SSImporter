using UnityEngine;

using SystemShock.Object;

namespace SystemShock.TriggerActions {
    [ExecuteInEditMode]
    public class ChangeClassData : Triggerable<ObjectInstance.Trigger.ChangeClassData> {
        public SystemShockObject Target;

        private LevelInfo levelInfo;

        protected override void Awake() {
            base.Awake();

            levelInfo = GameObject.FindObjectOfType<LevelInfo>();
        }

        private void Start() {
            if (ActionData.ObjectId != 0 && !levelInfo.Objects.TryGetValue((uint)ActionData.ObjectId, out Target))
                Debug.Log("Tried to find object! " + ActionData.ObjectId, this);
        }

        public override void Trigger() {

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