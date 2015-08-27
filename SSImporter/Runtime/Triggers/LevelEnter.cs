using UnityEngine;
using System.Collections;
using SystemShock.Resource;


namespace SystemShock.Triggers {
    public class LevelEnter : MonoBehaviour {
        private Triggerable triggerable;

        private void Awake() {
            triggerable = GetComponent<Triggerable>();
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