using UnityEngine;

using SystemShock.Object;
using SystemShock.Resource;


namespace SystemShock.TriggerActions {
    [ExecuteInEditMode]
    public class TeleportPlayer : Triggerable<ObjectInstance.Trigger.TeleportPlayer> {
        private Vector3 targetPosition;
        private Quaternion targetRotation;

        protected override void Awake() {
            base.Awake();

            LevelInfo levelInfo = GameObject.FindObjectOfType<LevelInfo>();

            targetPosition = new Vector3(ActionData.TileX + 0.5f, ActionData.Z * levelInfo.HeightFactor, ActionData.TileY + 0.5f);
            targetRotation = Quaternion.Euler(-ActionData.Pitch / 256f * 360f, ActionData.Yaw / 256f * 360f, -ActionData.Roll / 256f * 360f);

        }

        public override void Trigger() {
            GameObject player = GameObject.FindGameObjectWithTag(@"Player");

            Rigidbody rigidbody = player.GetComponent<Rigidbody>();
            if(rigidbody != null) {
                rigidbody.position = targetPosition;
                rigidbody.rotation = targetRotation;
            } else {
                player.transform.position = targetPosition;
                player.transform.rotation = targetRotation;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos() {
            Gizmos.DrawSphere(targetPosition, 0.25f);
            Gizmos.DrawRay(targetPosition, targetRotation * Vector3.forward);
        }
#endif
    }
}
