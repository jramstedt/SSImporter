using UnityEngine;

using SystemShock.Resource;

namespace SystemShock.Triggers {
    [RequireComponent(typeof(BoxCollider))]
    public class AreaContinous : MonoBehaviour {
        private ITriggerable triggerable;

        private void Awake() {
            triggerable = GetComponent<ITriggerable>();
        }
    }
}