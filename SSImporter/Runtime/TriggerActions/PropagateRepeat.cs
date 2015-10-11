using UnityEngine;

using SystemShock.Object;
using System.Collections;

namespace SystemShock.TriggerActions {
    [ExecuteInEditMode]
    public class PropagateRepeat : TriggerAction<ObjectInstance.Trigger.PropagateRepeat> {
        public int Count;

        private void Start() {
        }

        protected override void DoAct() {
            Count = ActionData.Count;

            StartCoroutine(RepeatTrigger());
        }
        private IEnumerator RepeatTrigger() {
            long delay = ActionData.Delay + Random.Range(ActionData.DelayVariationMin, ActionData.DelayVariationMax);

            TriggerAction Target = ObjectFactory.Get<TriggerAction>((ushort)ActionData.ObjectId);
            if (Target != null)
                Target.Act();
            else
                yield break;

            if (ActionData.Count == 0 || Count-- > 0) {
                yield return new WaitForSeconds(delay / 10f);
                StartCoroutine(RepeatTrigger());
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos() {
            TriggerAction Target = ObjectFactory.Get<TriggerAction>((ushort)ActionData.ObjectId);
            if (Target != null)
                Gizmos.DrawLine(transform.position, Target.transform.position);
        }

        private void OnDrawGizmosSelected() {

        }
#endif
    }
}