using UnityEngine;

using SystemShock.Object;

namespace SystemShock.TriggerActions {
    public class MovePlatform : TriggerAction<ObjectInstance.Trigger.MovePlatform> {
        public MovableCeiling Ceiling;
        public MovableFloor Floor;

        public int Speed;

        private void Start() {
            if(Ceiling == null && Floor == null) {
                LevelInfo.Tile tile = ObjectFactory.LevelInfo.Tiles[ActionData.TileX, ActionData.TileY];
                GameObject tileGo = tile.GameObject;

                Speed = ActionData.Speed;

                if (ActionData.TargetCeilingHeight != 0xFF && Ceiling == null)
                    Ceiling = tileGo.GetComponentInChildren<MovableCeiling>();

                if (ActionData.TargetFloorHeight != 0xFF && Floor == null)
                    Floor = tileGo.GetComponentInChildren<MovableFloor>();
            }
        }

        private void Reset() {
            Awake();
            Start();
        }

        protected override void DoAct() {
            if (Ceiling != null)
                Ceiling.Height = ActionData.TargetCeilingHeight;

            if (Floor != null)
                Floor.Height = ActionData.TargetFloorHeight;
        }
    }
}