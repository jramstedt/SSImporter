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
        protected override void InitializeInstance() {
            //Debug.LogFormat(gameObject, "Trigger {0}", ClassData.Action);

            bool ignoreAction = false;

            if (SubClass == 0) { // Triggers
                if (Type == 0) {
                    gameObject.AddComponent<Entry>();
                } else if(Type == 1) {
                    gameObject.AddComponent<Null>();
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
                ActionType actionType = ClassData.ActionType;

                if (actionType == ActionType.NoOp) {
                    // Nothing
                } else if (actionType == ActionType.TeleportPlayer) {
                    gameObject.AddComponent<TeleportPlayer>();
                } else if (actionType == ActionType.ResurrectPlayer) {
                    gameObject.AddComponent<ResurrectPlayer>();
                } else if (actionType == ActionType.SetPosition) {
                    gameObject.AddComponent<SetPosition>();
                } else if (actionType == ActionType.SetVariable) {
                    gameObject.AddComponent<SetVariable>();
                } else if (actionType == ActionType.Propagate) {
                    gameObject.AddComponent<Propagate>();
                } else if (actionType == ActionType.Lighting) {
                    gameObject.AddComponent<Lighting>();
                } else if (actionType == ActionType.Effect) {
                    gameObject.AddComponent<Effect>();
                } else if (actionType == ActionType.MovePlatform) {
                    gameObject.AddComponent<MovePlatform>();
                } else if (actionType == ActionType.PropagateRepeat) {
                    gameObject.AddComponent<PropagateRepeat>();
                } else if (actionType == ActionType.PropagateConditional) {
                    gameObject.AddComponent<PropagateConditional>();
                } else if (actionType == ActionType.Destroy) {
                    gameObject.AddComponent<Destroy>();
                } else if (actionType == ActionType.EmailPlayer) {
                    gameObject.AddComponent<EmailPlayer>();
                } else if (actionType == ActionType.RadiationTreatment) {
                    gameObject.AddComponent<RadiationTreatment>();
                } else if (actionType == ActionType.ChangeClassData) {
                    gameObject.AddComponent<ChangeClassData>();
                } else if (actionType == ActionType.ChangeFrameLoop) {
                    gameObject.AddComponent<ChangeFrameLoop>();
                } else if (actionType == ActionType.ChangeInstance) {
                    gameObject.AddComponent<ChangeInstance>();
                } else if (actionType == ActionType.Awaken) {
                    gameObject.AddComponent<Awaken>();
                } else if (actionType == ActionType.Message) {
                    gameObject.AddComponent<Message>();
                } else if (actionType == ActionType.Spawn) {
                    gameObject.AddComponent<Spawn>();
                } else if (actionType == ActionType.ChangeType) {
                    gameObject.AddComponent<ChangeType>();
                } else {
                    Debug.LogWarning(actionType, gameObject);
                }
            }
        }

        public byte[] ActionData { get { return ClassData.Data; } }
    }
}