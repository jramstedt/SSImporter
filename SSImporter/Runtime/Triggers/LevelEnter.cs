using UnityEngine;
using System.Collections;
using SystemShock.Resource;


namespace SystemShock.Triggers {
    public class LevelEnter : Null {
        private ITriggerable triggerable;

        protected override void Awake() {
            base.Awake();

            triggerable = GetComponent<ITriggerable>();
        }

        private void Start() {
            StartCoroutine(DelayedTrigger());
        }

        private IEnumerator DelayedTrigger() {
            yield return null;

            triggerable.Trigger();
        }
    }
}