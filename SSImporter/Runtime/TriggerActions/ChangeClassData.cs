using UnityEngine;

using SystemShock.Object;
using SystemShock.Resource;

namespace SystemShock.TriggerActions {
    [ExecuteInEditMode]
    public class ChangeClassData : TriggerAction<ObjectInstance.Trigger.ChangeClassData> {
        protected override void DoAct() {
            SystemShockObject Target = ObjectFactory.Get(ActionData.ObjectId);

            ObjectInstance objectInstance = Target.ObjectInstance;
            //IClassData classData = (IClassData)ActionData.Data.Read(Target.GetClassData().GetType());
            IClassData classData = (IClassData)Extensions.Write(ActionData).Read(Target.GetClassData().GetType());

            ObjectFactory.Replace(Target.ObjectId, objectInstance, classData);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos() {
            SystemShockObject Target = ObjectFactory.Get(ActionData.ObjectId);

            if (Target != null)
                Gizmos.DrawLine(transform.position, Target.transform.position);
        }

        private void OnDrawGizmosSelected() {

        }
#endif
    }
}