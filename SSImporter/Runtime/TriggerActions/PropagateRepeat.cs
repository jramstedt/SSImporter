using UnityEngine;

using SystemShock.Object;
using System.Collections;

namespace SystemShock.TriggerActions {
    [ExecuteInEditMode]
    public class PropagateRepeat : Triggerable<ObjectInstance.Trigger.PropagateRepeat> {
        public int Count;

        protected override bool DoTrigger() {
            Count = ActionData.Count;

            StartCoroutine(RepeatTrigger());

            return true;
        }
        private IEnumerator RepeatTrigger() {
            long delay = ActionData.Delay + Random.Range(ActionData.DelayVariationMin, ActionData.DelayVariationMax);

            ITriggerable Target = ObjectFactory.Get<ITriggerable>((ushort)ActionData.ObjectId);
            if (Target != null)
                Target.Trigger();
            else
                yield break;

            if (ActionData.Count == 0 || Count-- > 0) {
                yield return new WaitForSeconds(delay / 10f);
                StartCoroutine(RepeatTrigger());
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos() {
            if (ObjectFactory == null)
                return;

            ITriggerable Target = ObjectFactory.Get<ITriggerable>((ushort)ActionData.ObjectId);
            if (Target != null)
                Gizmos.DrawLine(transform.position, Target.transform.position);
        }

        private void OnDrawGizmosSelected() {

        }
#endif
    }
}