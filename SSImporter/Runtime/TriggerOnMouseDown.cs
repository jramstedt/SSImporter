using UnityEngine;
using System.Collections;

namespace SystemShock {
    public class TriggerOnMouseDown : MonoBehaviour {
        private Triggerable triggerable;

        private void Awake() {
            triggerable = GetComponentInChildren<Triggerable>();
        }
        private void OnMouseDown() {
            if (triggerable != null)
                triggerable.Trigger();
        }
    }
}