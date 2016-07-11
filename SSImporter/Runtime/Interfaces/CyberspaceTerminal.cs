using UnityEngine;
using System.Collections;
using SystemShock.Object;
using UnityEngine.EventSystems;
using System;

namespace SystemShock.Interfaces {
    public class CyberspaceTerminal : Interactable<ObjectInstance.Interface.Cyberjack>, IPointerClickHandler {
        public void OnPointerClick(PointerEventData eventData) {
            if (PermissionProvider.CanAct()) {
                // TODO enter cyberspace
            }
        }
    }
}