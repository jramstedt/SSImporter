using UnityEngine;

using SystemShock.Object;
using System.Collections;

namespace SystemShock.TriggerActions {
    [ExecuteInEditMode]
    public class PropagateRepeat : Triggerable<ObjectInstance.Trigger.PropagateRepeat> {
        public Triggerable Target;

        private LevelInfo levelInfo;

        public int Count;

        protected override void Awake() {
            base.Awake();

            levelInfo = GameObject.FindObjectOfType<LevelInfo>();
        }

        private void Start() {
            SystemShockObject ssObject;
            if (ActionData.ObjectId != 0 && levelInfo.Objects.TryGetValue((ushort)ActionData.ObjectId, out ssObject)) {
                Target = ssObject.GetComponent<Triggerable>();

                if (Target == null) {
                    Debug.Log("1Tried to link trigger! " + ssObject, ssObject);
                    Debug.Log("2Tried to link trigger! " + ssObject, this);
                }
            } else if (ActionData.ObjectId != 0) {
                Debug.Log("Tried to find object! " + ActionData.ObjectId, this);
            }

        }

        public override void Trigger() {
            if (!CanActivate)
                return;

            Count = ActionData.Count > 0 ? ActionData.Count : int.MaxValue;

            StartCoroutine(RepeatTrigger());
        }
        private IEnumerator RepeatTrigger() {
            long delay = ActionData.Delay + Random.Range(ActionData.DelayVariationMin, ActionData.DelayVariationMax);

            Target.Trigger();

            if (Count-- > 0) {
                yield return new WaitForSeconds(delay / 10f);
                StartCoroutine(RepeatTrigger());
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos() {
            if (Target != null)
                Gizmos.DrawLine(transform.position, Target.transform.position);
        }

        private void OnDrawGizmosSelected() {

        }
#endif
    }
}