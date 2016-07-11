using System;
using UnityEngine;

using SystemShock.Object;
using SystemShock.Resource;
using SystemShock.Gameplay;

namespace SystemShock.TriggerActions {
    [ExecuteInEditMode]
    public class ChangeInstance : TriggerAction<ObjectInstance.Trigger.ChangeInstance> {
        private IChanger changer;

        private void Start() {
            if (!Application.isPlaying)
                return;

            if (ActionData.Action == ObjectInstance.Trigger.ChangeInstance.ChangeAction.ChangeRepulsor)
                changer = new ChangeRepulsor(this);
            else if (ActionData.Action == ObjectInstance.Trigger.ChangeInstance.ChangeAction.ChangeScreen)
                changer = new ChangeScreen(this);
            else if (ActionData.Action == ObjectInstance.Trigger.ChangeInstance.ChangeAction.ChangeCode)
                changer = new ChangeCode(this);
            else if (ActionData.Action == ObjectInstance.Trigger.ChangeInstance.ChangeAction.ResetButton)
                changer = new ResetButton(this);
            else if (ActionData.Action == ObjectInstance.Trigger.ChangeInstance.ChangeAction.ActivateDoor)
                changer = new ActivateDoor(this);
            else if (ActionData.Action == ObjectInstance.Trigger.ChangeInstance.ChangeAction.ReturnToMenu)
                changer = null; // TODO Return player to main menu
            else if (ActionData.Action == ObjectInstance.Trigger.ChangeInstance.ChangeAction.ChangeYaw)
                changer = new ChangeYaw(this);
            else if (ActionData.Action == ObjectInstance.Trigger.ChangeInstance.ChangeAction.ChangeEnemy)
                changer = null; // TODO Behaviour unknown
            else if (ActionData.Action == ObjectInstance.Trigger.ChangeInstance.ChangeAction.ChangeInterfaceCondition)
                changer = new ChangeInterfaceCondition(this);
            else if (ActionData.Action == ObjectInstance.Trigger.ChangeInstance.ChangeAction.ShowSystemAnalyzer)
                changer = null; // TODO Shows system analyzer in HUD
            else if (ActionData.Action == ObjectInstance.Trigger.ChangeInstance.ChangeAction.RadiatePlayer)
                changer = new RadiatePlayer(this);
            else if (ActionData.Action == ObjectInstance.Trigger.ChangeInstance.ChangeAction.ActivateIfPlayerYaw)
                changer = null; // TODO Should activate object if player looking between -45 and +45 from target yaw angle.
            else if (ActionData.Action == ObjectInstance.Trigger.ChangeInstance.ChangeAction.DisableKeypad)
                changer = null; // TODO Should disable keypad.
            else if (ActionData.Action == ObjectInstance.Trigger.ChangeInstance.ChangeAction.GameFailed)
                changer = null; // TODO Behaviour unknown
            else if (ActionData.Action == ObjectInstance.Trigger.ChangeInstance.ChangeAction.ChangeEnemyType)
                changer = new ChangeEnemyType(this);
        }

        protected override void DoAct() {
            if (changer != null)
                changer.Change();
        }

        private interface IChanger {
            void Change();

#if UNITY_EDITOR
            void OnDrawGizmos();
#endif
        }

        private abstract class BaseChanger<D, T> : IChanger where T : class {
            protected readonly D ActionData;

            protected readonly ChangeInstance changeInstance;
            protected readonly ObjectFactory objectFactory;

            public BaseChanger(ChangeInstance changeInstance) {
                ActionData = changeInstance.ActionData.Data.Read<D>();

                this.changeInstance = changeInstance;
                objectFactory = ObjectFactory.GetController();
            }

            protected T GetTarget(ushort objectId) {
                if (objectId == 0)
                    return changeInstance.GetComponent<T>();
                else
                    return objectFactory.Get<T>(objectId);
            }

            public abstract void Change();

#if UNITY_EDITOR
            public abstract void OnDrawGizmos();
#endif
        }

        private class ChangeRepulsor : BaseChanger<ObjectInstance.Trigger.ChangeInstance.ChangeRepulsor, Triggers.Repulsor> {
            public ChangeRepulsor(ChangeInstance changeInstance) : base(changeInstance) { }

