using UnityEngine;

using SystemShock.Resource;

namespace SystemShock.Triggers {
    public class Shodan : MonoBehaviour {
        private ITriggerable triggerable;

        private void Awake() {
            triggerable = GetComponent<ITriggerable>();
        }
    }
}