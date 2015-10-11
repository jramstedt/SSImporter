﻿using UnityEngine;

using SystemShock.Object;
using SystemShock.Resource;

namespace SystemShock.TriggerActions {
    [ExecuteInEditMode]
    public class ChangeType : TriggerAction<ObjectInstance.Trigger.ChangeType> {
        protected override void DoAct() {
            SystemShockObject Target = ObjectFactory.Get((ushort)ActionData.ObjectId);

            ObjectInstance objectInstance = Target.ObjectInstance;
            objectInstance.Type = (byte)ActionData.NewType;

            ActionData.NewType = (byte)((ActionData.NewType & ~ActionData.Resettable) | (Target.ObjectInstance.Type & ActionData.Resettable));

            IClassData classData = Target.GetClassData();

            ObjectFactory.Replace(Target.ObjectId, objectInstance, classData);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos() {
            SystemShockObject Target = ObjectFactory.Get((ushort)ActionData.ObjectId);

            if (Target != null)
                Gizmos.DrawLine(transform.position, Target.transform.position);
        }

        private void OnDrawGizmosSelected() {

        }
#endif
    }
}