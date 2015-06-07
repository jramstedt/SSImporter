using UnityEngine;

using SystemShock.Resource;

namespace SystemShock.Triggers {
    public class PlayerDeath : MonoBehaviour {
        private Triggerable triggerable;

        private void Awake() {
            triggerable = GetComponent<Triggerable>();

            // TODO Listen player death
        }
    }
}