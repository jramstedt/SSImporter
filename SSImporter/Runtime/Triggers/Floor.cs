using UnityEngine;
using System.Collections;

namespace SystemShock.Triggers {
    [RequireComponent(typeof(BoxCollider))]
    public class Floor : MonoBehaviour {
        private ITriggerable triggerable;

        private void Awake() {
            // TODO Collider for floor
        }

        private void OnCollisionEnter(Collision collision) {
            triggerable.Trigger();
        }
    }
}