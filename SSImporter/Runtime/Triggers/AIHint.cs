using UnityEngine;

using SystemShock.Resource;

namespace SystemShock.Triggers {
    public class AIHint : MonoBehaviour {
        private Triggerable triggerable;

        private void Awake() {
            triggerable = GetComponent<Triggerable>();
        }
    }
}