using UnityEngine;

using SystemShock.Resource;

namespace SystemShock.Triggers {
    [RequireComponent(typeof(BoxCollider))]
    public class AreaContinous : MonoBehaviour {
        private Triggerable triggerable;

        private void Awake() {
            triggerable = GetComponent<Triggerable>();
        }
    }
}