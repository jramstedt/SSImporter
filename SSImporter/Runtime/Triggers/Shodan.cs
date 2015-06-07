using UnityEngine;

using SystemShock.Resource;

namespace SystemShock.Triggers {
    public class Shodan : MonoBehaviour {
        private Triggerable triggerable;

        private void Awake() {
            triggerable = GetComponent<Triggerable>();
        }
    }
}