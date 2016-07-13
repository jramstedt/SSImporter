using UnityEngine;

using SystemShock.Resource;
using SystemShock.Gameplay;

namespace SystemShock.Triggers {
    [RequireComponent(typeof(BoxCollider))]
    public class Entry : Null {
        private BoxCollider boxCollider;
        private ITriggerable triggerable;

        protected override void Awake() {
            base.Awake();

            LevelInfo levelInfo = ObjectFactory.GetController().LevelInfo;

            boxCollider = GetComponent<BoxCollider>();
            boxCollider.isTrigger = true;
            boxCollider.size = new Vector3(1f, 256.0f * levelInfo.HeightFactor, 1f);

            triggerable = GetComponent<ITriggerable>();
        }

        private void OnTriggerEnter(Collider collider) {
            if (collider.GetComponentInChildren<Hacker>() != null)
                triggerable.Trigger();
        }
    }
}