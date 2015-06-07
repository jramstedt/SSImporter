using UnityEngine;
using System.Collections;

namespace SystemShock.Triggers {
    [RequireComponent(typeof(BoxCollider))]
    public class Floor : MonoBehaviour {
        private Triggerable triggerable;

        private void Awake() {
            triggerable = GetComponent<Triggerable>();

            // TODO Collider for floor
        }

        private void OnCollisionEnter(Collision collision) {
            triggerable.Trigger();
        }
    }
}