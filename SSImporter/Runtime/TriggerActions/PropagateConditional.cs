using UnityEngine;

using SystemShock.Object;

namespace SystemShock.TriggerActions {
    [ExecuteInEditMode]
    public class PropagateConditional : Triggerable<ObjectInstance.Trigger.Propagate> {
        public bool condition;

        protected override bool DoTrigger() {
            condition = !condition;

            if(condition)
                WaitAndTrigger(ActionData.ObjectToTrigger1, ActionData.Delay1);

            if (!condition)
                WaitAndTrigger(ActionData.ObjectToTrigger2, ActionData.Delay2);

            if (condition)
                WaitAndTrigger(ActionData.ObjectToTrigger3, ActionData.Delay3);

            if (!condition)
                WaitAndTrigger(ActionData.ObjectToTrigger4, ActionData.Delay4);

            return true;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos() {
            if (ObjectFactory == null)
                return;

            ITriggerable Target1 = ObjectFactory.Get<ITriggerable>(ActionData.ObjectToTrigger1);
            if (Target1 != null)
                Gizmos.DrawLine(transform.position, Target1.transform.position);

            ITriggerable Target2 = ObjectFactory.Get<ITriggerable>(ActionData.ObjectToTrigger2);
            if (Target2 != null)
                Gizmos.DrawLine(transform.position, Target2.transform.position);

            ITriggerable Target3 = ObjectFactory.Get<ITriggerable>(ActionData.ObjectToTrigger3);
            if (Target3 != null)
                Gizmos.DrawLine(transform.position, Target3.transform.position);

            ITriggerable Target4 = ObjectFactory.Get<ITriggerable>(ActionData.ObjectToTrigger4);
            if (Target4 != null)
                Gizmos.DrawLine(transform.position, Target4.transform.position);
        }

        private void OnDrawGizmosSelected() {

        }
#endif
    }
}