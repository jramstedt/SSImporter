using System;
using SystemShock.Object;
using SystemShock.UserInterface;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SystemShock {
    [RequireComponent(typeof(Door))]
    public class ActivableDoor : TriggerAction, IPointerClickHandler {
        private Door door;

        protected override void Awake() {
            base.Awake();

            PermissionProvider = GetComponentInParent<IActionPermission>();
            door = GetComponent<Door>();
        }

        public override bool Act() {
            if (door != null) {
                door.Activate();
                return true;
            }

            return false;
        }

        public void OnPointerClick(PointerEventData eventData) {
            if (eventData.clickCount >= 2 && PermissionProvider.CanAct())
                Act();
        }
    }
}