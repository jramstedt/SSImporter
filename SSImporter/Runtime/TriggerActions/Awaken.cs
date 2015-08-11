using UnityEngine;

using SystemShock.Object;
using SystemShock.Resource;

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

            Bounds bounds = new Bounds(FirstCorner.transform.position, Vector3.zero);
            bounds.Encapsulate(SecondCorner.transform.position);

            Color color = Color.green;
            color.a = 0.1f;
            Gizmos.color = color;

            Gizmos.DrawCube(bounds.center, bounds.size);
        }

        private void OnDrawGizmosSelected() {

        }
#endif
    }
}