using UnityEngine;
using System.Collections;

namespace SystemShock.Triggers {
    [RequireComponent(typeof(BoxCollider))]
    public class Floor : Null {
        private ITriggerable triggerable;

        protected override void Awake() {
            base.Awake();

            triggerable = GetComponent<ITriggerable>();

            // TODO Collider for floor
        }

        private void OnCollisionEnter(Collision collision) {
            triggerable.Trigger();
        }
    }
}