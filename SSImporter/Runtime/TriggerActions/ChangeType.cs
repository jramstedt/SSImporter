using UnityEngine;

using SystemShock.Object;
using SystemShock.Resource;

namespace SystemShock.TriggerActions {
    [ExecuteInEditMode]
    public class ChangeType : Triggerable<ObjectInstance.Trigger.ChangeType> {
        public SystemShockObject Target;

        private LevelInfo levelInfo;
        private ObjectFactory objectFactory;

        protected override void Awake() {
            base.Awake();

            objectFactory = ObjectFactory.GetController();
            levelInfo = objectFactory.LevelInfo;
        }

        private void Start() {
            if (ActionData.ObjectId != 0 && !levelInfo.Objects.TryGetValue((ushort)ActionData.ObjectId, out Target))
                Debug.Log("Tried to find object! " + ActionData.ObjectId, this);
        }

        public override void Trigger() {
            if (!CanActivate)
                return;

            ObjectInstance objectInstance = Target.ObjectInstance.Clone();
            objectInstance.Type = ActionData.NewType;

            IClassData classData = Target.GetClassData();

            objectFactory.Destroy(Target.ObjectId);
            objectFactory.Instantiate(objectInstance, classData);
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