using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

using SystemShock.Object;
using SystemShock.Resource;
using SystemShock.Triggers;
using SystemShock.TriggerActions;

namespace SystemShock.InstanceObjects {
    public partial class Trigger : SystemShockObject<ObjectInstance.Trigger>, IActionProvider {
        private bool hasBeenActivated = false;
        private GameVariables gameVariables;

        private void Awake() {
            gameVariables = GameVariables.GetController();
        }

        protected override void InitializeInstance() {
            //Debug.LogFormat(gameObject, "Trigger {0}", ClassData.Action);

            bool ignoreAction = false;

            if (SubClass == 0) { // Triggers
                if (Type == 0) {
                    gameObject.AddComponent<Entry>();
                } else if(Type == 1) {
                    // NULL Trigger.
                } else if(Type == 2) {
                    gameObject.AddComponent<Floor>();
                } else if (Type == 3) {
                    gameObject.AddComponent<PlayerDeath>();
                } else if (Type == 4) {
                    gameObject.AddComponent<DeathWatch>();
                } else if (Type == 5) {
                    gameObject.AddComponent<AreaEnter>();
                } else if (Type == 6) {
                    gameObject.AddComponent<AreaContinous>();
                } else if (Type == 7) {
                    gameObject.AddComponent<AIHint>();
                } else if (Type == 8) {
                    gameObject.AddComponent<LevelEnter>();
                } else if (Type == 9) {
                    gameObject.AddComponent<Continuous>();
                } else if (Type == 10) {
                    gameObject.AddComponent<Repulsor>();
                    ignoreAction = true;
                } else if (Type == 11) {
                    gameObject.AddComponent<Ecology>();
                } else if (Type == 12) {
                    gameObject.AddComponent<Shodan>();
                }
            } else if (SubClass == 2) { // Trip beam
                if (Type == 0) {
                    // Trip beam
                }
            } else if (SubClass == 3) { // Markers
                if (Type == 0) {
                    // Bio hazard mark
                } else if (Type == 1) {
                    // Radiation hazard mark
                } else if (Type == 2) {
                    // Chemical hazard mark
                } else if (Type == 3) {
                    // Map note
                } else if (Type == 4) {
                    // Music marker
                }
            }

            //if (ClassData.ConditionVariable != 0)
            //    Debug.LogFormat(this, "{0} == {1}", ClassData.ConditionVariable, ClassData.ConditionValue);

            if (!ignoreAction) {
                if (ClassData.Action == ActionType.NoOp) {
                    // Nothing
                } else if (ClassData.Action == ActionType.TeleportPlayer) {
                    gameObject.AddComponent<TeleportPlayer>();
                } else if (ClassData.Action == ActionType.ResurrectPlayer) {
                    gameObject.AddComponent<ResurrectPlayer>();
                } else if (ClassData.Action == ActionType.SetPosition) {
                    gameObject.AddComponent<SetPosition>();
                } else if (ClassData.Action == ActionType.SetVariable) {
                    gameObject.AddComponent<SetVariable>();
                } else if (ClassData.Action == ActionType.Propagate) {
                    gameObject.AddComponent<Propagate>();
                } else if (ClassData.Action == ActionType.Lighting) {
                    gameObject.AddComponent<Lighting>();
                } else if (ClassData.Action == ActionType.Effect) {
                    gameObject.AddComponent<Effect>();
                } else if (ClassData.Action == ActionType.MovePlatform) {
                    gameObject.AddComponent<MovePlatform>();
                } else if (ClassData.Action == ActionType.PropagateRepeat) {
                    gameObject.AddComponent<PropagateRepeat>();
                } else if (ClassData.Action == ActionType.PropagateConditional) {
                    gameObject.AddComponent<PropagateConditional>();
                } else if (ClassData.Action == ActionType.Destroy) {
                    gameObject.AddComponent<Destroy>();
                } else if (ClassData.Action == ActionType.EmailPlayer) {
                    gameObject.AddComponent<EmailPlayer>();
                } else if (ClassData.Action == ActionType.RadiationTreatment) {
                    gameObject.AddComponent<RadiationTreatment>();
                } else if (ClassData.Action == ActionType.ChangeClassData) {
                    gameObject.AddComponent<ChangeClassData>();
                } else if (ClassData.Action == ActionType.ChangeFrameLoop) {
                    gameObject.AddComponent<ChangeFrameLoop>();
                } else if (ClassData.Action == ActionType.ChangeState) {
                    gameObject.AddComponent<ChangeState>();
                } else if (ClassData.Action == ActionType.Awaken) {
                    gameObject.AddComponent<Awaken>();
                } else if (ClassData.Action == ActionType.Message) {
                    gameObject.AddComponent<Message>();
                } else if (ClassData.Action == ActionType.Spawn) {
                    gameObject.AddComponent<Spawn>();
                } else if (ClassData.Action == ActionType.ChangeType) {
                    gameObject.AddComponent<ChangeType>();
                } else {
                    Debug.LogWarning(ClassData.Action, gameObject);
                }
            }
        }

        bool IActionProvider.CanActivate {
            get {
                if (hasBeenActivated && ClassData.OnceOnly == 1)
                    return false;

                bool canActivate = true;

                if (ClassData.ConditionVariable != 0) {
                    ushort conditionValue;
                    canActivate = gameVariables.TryGetValue(ClassData.ConditionVariable, out conditionValue) && conditionValue == ClassData.ConditionValue;
                }

                return hasBeenActivated = canActivate;
            }
        }

        byte[] IActionProvider.ActionData {
            get { return ClassData.Data; }
        }
    }
}