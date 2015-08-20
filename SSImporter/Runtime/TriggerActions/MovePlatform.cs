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
            LevelInfo.Tile tile = levelInfo.Tiles[ActionData.TileX, ActionData.TileY];
            GameObject tileGo = tile.GameObject;

            Speed = ActionData.Speed;

            if (Ceiling == null)
                Ceiling = tileGo.GetComponentInChildren<MovableCeiling>();

            if(Floor == null)
                Floor = tileGo.GetComponentInChildren<MovableFloor>();
        }
        
        public override void Trigger() {
            if (!CanActivate)
                return;

            if (Ceiling != null)
                Ceiling.Height = ActionData.TargetCeilingHeight;

            if (Floor != null)
                Floor.Height = ActionData.TargetFloorHeight;
        }
    }
}