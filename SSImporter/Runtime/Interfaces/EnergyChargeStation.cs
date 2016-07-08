using UnityEngine;
using System.Collections;
using SystemShock.Object;

namespace SystemShock.Interfaces {
    public class EnergyChargeStation : Interactable<ObjectInstance.Interface.ChargeStation> {
        private void OnMouseDown() {
            if (PermissionProvider.CanAct()) {
                // TODO charge hacker
            }
        }
    }
}