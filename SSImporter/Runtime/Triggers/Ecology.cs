using UnityEngine;

using SystemShock.Resource;

namespace SystemShock.Triggers {
    public class Ecology : MonoBehaviour {
        private ITriggerable triggerable;

        private void Awake() {
            triggerable = GetComponent<ITriggerable>();

            // TODO monitor ecology
            // TODO trigger if out of limits
        }
    }
}