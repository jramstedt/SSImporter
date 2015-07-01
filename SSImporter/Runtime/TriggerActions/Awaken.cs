using UnityEngine;

using SystemShock.Object;

namespace SystemShock.TriggerActions {
    [ExecuteInEditMode]
    public class Awaken : Triggerable<ObjectInstance.Trigger.Awaken> {
        public SystemShockObject FirstCorner;
        public SystemShockObject SecondCorner;

        private LevelInfo levelInfo;

        protected override void Awake() {
            base.Awake();

            levelInfo = GameObject.FindObjectOfType<LevelInfo>();
        }

        private void Start() {
            if (ActionData.Corner1ObjectId != 0 && !levelInfo.Objects.TryGetValue(ActionData.Corner1ObjectId, out FirstCorner))
                Debug.Log("Tried to find object! " + ActionData.Corner1ObjectId, this);

            if (ActionData.Corner2ObjectId != 0 && !levelInfo.Objects.TryGetValue(ActionData.Corner2ObjectId, out SecondCorner))
                Debug.Log("Tried to find object! " + ActionData.Corner2ObjectId, this);
        }

        public override void Trigger() {

        }

#if UNITY_EDITOR
        private void OnDrawGizmos() {
            if (FirstCorner == null || SecondCorner == null)
                return;

            //Vector3 center = (FirstCorner.transform.position + FirstCorner.transform.position) / 2f;
            //Vector3 size = FirstCorner.

            Bounds bounds = new Bounds(FirstCorner.transform.position, Vector3.zero);
            bounds.Encapsulate(SecondCorner.transform.position);

            Gizmos.DrawCube(bounds.center, bounds.size);

            //if (Target != null)
            //    Gizmos.DrawLine(transform.position, Target.transform.position);
        }

        private void OnDrawGizmosSelected() {

        }
#endif
    }
}