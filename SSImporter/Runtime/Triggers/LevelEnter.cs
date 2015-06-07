using UnityEngine;

using SystemShock.Resource;

namespace SystemShock.Triggers {
    public class LevelEnter : MonoBehaviour {
        private Triggerable triggerable;

        private void Awake() {
            triggerable = GetComponent<Triggerable>();
        }

        private void Start() {
            triggerable.Trigger();
        }
    }
}