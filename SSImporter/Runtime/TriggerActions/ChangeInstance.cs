using System;
using UnityEngine;

using SystemShock.Object;
using SystemShock.Resource;

namespace SystemShock.TriggerActions {
    [ExecuteInEditMode]
    public class ChangeInstance : TriggerAction<ObjectInstance.Trigger.ChangeInstance> {
        private IChanger changer;

        private void Start() {
            if (!Application.isPlaying)
                return;

            if (ActionData.Action == ObjectInstance.Trigger.ChangeInstance.ChangeAction.ChangeRepulsor)
                changer = new ChangeRepulsor(this, ObjectFactory);
            else if (ActionData.Action == ObjectInstance.Trigger.ChangeInstance.ChangeAction.ChangeScreen)
                changer = new ChangeScreen(this, ObjectFactory);
            else if (ActionData.Action == ObjectInstance.Trigger.ChangeInstance.ChangeAction.ChangeCode)
                changer = new ChangeCode(this, ObjectFactory);
            else if (ActionData.Action == ObjectInstance.Trigger.ChangeInstance.ChangeAction.ResetButton)
                changer = new ResetButton(this, ObjectFactory);
            else if (ActionData.Action == ObjectInstance.Trigger.ChangeInstance.ChangeAction.ActivateDoor)
                changer = new ActivateDoor(this, ObjectFactory);
            else if (ActionData.Action == ObjectInstance.Trigger.ChangeInstance.ChangeAction.ReturnToMenu)
                changer = null; // TODO Return player to main menu
            else if (ActionData.Action == ObjectInstance.Trigger.ChangeInstance.ChangeAction.ChangeYaw)
                changer = new ChangeYaw(this, ObjectFactory);
            else if (ActionData.Action == ObjectInstance.Trigger.ChangeInstance.ChangeAction.ChangeEnemy)
                changer = null; // TODO Behaviour unknown
            else if (ActionData.Action == ObjectInstance.Trigger.ChangeInstance.ChangeAction.ChangeInterfaceCondition)
                changer = new ChangeInterfaceCondition(this, ObjectFactory);
            else if (ActionData.Action == ObjectInstance.Trigger.ChangeInstance.ChangeAction.ShowSystemAnalyzer)
                changer = null; // TODO Shows system analyzer in HUD
            else if (ActionData.Action == ObjectInstance.Trigger.ChangeInstance.ChangeAction.RadiatePlayer)
                changer = new RadiatePlayer(this, ObjectFactory);
            else if (ActionData.Action == ObjectInstance.Trigger.ChangeInstance.ChangeAction.ActivateIfPlayerYaw)
                changer = null; // TODO Should activate object if player looking between -45 and +45 from target yaw angle.
            else if (ActionData.Action == ObjectInstance.Trigger.ChangeInstance.ChangeAction.DisableKeypad)
                changer = null; // TODO Should disable keypad.
            else if (ActionData.Action == ObjectInstance.Trigger.ChangeInstance.ChangeAction.GameFailed)
                changer = null; // TODO Behaviour unknown
            else if (ActionData.Action == ObjectInstance.Trigger.ChangeInstance.ChangeAction.ChangeEnemyType)
                changer = new ChangeEnemyType(this, ObjectFactory);
        }

        protected override void DoAct() {
            if (changer != null)
                changer.Change();
        }

        private interface IChanger {
            void Change();

#if UNITY_EDITOR
            Transform[] Targets { get; }
#endif
        }

        private class ChangeRepulsor : IChanger {
            private Triggers.Repulsor Repulsor;
            private ObjectInstance.Trigger.ChangeInstance.ChangeRepulsor ActionData;

            public ChangeRepulsor(ChangeInstance instance, ObjectFactory objectFactory) {
                ActionData = instance.ActionData.Data.Read<ObjectInstance.Trigger.ChangeInstance.ChangeRepulsor>();

                if (ActionData.ObjectId == 0)
                    Repulsor = instance.GetComponent<Triggers.Repulsor>();
                else
                    Repulsor = objectFactory.Get<Triggers.Repulsor>((ushort)ActionData.ObjectId);                
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

            public Transform[] Targets { get { return new Transform[] { Repulsor.transform }; } }
        }

        private class ChangeScreen : IChanger {
            private TextScreen Screen;
            private ObjectInstance.Trigger.ChangeInstance.ChangeScreen ActionData;

            public ChangeScreen(ChangeInstance instance, ObjectFactory objectFactory) {
                ActionData = instance.ActionData.Data.Read<ObjectInstance.Trigger.ChangeInstance.ChangeScreen>();

                if (ActionData.ObjectId == 0)
                    Screen = instance.GetComponent<TextScreen>();
                else
                    Screen = objectFactory.Get<TextScreen>((ushort)ActionData.ObjectId);                
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

            public Transform[] Targets { get { return new Transform[] { Screen.transform }; } }
        }

        private class ChangeCode : IChanger {
            private Interfaces.KeyPad KeyPad;
            private ObjectInstance.Trigger.ChangeInstance.ChangeCode ActionData;

            public ChangeCode(ChangeInstance instance, ObjectFactory objectFactory) {
                ActionData = instance.ActionData.Data.Read<ObjectInstance.Trigger.ChangeInstance.ChangeCode>();

                if (ActionData.ObjectId == 0)
                    KeyPad = instance.GetComponent<Interfaces.KeyPad>();
                else
                    KeyPad = objectFactory.Get<Interfaces.KeyPad>((ushort)ActionData.ObjectId);
            }

