using UnityEngine;

using SystemShock.Object;

namespace SystemShock.TriggerActions {
    [ExecuteInEditMode]
    public class SetPosition : Triggerable<ObjectInstance.Trigger.SetPosition> {
        public SystemShockObject Target;

        private LevelInfo levelInfo;

        private Vector3 targetPosition;

        protected override void Awake() {
            base.Awake();

            levelInfo = GameObject.FindObjectOfType<LevelInfo>();
            targetPosition = new Vector3(ActionData.TileX, ActionData.Z * levelInfo.HeightFactor, ActionData.TileY);
        }

        private void Start() {
            if (ActionData.ObjectId != 0 && !levelInfo.Objects.TryGetValue(ActionData.ObjectId, out Target))
                Debug.Log("Tried to find object! " + ActionData.ObjectId, this);
        }

        public override void Trigger() {
            if (!CanActivate)
                return;

            Rigidbody rigidbody = Target.GetComponent<Rigidbody>();
            if (rigidbody != null) {
                Vector3 oldPos = rigidbody.position;
                rigidbody.position = new Vector3(targetPosition.x + (oldPos.x % 1), targetPosition.y, targetPosition.z + (oldPos.z % 1));
            } else {
                Vector3 oldPos = Target.transform.position;
                Target.transform.position = new Vector3(targetPosition.x + (oldPos.x % 1), targetPosition.y, targetPosition.z + (oldPos.z % 1));
            }
        }
    }
}

