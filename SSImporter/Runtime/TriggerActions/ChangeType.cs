using UnityEngine;

using SystemShock.Object;
using SystemShock.Resource;

namespace SystemShock.TriggerActions {
    [ExecuteInEditMode]
    public class ChangeType : TriggerAction<ObjectInstance.Trigger.ChangeType> {
        public SystemShockObject Target;

        private void Start() {
            Target = ObjectFactory.Get((ushort)ActionData.ObjectId);
        }

        protected override void DoAct() {
        ObjectInstance objectInstance = Target.ObjectInstance;
            objectInstance.Type = (byte)ActionData.NewType;

            ActionData.NewType = (byte)((ActionData.NewType & ~ActionData.Resettable) | (Target.ObjectInstance.Type & ActionData.Resettable));

            IClassData classData = Target.GetClassData();

            ObjectFactory.Destroy(Target.ObjectId);
            Target = ObjectFactory.Instantiate(objectInstance, classData); // FIXME
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