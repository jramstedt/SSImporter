using UnityEngine;

using SystemShock.Object;

namespace SystemShock.TriggerActions {
    [ExecuteInEditMode]
    public class MovePlatform : TriggerAction<ObjectInstance.Trigger.MovePlatform> {
        public MovableCeiling Ceiling;
        public MovableFloor Floor;

        public int Speed;

        private void Start() {
            LevelInfo.Tile tile = ObjectFactory.LevelInfo.Tiles[ActionData.TileX, ActionData.TileY];
            GameObject tileGo = tile.GameObject;

            Speed = ActionData.Speed;

            if (Ceiling == null)
                Ceiling = tileGo.GetComponentInChildren<MovableCeiling>();

            if(Floor == null)
                Floor = tileGo.GetComponentInChildren<MovableFloor>();
        }

        protected override void DoAct() {
            if (Ceiling != null)
                Ceiling.Height = ActionData.TargetCeilingHeight;

            if (Floor != null)
                Floor.Height = ActionData.TargetFloorHeight;
        }
    }
}