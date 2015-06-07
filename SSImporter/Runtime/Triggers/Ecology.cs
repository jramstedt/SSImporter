using UnityEngine;

using SystemShock.Resource;

namespace SystemShock.Triggers {
    public class Ecology : MonoBehaviour {
        private Triggerable triggerable;

        private void Awake() {
            triggerable = GetComponent<Triggerable>();

            // TODO monitor ecology
            // TODO trigger if out of limits
        }
    }
}