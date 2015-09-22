using UnityEngine;

using SystemShock.Resource;

namespace SystemShock.Triggers {
    [RequireComponent(typeof(BoxCollider))]
    public class Entry : Null {
        private BoxCollider boxCollider;
        private TriggerAction triggerable;

        protected override void Awake() {
            base.Awake();

            LevelInfo levelInfo = ObjectFactory.GetController().LevelInfo;

            boxCollider = GetComponent<BoxCollider>();
            boxCollider.isTrigger = true;
            boxCollider.size = new Vector3(1f, 256.0f * levelInfo.HeightFactor, 1f);

            triggerable = GetComponent<TriggerAction>();
        }

        private void OnTriggerEnter(Collider collider) {
            // TODO Better check if player

            if (triggerable != null && collider.tag == "Player")
                triggerable.Act();
        }
    }
}