using UnityEngine;

using SystemShock.Resource;

namespace SystemShock.Triggers {
    /// <summary>
    /// This trigger type is not used in any of the levels.
    /// </summary>


    [RequireComponent(typeof(BoxCollider))]
    public class AreaEnter : MonoBehaviour {
        private BoxCollider boxCollider;
        private Triggerable triggerable;

        private void Awake() {
            LevelInfo levelInfo = ObjectFactory.GetController().LevelInfo;

            boxCollider = GetComponent<BoxCollider>();
            boxCollider.isTrigger = true;
            boxCollider.size = new Vector3(1f, 256.0f * levelInfo.HeightFactor, 1f); // TODO area size

            triggerable = GetComponent<Triggerable>();
        }

        private void OnCollisionEnter(Collision collision) {
            // TODO Better check if player

            if (collision.gameObject.tag == "Player")
                triggerable.Trigger();
        }
    }
}