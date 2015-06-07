using UnityEngine;

using SystemShock.Object;

namespace SystemShock.TriggerActions {
    [ExecuteInEditMode]
    public class MovePlatform : Triggerable<ObjectInstance.Trigger.MovePlatform> {
        public MovableCeiling Ceiling;
        public MovableFloor Floor;

        public int Speed;

        private LevelInfo levelInfo;

        protected override void Awake() {
            base.Awake();

            levelInfo = GameObject.FindObjectOfType<LevelInfo>();
        }

        private void Start() {
            GameObject tile = levelInfo.Tile[ActionData.TileX, ActionData.TileY];

            Speed = ActionData.Speed;

            if (Ceiling == null)
                Ceiling = tile.GetComponentInChildren<MovableCeiling>();

            if(Floor == null)
                Floor = tile.GetComponentInChildren<MovableFloor>();
        }
        
        public override void Trigger() {
            if(Ceiling != null)
                Ceiling.Height = ActionData.TargetCeilingHeight;

            if (Floor != null)
                Floor.Height = ActionData.TargetFloorHeight;
        }
    }
}