            public void Change() {
                if (KeyPad == null)
                    return;

                if (ActionData.CodeIndex == 1)
                    KeyPad.ActionData.Combination1 = (ushort)ActionData.Code; // FIXME this is not the code. Code is somewhere else.
                else if(ActionData.CodeIndex == 2)
                    KeyPad.ActionData.Combination2 = (ushort)ActionData.Code; // FIXME this is not the code. Code is somewhere else.
            }
            public Transform[] Targets { get { return new Transform[] { KeyPad.transform }; } }
        }

        private class ResetButton : IChanger {
            private ToggleSprite ToggleSprite;
            private ObjectInstance.Trigger.ChangeInstance.ResetButton ActionData;
            public ResetButton(ChangeInstance instance, ObjectFactory objectFactory) {
                ActionData = instance.ActionData.Data.Read<ObjectInstance.Trigger.ChangeInstance.ResetButton>();

                if (ActionData.ObjectId == 0)
                    ToggleSprite = instance.GetComponent<ToggleSprite>();
                else
                    ToggleSprite = objectFactory.Get<ToggleSprite>((ushort)ActionData.ObjectId);
            }

            public void Change() {
                if (ToggleSprite == null)
                    return;

                ToggleSprite.SetFrame(1);
            }

            public Transform[] Targets { get { return new Transform[] { ToggleSprite.transform }; } }
        }

        private class ActivateDoor : IChanger {
            private Door Door;
            private ObjectInstance.Trigger.ChangeInstance.ActivateDoor ActionData;

            public ActivateDoor(ChangeInstance instance, ObjectFactory objectFactory) {
                ActionData = instance.ActionData.Data.Read<ObjectInstance.Trigger.ChangeInstance.ActivateDoor>();

                if (ActionData.ObjectId == 0)
                    Door = instance.GetComponent<Door>();
                else
                    Door = objectFactory.Get<Door>((ushort)ActionData.ObjectId);
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

            public Transform[] Targets { get { return new Transform[] { Door.transform }; } }
        }

        private class ChangeYaw : IChanger {
            private SystemShockObject Target;
            private ObjectInstance.Trigger.ChangeInstance.ChangeYaw ActionData;

            private byte State;
            private short Value;

            public ChangeYaw(ChangeInstance instance, ObjectFactory objectFactory) {
                ActionData = instance.ActionData.Data.Read<ObjectInstance.Trigger.ChangeInstance.ChangeYaw>();

                if (ActionData.ObjectId == 0)
                    Target = instance.GetComponent<SystemShockObject>();
                else
                    Target = objectFactory.Get<SystemShockObject>((ushort)ActionData.ObjectId);
                
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

            public Transform[] Targets { get { return new Transform[] { Target.transform }; } }
        }

        private class ChangeInterfaceCondition : IChanger {
            private InstanceObjects.Interface Interface;
            private ObjectInstance.Trigger.ChangeInstance.ChangeInterfaceCondition ActionData;
            public ChangeInterfaceCondition(ChangeInstance instance, ObjectFactory objectFactory) {
                ActionData = instance.ActionData.Data.Read<ObjectInstance.Trigger.ChangeInstance.ChangeInterfaceCondition>();

                if (ActionData.ObjectId == 0)
                    Interface = instance.GetComponent<InstanceObjects.Interface>();
                else
                    Interface = objectFactory.Get<InstanceObjects.Interface>((ushort)ActionData.ObjectId);                
            }

            public void Change() {
                if (Interface == null)
                    return;

                Interface.ClassData.ConditionVariable = ActionData.Variable;
                Interface.ClassData.ConditionValue = ActionData.Value;
                Interface.ClassData.ConditionFailedMessage = ActionData.FailedMessage;
            }

            public Transform[] Targets { get { return new Transform[] { Interface.transform }; } }
        }

        private class RadiatePlayer : IChanger {
            private GameObject Player;
            private SystemShockObject Watched;
            private ObjectInstance.Trigger.ChangeInstance.RadiatePlayer ActionData;
            public RadiatePlayer(ChangeInstance instance, ObjectFactory objectFactory) {
                ActionData = instance.ActionData.Data.Read<ObjectInstance.Trigger.ChangeInstance.RadiatePlayer>();

                Watched = objectFactory.Get((ushort)ActionData.ObjectId);
            }

            public void Change() {
                if (Player == null)
                    return;

                if (Watched.ObjectInstance.State >= ActionData.MinimumState)
                    Debug.Log("Radiation");
            }

            public Transform[] Targets { get { return new Transform[] { }; } }
        }

        private class ChangeEnemyType : IChanger {
            private ObjectInstance.Trigger.ChangeInstance.ChangeEnemyType ActionData;
            public ChangeEnemyType(ChangeInstance instance, ObjectFactory objectFactory) {
                ActionData = instance.ActionData.Data.Read<ObjectInstance.Trigger.ChangeInstance.ChangeEnemyType>();

                // objectFactory.GetObjectsByType(ActionData.CombinedId);
            }

            public void Change() {

            }

            public Transform[] Targets { get { return new Transform[] { }; } }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos() {
            if (changer != null && changer.Targets != null) {
                foreach (Transform target in changer.Targets)
                    Gizmos.DrawLine(transform.position, target.position);
            }
        }

        private void OnDrawGizmosSelected() {

        }
#endif
    }
}