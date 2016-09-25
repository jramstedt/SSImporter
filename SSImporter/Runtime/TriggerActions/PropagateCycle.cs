using UnityEngine;

using SystemShock.Object;

namespace SystemShock.TriggerActions {
    [ExecuteInEditMode]
    public class PropagateCycle : Triggerable<ObjectInstance.Trigger.PropagateCycle> {
        protected override bool DoTrigger() {
            uint nextIndex = ActionData.NextIndex++ & 0x03;
            if (nextIndex == 0)
                WaitAndTrigger(ActionData.ObjectToTrigger1, ActionData.Delay1);
            else if (nextIndex == 1)
                WaitAndTrigger(ActionData.ObjectToTrigger2, ActionData.Delay2);
            else
                WaitAndTrigger(ActionData.ObjectToTrigger3, ActionData.Delay3);

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
        }

        private void OnDrawGizmosSelected() {

        }
#endif
    }
}