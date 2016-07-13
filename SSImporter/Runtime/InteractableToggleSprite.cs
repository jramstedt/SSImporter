using UnityEngine;
using System.Collections;
using System;

namespace SystemShock {
    public class InteractableToggleSprite : InteractableTrigger {
        private ToggleSprite toggleSprite;

        protected override void Awake() {
            base.Awake();
            toggleSprite = GetComponent<ToggleSprite>();
        }

        protected override bool DoInteraction() {
            if (toggleSprite != null && base.DoInteraction()) {
                toggleSprite.Toggle();
                return true;
            }

            return false;
        }
    }
}