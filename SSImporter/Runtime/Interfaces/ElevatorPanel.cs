using UnityEngine;
using System.Collections;
using SystemShock.Object;
using System;

namespace SystemShock.Interfaces {
    [ExecuteInEditMode]
    public class ElevatorPanel : Interactable<ObjectInstance.Interface.Elevator> {
        protected override bool DoInteraction() {
            return false;
        }
    }
}