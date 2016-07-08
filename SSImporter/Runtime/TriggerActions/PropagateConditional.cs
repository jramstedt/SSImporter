using UnityEngine;

using SystemShock.Object;

namespace SystemShock.TriggerActions {
    [ExecuteInEditMode]
    public class PropagateConditional : TriggerAction<ObjectInstance.Trigger.Propagate> {
        public bool condition;

        protected override void DoAct() {
            condition = !condition;

            if(condition)
                WaitAndTrigger(ActionData.ObjectToTrigger1, ActionData.Delay1);

            if (!condition)
                WaitAndTrigger(ActionData.ObjectToTrigger2, ActionData.Delay2);

            if (condition)
                WaitAndTrigger(ActionData.ObjectToTrigger3, ActionData.Delay3);

            if (!condition)
                WaitAndTrigger(ActionData.ObjectToTrigger4, ActionData.Delay4);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos() {
            if (ObjectFactory == null)
                return;

            TriggerAction Target1 = ObjectFactory.Get<TriggerAction>(ActionData.ObjectToTrigger1);
            if (Target1 != null)
                Gizmos.DrawLine(transform.position, Target1.transform.position);

            TriggerAction Target2 = ObjectFactory.Get<TriggerAction>(ActionData.ObjectToTrigger2);
            if (Target2 != null)
                Gizmos.DrawLine(transform.position, Target2.transform.position);

            TriggerAction Target3 = ObjectFactory.Get<TriggerAction>(ActionData.ObjectToTrigger3);
            if (Target3 != null)
                Gizmos.DrawLine(transform.position, Target3.transform.position);

            TriggerAction Target4 = ObjectFactory.Get<TriggerAction>(ActionData.ObjectToTrigger4);
            if (Target4 != null)
                Gizmos.DrawLine(transform.position, Target4.transform.position);
        }

        private void OnDrawGizmosSelected() {

        }
#endif
    }
}