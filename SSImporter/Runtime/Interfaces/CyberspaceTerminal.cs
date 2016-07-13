using UnityEngine;
using System.Collections;
using SystemShock.Object;
using UnityEngine.EventSystems;
using System;

namespace SystemShock.Interfaces {
    public class CyberspaceTerminal : Interactable<ObjectInstance.Interface.Cyberjack> {
        protected override bool DoInteraction() {
            // TODO enter cyberspace

            return false;
        }
    }
}