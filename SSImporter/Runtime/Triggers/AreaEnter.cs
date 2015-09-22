using UnityEngine;

using SystemShock.Resource;

namespace SystemShock.Triggers {
    /// <summary>
    /// This trigger type is not used in any of the levels.
    /// </summary>


    [RequireComponent(typeof(BoxCollider))]
    public class AreaEnter : Null {
        private BoxCollider boxCollider;
        private TriggerAction triggerable;

        protected override void Awake() {
            base.Awake();

            LevelInfo levelInfo = ObjectFactory.GetController().LevelInfo;

            boxCollider = GetComponent<BoxCollider>();
            boxCollider.isTrigger = true;
            boxCollider.size = new Vector3(1f, 256.0f * levelInfo.HeightFactor, 1f); // TODO area size

            triggerable = GetComponent<TriggerAction>();
        }

        private void OnCollisionEnter(Collision collision) {
            // TODO Better check if player

            if (collision.gameObject.tag == "Player")
                triggerable.Act();
        }
    }
}