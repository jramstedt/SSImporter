using UnityEngine;

using SystemShock.Resource;

namespace SystemShock.Triggers {
    public class LevelEnter : MonoBehaviour {
        private ITriggerable triggerable;

        private void Awake() {
            triggerable = GetComponent<ITriggerable>();
        }

        private void Start() {
            triggerable.Trigger();
        }
    }
}