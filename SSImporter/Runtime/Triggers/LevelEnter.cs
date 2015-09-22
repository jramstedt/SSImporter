using UnityEngine;
using System.Collections;
using SystemShock.Resource;


namespace SystemShock.Triggers {
    public class LevelEnter : Null {
        private TriggerAction triggerable;

        protected override void Awake() {
            base.Awake();

            triggerable = GetComponent<TriggerAction>();
        }

        private void Start() {
            StartCoroutine(DelayedTrigger());
        }

        private IEnumerator DelayedTrigger() {
            yield return null;

            triggerable.Act();
        }
    }
}