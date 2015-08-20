using UnityEngine;

using SystemShock.Object;
using SystemShock.Resource;

namespace SystemShock.TriggerActions {
    [ExecuteInEditMode]
    public class TeleportPlayer : Triggerable<ObjectInstance.Trigger.TeleportPlayer> {
        private LevelInfo levelInfo;

        public Vector3 targetPosition;
        public Quaternion targetRotation;

        protected override void Awake() {
            base.Awake();

            levelInfo = GameObject.FindObjectOfType<LevelInfo>();

            LevelInfo.Tile Tile = levelInfo.Tiles[ActionData.TileX, ActionData.TileY];

            float realY = Mathf.Clamp(ActionData.Z * levelInfo.HeightFactor, Tile.Floor * levelInfo.MapScale, Tile.Ceiling * levelInfo.MapScale);

            targetPosition = new Vector3(ActionData.TileX + 0.5f, realY, ActionData.TileY + 0.5f);
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
