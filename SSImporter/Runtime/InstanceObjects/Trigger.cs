﻿using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

using SystemShock.Object;
using SystemShock.Resource;
using SystemShock.Triggers;
using SystemShock.TriggerActions;

namespace SystemShock.InstanceObjects {
    public partial class Trigger : SystemShockObject<ObjectInstance.Trigger>, ITriggerActionProvider {
        protected override void InitializeInstance() {
            //Debug.LogFormat(gameObject, "Trigger {0}", ClassData.Action);

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

            if (ClassData.Action == ActionType.NoOp) {
                // Nothing
            } else if (ClassData.Action == ActionType.Transport) {
                gameObject.AddComponent<Transport>();
            } else if (ClassData.Action == ActionType.Resurrect) {
                gameObject.AddComponent<Resurrect>();
            } else if (ClassData.Action == ActionType.Clone) {
                gameObject.AddComponent<Clone>();
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
            } else if (ClassData.Action == ActionType.PropagateConditional) {
                gameObject.AddComponent<PropagateConditional>();
            } else if (ClassData.Action == ActionType.EmailPlayer) {
                gameObject.AddComponent<EmailPlayer>();
            } else if (ClassData.Action == ActionType.RadiationTreatment) {
                gameObject.AddComponent<RadiationTreatment>();
            } else if (ClassData.Action == ActionType.ChangeState) {
                gameObject.AddComponent<ChangeState>();
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

        public byte[] TriggerData {
            get { return ClassData.Data; }
        }
    }
}