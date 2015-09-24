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

            if (ActionData.Action == ObjectInstance.Trigger.ChangeInstance.ChangeAction.ChangeRepulsor)
                changer = new ChangeRepulsor(Target, ActionData.Data);
            else if (ActionData.Action == ObjectInstance.Trigger.ChangeInstance.ChangeAction.ChangeScreen)
                changer = new ChangeScreen(Target, ActionData.Data);
            else if (ActionData.Action == ObjectInstance.Trigger.ChangeInstance.ChangeAction.ChangeCode)
                changer = new ChangeCode(Target, ActionData.Data);
            else if (ActionData.Action == ObjectInstance.Trigger.ChangeInstance.ChangeAction.ResetButton)
                changer = new ResetButton(Target, ActionData.Data);
            else if (ActionData.Action == ObjectInstance.Trigger.ChangeInstance.ChangeAction.ActivateDoor)
                changer = new ActivateDoor(Target, ActionData.Data);
            else if (ActionData.Action == ObjectInstance.Trigger.ChangeInstance.ChangeAction.ChangeYaw)
                changer = new ChangeYaw(Target, ActionData.Data);
            else if (ActionData.Action == ObjectInstance.Trigger.ChangeInstance.ChangeAction.ChangeInterfaceCondition)
                changer = new ChangeInterfaceCondition(Target, ActionData.Data);
            else if (ActionData.Action == ObjectInstance.Trigger.ChangeInstance.ChangeAction.RadiatePlayer)
                changer = new RadiatePlayer(Target, ActionData.Data, ObjectFactory);
        }

        protected override void DoAct() {
            if (changer != null)
                changer.Change();
        }

        private interface IChanger {
            void Change();
        }

        private class ChangeRepulsor : IChanger {
            private Triggers.Repulsor Repulsor;
            private ObjectInstance.Trigger.ChangeInstance.ChangeRepulsor ActionData;

            public ChangeRepulsor(SystemShockObject target, byte[] data) {
                Repulsor = target.GetComponent<Triggers.Repulsor>();
                ActionData = data.Read<ObjectInstance.Trigger.ChangeInstance.ChangeRepulsor>();
            }

            public void Change() {
                if (Repulsor == null)
                    return;

                if(ActionData.ForceDirection == ObjectInstance.Trigger.ChangeInstance.ChangeRepulsor.Direction.Up) {
                    Repulsor.Data.ForceDirection = Triggers.Repulsor.RepulsorData.Direction.Up;
                } else if(ActionData.ForceDirection == ObjectInstance.Trigger.ChangeInstance.ChangeRepulsor.Direction.Down) {
                    Repulsor.Data.ForceDirection = Triggers.Repulsor.RepulsorData.Direction.Down;
                } else {
                    if (Repulsor.Data.ForceDirection == Triggers.Repulsor.RepulsorData.Direction.Up)
                        Repulsor.Data.ForceDirection = Triggers.Repulsor.RepulsorData.Direction.Down;
                    else
                        Repulsor.Data.ForceDirection = Triggers.Repulsor.RepulsorData.Direction.Up;
                }
            }
        }

        private class ChangeScreen : IChanger {
            private TextScreen Screen;
            private ObjectInstance.Trigger.ChangeInstance.ChangeScreen ActionData;

            public ChangeScreen(SystemShockObject target, byte[] data) {
                Screen = target.GetComponent<TextScreen>();
                ActionData = data.Read<ObjectInstance.Trigger.ChangeInstance.ChangeScreen>();
            }

            public void Change() {
                if (Screen == null)
                    return;

                Screen.Frames = 0;
                Screen.Type = TextScreen.AnimationType.Normal;
                Screen.Texts = new string[] { UnityEngine.Random.Range(0, 9).ToString() };
                Screen.Alignment = TextAnchor.MiddleCenter;
                Screen.SmallText = false;
            }
        }

        private class ChangeCode : IChanger {
            private ObjectInstance.Interface.KeyPad KeyPad;
            private ObjectInstance.Trigger.ChangeInstance.ChangeCode ActionData;

            public ChangeCode(SystemShockObject target, byte[] data) {
                KeyPad = target.GetComponent<ObjectInstance.Interface.KeyPad>();
                ActionData = data.Read<ObjectInstance.Trigger.ChangeInstance.ChangeCode>();
            }

            public void Change() {
                if (KeyPad == null)
                    return;

                if (ActionData.CodeIndex == 1)
                    KeyPad.Combination1 = (ushort)ActionData.Code; // FIXME this is not the code. Code is somewhere else.
                else if(ActionData.CodeIndex == 2)
                    KeyPad.Combination2 = (ushort)ActionData.Code; // FIXME this is not the code. Code is somewhere else.
            }
        }

        private class ResetButton : IChanger {
            private ToggleSprite ToggleSprite;
            private ObjectInstance.Trigger.ChangeInstance.ResetButton ActionData;
            public ResetButton(SystemShockObject target, byte[] data) {
                ToggleSprite = target.GetComponent<ToggleSprite>();
                ActionData = data.Read<ObjectInstance.Trigger.ChangeInstance.ResetButton>();
            }

            public void Change() {
                if (ToggleSprite == null)
                    return;

                ToggleSprite.SetFrame(1);
            }
        }

        private class ActivateDoor : IChanger {
            private Door Door;
            private ObjectInstance.Trigger.ChangeInstance.ActivateDoor ActionData;

            public ActivateDoor(SystemShockObject target, byte[] data) {
                Door = target.GetComponent<Door>();
                ActionData = data.Read<ObjectInstance.Trigger.ChangeInstance.ActivateDoor>();
            }

            public void Change() {
                if (Door == null)
                    return;

                if (ActionData.TargetState == ObjectInstance.Trigger.ChangeInstance.ActivateDoor.State.Close)
                    Door.Open();
                else if (ActionData.TargetState == ObjectInstance.Trigger.ChangeInstance.ActivateDoor.State.Open)
                    Door.Close();
                else
                    Door.Activate();
            }
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

        private class ChangeInterfaceCondition : IChanger {
            private InstanceObjects.Interface Interface;
            private ObjectInstance.Trigger.ChangeInstance.ChangeInterfaceCondition ActionData;
            public ChangeInterfaceCondition(SystemShockObject target, byte[] data) {
                Interface = target.GetComponent<InstanceObjects.Interface>();
                ActionData = data.Read<ObjectInstance.Trigger.ChangeInstance.ChangeInterfaceCondition>();
            }

            public void Change() {
                if (Interface == null)
                    return;

                Interface.ClassData.ConditionVariable = ActionData.Variable;
                Interface.ClassData.ConditionValue = ActionData.Value;
                Interface.ClassData.ConditionFailedMessage = ActionData.FailedMessage;
            }
        }

        private class RadiatePlayer : IChanger {
            private GameObject Player;
            private SystemShockObject Watched;
            private ObjectInstance.Trigger.ChangeInstance.RadiatePlayer ActionData;
            public RadiatePlayer(SystemShockObject target, byte[] data, ObjectFactory objectFactory) {
                //Interface = target.GetComponent<InstanceObjects.Interface>();
                ActionData = data.Read<ObjectInstance.Trigger.ChangeInstance.RadiatePlayer>();

                Watched = objectFactory.Get((ushort)ActionData.ObjectId);
            }

            public void Change() {
                if (Player == null)
                    return;

                if (Watched.ObjectInstance.State >= ActionData.MinimumState)
                    Debug.Log("Radiation");
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