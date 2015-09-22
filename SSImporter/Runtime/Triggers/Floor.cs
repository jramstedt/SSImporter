using UnityEngine;
using System.Collections;

namespace SystemShock.Triggers {
    [RequireComponent(typeof(BoxCollider))]
    public class Floor : Null {
        private TriggerAction triggerable;

        protected override void Awake() {
            base.Awake();

            triggerable = GetComponent<TriggerAction>();

            // TODO Collider for floor
        }

        private void OnCollisionEnter(Collision collision) {
            triggerable.Act();
        }
    }
}