            public override void Change() {
                var Repulsor = GetTarget((ushort)ActionData.ObjectId);
                if (Repulsor == null)
                    return;

                if (ActionData.ForceDirection == ObjectInstance.Trigger.ChangeInstance.ChangeRepulsor.Direction.Up) {
                    Repulsor.Data.ForceDirection = Triggers.Repulsor.RepulsorData.Direction.Up;
                } else if (ActionData.ForceDirection == ObjectInstance.Trigger.ChangeInstance.ChangeRepulsor.Direction.Down) {
                    Repulsor.Data.ForceDirection = Triggers.Repulsor.RepulsorData.Direction.Down;
                } else {
                    if (Repulsor.Data.ForceDirection == Triggers.Repulsor.RepulsorData.Direction.Up)
                        Repulsor.Data.ForceDirection = Triggers.Repulsor.RepulsorData.Direction.Down;
                    else
                        Repulsor.Data.ForceDirection = Triggers.Repulsor.RepulsorData.Direction.Up;
                }
            }

#if UNITY_EDITOR
            public override void OnDrawGizmos() {
                var Target = GetTarget((ushort)ActionData.ObjectId);
                Gizmos.DrawLine(changeInstance.transform.position, Target.transform.position);
            }
#endif
        }

        private class ChangeScreen : BaseChanger<ObjectInstance.Trigger.ChangeInstance.ChangeScreen, TextScreen> {
            public ChangeScreen(ChangeInstance changeInstance) : base(changeInstance) { }

            public override void Change() {
                var Screen = GetTarget((ushort)ActionData.ObjectId);
                if (Screen == null)
                    return;

                Screen.Frames = 0;
                Screen.Type = TextScreen.AnimationType.Normal;
                Screen.Texts = new string[] { UnityEngine.Random.Range(0, 9).ToString() };
                Screen.Alignment = TextAnchor.MiddleCenter;
                Screen.SmallText = false;
            }

#if UNITY_EDITOR
            public override void OnDrawGizmos() {
                var Target = GetTarget((ushort)ActionData.ObjectId);
                Gizmos.DrawLine(changeInstance.transform.position, Target.transform.position);
            }
#endif
        }

        private class ChangeCode : BaseChanger<ObjectInstance.Trigger.ChangeInstance.ChangeCode, Interfaces.KeyPad> {
            public ChangeCode(ChangeInstance changeInstance) : base(changeInstance) { }

            public override void Change() {
                var KeyPad = GetTarget((ushort)ActionData.ObjectId);
                if (KeyPad == null)
                    return;

                if (ActionData.CodeIndex == 1)
                    KeyPad.ActionData.Combination1 = (ushort)ActionData.Code; // FIXME this is not the code. Code is somewhere else.
                else if(ActionData.CodeIndex == 2)
                    KeyPad.ActionData.Combination2 = (ushort)ActionData.Code; // FIXME this is not the code. Code is somewhere else.
            }

#if UNITY_EDITOR
            public override void OnDrawGizmos() {
                var Target = GetTarget((ushort)ActionData.ObjectId);
                Gizmos.DrawLine(changeInstance.transform.position, Target.transform.position);
            }
#endif
        }

        private class ResetButton : BaseChanger<ObjectInstance.Trigger.ChangeInstance.ResetButton, ToggleSprite> {
            public ResetButton(ChangeInstance changeInstance) : base(changeInstance) { }

            public override void Change() {
                var ToggleSprite = GetTarget((ushort)ActionData.ObjectId);
                if (ToggleSprite == null)
                    return;

                ToggleSprite.SetFrame(1);
            }
#if UNITY_EDITOR
            public override void OnDrawGizmos() {
                var Target = GetTarget((ushort)ActionData.ObjectId);
                Gizmos.DrawLine(changeInstance.transform.position, Target.transform.position);
            }
#endif
        }

        private class ActivateDoor : BaseChanger<ObjectInstance.Trigger.ChangeInstance.ActivateDoor, Door> {
            public ActivateDoor(ChangeInstance changeInstance) : base(changeInstance) { }

            public override void Change() {
                var Door = GetTarget((ushort)ActionData.ObjectId);
                if (Door == null)
                    return;

                if (ActionData.TargetState == ObjectInstance.Trigger.ChangeInstance.ActivateDoor.State.Close)
                    Door.Open();
                else if (ActionData.TargetState == ObjectInstance.Trigger.ChangeInstance.ActivateDoor.State.Open)
                    Door.Close();
                else
                    Door.Activate();
            }
#if UNITY_EDITOR
            public override void OnDrawGizmos() {
                var Target = GetTarget((ushort)ActionData.ObjectId);
                Gizmos.DrawLine(changeInstance.transform.position, Target.transform.position);
            }
#endif
        }

        private class ChangeYaw : BaseChanger<ObjectInstance.Trigger.ChangeInstance.ChangeYaw, SystemShockObject> {
            private byte State;
            private short Value;

