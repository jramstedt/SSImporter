using UnityEngine;

using SystemShock.Object;
using SystemShock.Resource;


namespace SystemShock.TriggerActions {
    [ExecuteInEditMode]
    public class TeleportPlayer : Triggerable<ObjectInstance.Trigger.TeleportPlayer> {
        public Vector3 targetPosition;
        public Quaternion targetRotation;

        protected override void Awake() {
            base.Awake();

            LevelInfo levelInfo = GameObject.FindObjectOfType<LevelInfo>();

            targetPosition = new Vector3(ActionData.TileX + 0.5f, ActionData.Z * levelInfo.HeightFactor, ActionData.TileY + 0.5f);
            targetRotation = Quaternion.Euler(-ActionData.Pitch / 65536f * 360f, ActionData.Yaw / 65536f * 360f, -ActionData.Roll / 65536f * 360f);           
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
