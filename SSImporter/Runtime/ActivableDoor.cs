using UnityEngine;

namespace SystemShock {
    [RequireComponent(typeof(Door))]
    public class ActivableDoor : TriggerAction {
        private Door door;

        private void Awake() {
            door = GetComponent<Door>();
        }

        private void OnMouseDown() {
            door.Activate();
        }


        public override void Act() {
            door.Activate();
        }
    }
}