using UnityEngine;

namespace SystemShock {
    [RequireComponent(typeof(Door))]
    public class ActivableDoor : TriggerAction {
        protected IActionPermission PermissionProvider;

        private Door door;

        private void Awake() {
            PermissionProvider = GetComponentInParent<IActionPermission>();
            door = GetComponent<Door>();
        }

        private void OnMouseDown() {
            if (PermissionProvider.CanAct())
                Act();
        }


        public override bool Act() {
            if (door != null) {
                door.Activate();
                return true;
            }

            return false;
        }
    }
}