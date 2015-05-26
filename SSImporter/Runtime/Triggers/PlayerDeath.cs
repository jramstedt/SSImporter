using UnityEngine;

using SystemShock.Resource;

namespace SystemShock.Triggers {
    public class PlayerDeath : MonoBehaviour {
        private ITriggerable triggerable;

        private void Awake() {
            triggerable = GetComponent<ITriggerable>();

            // TODO Listen player death
        }
    }
}