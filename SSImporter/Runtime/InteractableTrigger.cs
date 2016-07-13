using System;
using SystemShock.Object;
using SystemShock.UserInterface;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SystemShock {
    public class InteractableTrigger : Interactable {
        private ITriggerable triggerable;

        protected override void Awake() {
            base.Awake();
            triggerable = GetComponent<ITriggerable>();
        }

        protected override bool DoInteraction() {
            if (triggerable != null)
                return triggerable.Trigger();

            return false;
        }
    }
}