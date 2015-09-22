using UnityEngine;

using SystemShock.Object;

namespace SystemShock.TriggerActions {
    [ExecuteInEditMode]
    public class PropagateConditional : TriggerAction<ObjectInstance.Trigger.Propagate> {
        public TriggerAction Target1;
        public TriggerAction Target2;
        public TriggerAction Target3;
        public TriggerAction Target4;

        public bool condition;

        private void Start() {
            Target1 = ObjectFactory.Get<TriggerAction>(ActionData.ObjectToTrigger1);
            Target2 = ObjectFactory.Get<TriggerAction>(ActionData.ObjectToTrigger2);
            Target3 = ObjectFactory.Get<TriggerAction>(ActionData.ObjectToTrigger3);
            Target4 = ObjectFactory.Get<TriggerAction>(ActionData.ObjectToTrigger4);
        }

        protected override void DoAct() {
            condition = !condition;

            if (Target1 != null && condition)
                StartCoroutine(WaitAndTrigger(Target1, ActionData.Delay1));

            if (Target2 != null && !condition)
                StartCoroutine(WaitAndTrigger(Target2, ActionData.Delay2));

            if (Target3 != null && condition)
                StartCoroutine(WaitAndTrigger(Target3, ActionData.Delay3));

            if (Target4 != null && !condition)
                StartCoroutine(WaitAndTrigger(Target4, ActionData.Delay4));
        }

#if UNITY_EDITOR
        private void OnDrawGizmos() {
            if (Target1 != null)
                Gizmos.DrawLine(transform.position, Target1.transform.position);

            if (Target2 != null)
                Gizmos.DrawLine(transform.position, Target2.transform.position);

            if (Target3 != null)
                Gizmos.DrawLine(transform.position, Target3.transform.position);

            if (Target4 != null)
                Gizmos.DrawLine(transform.position, Target4.transform.position);
        }

        private void OnDrawGizmosSelected() {

        }
#endif
    }
}