using UnityEngine;

using SystemShock.Object;
using SystemShock.Resource;


namespace SystemShock.TriggerActions {
    [ExecuteInEditMode]
    public class Transport : Triggerable<ObjectInstance.Trigger.Transport> {
        private Vector3 targetPosition;
        private Quaternion targetRotation;

        protected override void Awake() {
            base.Awake();

            LevelInfo levelInfo = GameObject.FindObjectOfType<LevelInfo>();

            targetPosition = new Vector3(ActionData.TileX + 0.5f, ActionData.Z * levelInfo.HeightFactor, ActionData.TileY + 0.5f);
            targetRotation = Quaternion.Euler(-ActionData.Pitch / 256f * 360f, ActionData.Yaw / 256f * 360f, -ActionData.Roll / 256f * 360f);

        }

        public override void Trigger() {
            Rigidbody rigidbody = GetComponent<Rigidbody>();
            if(rigidbody != null) {
                rigidbody.position = targetPosition;
                rigidbody.rotation = targetRotation;
            } else {
                transform.position = targetPosition;
                transform.rotation = targetRotation;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos() {
            Gizmos.DrawLine(transform.position, targetPosition);
            Gizmos.DrawRay(targetPosition, targetRotation * Vector3.forward);
        }
#endif
    }
}
