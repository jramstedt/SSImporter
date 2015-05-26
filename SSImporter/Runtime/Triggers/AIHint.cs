using UnityEngine;

using SystemShock.Resource;

namespace SystemShock.Triggers {
    public class AIHint : MonoBehaviour {
        private ITriggerable triggerable;

        private void Awake() {
            triggerable = GetComponent<ITriggerable>();
        }
    }
}