            public ChangeYaw(ChangeInstance changeInstance) : base(changeInstance) {
                State = 0;

                Value = GetTarget((ushort)ActionData.ObjectId).ObjectInstance.Yaw;
            }

            public override void Change() {
                if (Value == ActionData.Limit[State])
                    State = ActionData.Step[++State] > 0 ? State : (byte)0;

                if (ActionData.Limit[State] == 0)
                    Value += (short)ActionData.Step[State];
                else
                    Value += (short)(ActionData.Step[State] * ((Value - ActionData.Limit[State]) > 0 ? -1 : 1));

                GetTarget((ushort)ActionData.ObjectId).transform.localRotation = Quaternion.AngleAxis(Value / 256f * 360f, Vector3.up);
            }
#if UNITY_EDITOR
            public override void OnDrawGizmos() {
                var Target = GetTarget((ushort)ActionData.ObjectId);
                Gizmos.DrawLine(changeInstance.transform.position, Target.transform.position);
            }
#endif
        }

        private class ChangeInterfaceCondition : BaseChanger<ObjectInstance.Trigger.ChangeInstance.ChangeInterfaceCondition, InstanceObjects.Interface> {
            public ChangeInterfaceCondition(ChangeInstance changeInstance) : base(changeInstance) { }

            public override void Change() {
                var Interface = GetTarget((ushort)ActionData.ObjectId);
                if (Interface == null)
                    return;

                Interface.ClassData.ConditionVariable = ActionData.Variable;
                Interface.ClassData.ConditionValue = ActionData.Value;
                Interface.ClassData.ConditionFailedMessage = ActionData.FailedMessage;
            }
#if UNITY_EDITOR
            public override void OnDrawGizmos() {
                var Target = GetTarget((ushort)ActionData.ObjectId);
                Gizmos.DrawLine(changeInstance.transform.position, Target.transform.position);
            }
#endif
        }

        private class RadiatePlayer : BaseChanger<ObjectInstance.Trigger.ChangeInstance.RadiatePlayer, SystemShockObject> {
            public RadiatePlayer(ChangeInstance changeInstance) : base(changeInstance) { }

            public override void Change() {
                var Watched = GetTarget((ushort)ActionData.WatchedObjectId);
                if (Watched.ObjectInstance.State >= ActionData.MinimumState)
                    changeInstance.MessageBus.Send(new RadiatePlayerMessage(GetTarget((ushort)ActionData.ObjectId), (ushort)15u)); // TODO Hard coded. Check.
            }
#if UNITY_EDITOR
            public override void OnDrawGizmos() {
                var Target = GetTarget((ushort)ActionData.ObjectId);
                Gizmos.DrawLine(changeInstance.transform.position, Target.transform.position);

                Target = GetTarget((ushort)ActionData.WatchedObjectId);
                Gizmos.DrawLine(changeInstance.transform.position, Target.transform.position);
            }
#endif
        }

        private class ChangeEnemyType : BaseChanger<ObjectInstance.Trigger.ChangeInstance.ChangeEnemyType, SystemShockObject> {
            public ChangeEnemyType(ChangeInstance changeInstance) : base(changeInstance) { }

            public override void Change() {
                uint combinedId = ActionData.CombinedId;
                uint Class = (combinedId >> 16) & 0xFF;
                uint Subclass = (combinedId >> 8) & 0xFF;
                uint Type = combinedId & 0xFF;

                SystemShockObject[] ssObjects = objectFactory.GetAll((ObjectClass)Class, (byte)Subclass, (byte)Type);
                int i = ssObjects.Length;
                while (i-- > 0) {
                    SystemShockObject ssObject = ssObjects[i];

                    ObjectInstance objectInstance = ssObject.ObjectInstance;
                    objectInstance.Type = (byte)ActionData.NewType;

                    IClassData classData = ssObject.GetClassData();

                    objectFactory.Replace(ssObject.ObjectId, objectInstance, classData);
                }
            }
#if UNITY_EDITOR
            public override void OnDrawGizmos() {
                uint combinedId = ActionData.CombinedId;
                uint Class = (combinedId >> 16) & 0xFF;
                uint Subclass = (combinedId >> 8) & 0xFF;
                uint Type = combinedId & 0xFF;

                SystemShockObject[] ssObjects = objectFactory.GetAll((ObjectClass)Class, (byte)Subclass, (byte)Type);
                foreach(SystemShockObject Target in ssObjects)
                    Gizmos.DrawLine(changeInstance.transform.position, Target.transform.position);
            }
#endif
        }

#if UNITY_EDITOR
        private void OnDrawGizmos() {
            if (changer != null)
                changer.OnDrawGizmos();
        }

        private void OnDrawGizmosSelected() {

        }
#endif
    }
}