using UnityEngine;

using SystemShock.Object;
using SystemShock.Resource;

namespace SystemShock.TriggerActions {
    [ExecuteInEditMode]
    public class ChangeType : Triggerable<ObjectInstance.Trigger.ChangeType> {
        protected override bool DoTrigger() {
            SystemShockObject Target = ObjectFactory.Get((ushort)ActionData.ObjectId);

            ObjectInstance objectInstance = Target.ObjectInstance;
            objectInstance.Type = (byte)ActionData.NewType;

            ActionData.NewType = (byte)((ActionData.NewType & ~ActionData.Resettable) | (Target.ObjectInstance.Type & ActionData.Resettable));

            IClassData classData = Target.GetClassData();

            ObjectFactory.Replace(Target.ObjectId, objectInstance, classData);

            return true;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos() {
            if (ObjectFactory == null)
                return;

            SystemShockObject Target = ObjectFactory.Get((ushort)ActionData.ObjectId);

            if (Target != null)
                Gizmos.DrawLine(transform.position, Target.transform.position);
        }

        private void OnDrawGizmosSelected() {

        }
#endif
    }
}