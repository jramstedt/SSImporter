using UnityEngine;

using SystemShock.Resource;

namespace SystemShock.Triggers {
    [RequireComponent(typeof(BoxCollider))]
    public class AreaEnter : MonoBehaviour {
        private BoxCollider boxCollider;
        private ITriggerable triggerable;

        private void Awake() {
            LevelInfo levelInfo = ObjectFactory.GetController().LevelInfo;

            boxCollider = GetComponent<BoxCollider>();
            boxCollider.isTrigger = true;
            boxCollider.size = new Vector3(1f, 256.0f * levelInfo.HeightFactor, 1f); // TODO area size

            triggerable = GetComponent<ITriggerable>();
        }

        private void OnCollisionEnter(Collision collision) {
            // TODO Better check if player

            if (collision.gameObject.tag == "Player")
                triggerable.Trigger();
        }
    }
}