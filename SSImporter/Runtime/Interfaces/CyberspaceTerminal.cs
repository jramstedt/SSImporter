using UnityEngine;
using System.Collections;
using SystemShock.Object;

namespace SystemShock.Interfaces {
    public class CyberspaceTerminal : Interactable<ObjectInstance.Interface.Cyberjack> {
        private void OnMouseDown() {
            if (PermissionProvider.CanAct()) {
                // TODO enter cyberspace
            }
        }
    }
}