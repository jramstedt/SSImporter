using System;
using UnityEngine;

using SystemShock.Object;
using SystemShock.Resource;

namespace SystemShock.TriggerActions {
    [ExecuteInEditMode]
    public class ChangeInstance : TriggerAction<ObjectInstance.Trigger.ChangeInstance> {
        public SystemShockObject Target;

        private IChanger changer;

        private void Start() {
            if (ActionData.ObjectId == 0)
                Target = GetComponent<SystemShockObject>();
            else
                Target = ObjectFactory.Get((ushort)ActionData.ObjectId);

            if (!Application.isPlaying)
                return;

            if (ActionData.Action == ObjectInstance.Trigger.ChangeInstance.ChangeAction.ChangeYaw)
                changer = new ChangeYaw(Target, ActionData.Data);
        }

        protected override void DoAct() {
            if (changer != null)
                changer.Change();
        }

        private interface IChanger {
            void Change();
        }

        private class ChangeYaw : IChanger {
            private SystemShockObject Target;
            private ObjectInstance.Trigger.ChangeInstance.ChangeYaw ActionData;

            private byte State;
            private short Value;

            public ChangeYaw(SystemShockObject target, byte[] data) {
                Target = target;
                ActionData = data.Read<ObjectInstance.Trigger.ChangeInstance.ChangeYaw>();

                State = 0;
                Value = Target.ObjectInstance.Yaw;
            }

            public void Change() {
                if (Value == ActionData.Limit[State])
                    State = ActionData.Step[++State] > 0 ? State : (byte)0;

                if (ActionData.Limit[State] == 0)
                    Value += (short)ActionData.Step[State];
                else
                    Value += (short)(ActionData.Step[State] * ((Value - ActionData.Limit[State]) > 0 ? -1 : 1));

                Target.transform.localRotation = Quaternion.AngleAxis(Value / 256f * 360f, Vector3.up);
            }
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