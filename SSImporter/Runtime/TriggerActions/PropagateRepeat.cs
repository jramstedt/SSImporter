using UnityEngine;

using SystemShock.Object;
using System.Collections;

namespace SystemShock.TriggerActions {
    [ExecuteInEditMode]
    public class PropagateRepeat : TriggerAction<ObjectInstance.Trigger.PropagateRepeat> {
        public TriggerAction Target;

        public int Count;

        private void Start() {
            Target = ObjectFactory.Get<TriggerAction>((ushort)ActionData.ObjectId);
        }

        protected override void DoAct() {
            Count = ActionData.Count;

            StartCoroutine(RepeatTrigger());
        }
        private IEnumerator RepeatTrigger() {
            long delay = ActionData.Delay + Random.Range(ActionData.DelayVariationMin, ActionData.DelayVariationMax);

            Target.Act();

            if (ActionData.Count == 0 || Count-- > 0) {
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