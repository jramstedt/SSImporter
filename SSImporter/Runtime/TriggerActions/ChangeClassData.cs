using UnityEngine;

using SystemShock.Object;
using SystemShock.Resource;

namespace SystemShock.TriggerActions {
    [ExecuteInEditMode]
    public class ChangeClassData : TriggerAction<ObjectInstance.Trigger.ChangeClassData> {
        public SystemShockObject Target;

        private void Start() {
            Target = ObjectFactory.Get(ActionData.ObjectId);
        }

        protected override void DoAct() {
            ObjectInstance objectInstance = Target.ObjectInstance;
            IClassData classData = (IClassData)ActionData.Data.Read(Target.GetClassData().GetType());

            ObjectFactory.Destroy(Target.ObjectId);
            ObjectFactory.Instantiate(objectInstance, classData);
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