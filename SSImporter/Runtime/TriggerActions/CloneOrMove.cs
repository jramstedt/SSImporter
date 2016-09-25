using UnityEngine;

using SystemShock.Object;

namespace SystemShock.TriggerActions {
    [ExecuteInEditMode]
    public class CloneOrMove : Triggerable<ObjectInstance.Trigger.CloneOrMove> {
        private Vector3 targetPosition;

        protected override void Awake() {
            base.Awake();

            targetPosition = new Vector3(ActionData.TileX, ActionData.Z * ObjectFactory.LevelInfo.HeightFactor, ActionData.TileY);
        }

        protected override bool DoTrigger() {
            SystemShockObject Target = ObjectFactory.Get(ActionData.ObjectId);

            if (ActionData.Action == (ushort)ObjectInstance.Trigger.CloneOrMove.Actions.Clone) {
                ObjectInstance objectInstance = Target.ObjectInstance;
                IClassData classData = Target.GetClassData();
                classData.ObjectId = ObjectFactory.GetFreeObjectId();
                Target = ObjectFactory.Instantiate(objectInstance, classData);
            }

            Rigidbody rigidbody = Target.GetComponent<Rigidbody>();
            if (rigidbody != null) {
                Vector3 oldPos = rigidbody.position;
                rigidbody.position = new Vector3(targetPosition.x + (oldPos.x % 1), targetPosition.y, targetPosition.z + (oldPos.z % 1));
            } else {
                Vector3 oldPos = Target.transform.position;
                Target.transform.position = new Vector3(targetPosition.x + (oldPos.x % 1), targetPosition.y, targetPosition.z + (oldPos.z % 1));
            }

            return true;
        }
    }
}

