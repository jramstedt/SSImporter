﻿using UnityEngine;
using System;
using System.Collections;

using SystemShock.Object;
using SystemShock.Resource;
using SystemShock.TriggerActions;
using SystemShock.Interfaces;

namespace SystemShock.InstanceObjects {
    public partial class Interface : SystemShockObject<ObjectInstance.Interface>, ITriggerActionProvider {
        protected override void InitializeInstance() {
            //SystemShockObjectProperties properties = GetComponent<SystemShockObjectProperties>();

            if (SubClass == 0) {
                ObjectPropertyLibrary objectPropertyLibrary = ObjectPropertyLibrary.GetLibrary(@"objprop.dat");
                SpriteLibrary objartLibrary = SpriteLibrary.GetLibrary(@"objart.res");

                uint spriteIndex = objectPropertyLibrary.GetSpriteOffset((uint)Class << 16 | (uint)SubClass << 8 | Type);

                SpriteDefinition[] Frames = new SpriteDefinition[3];
                Array.Copy(objartLibrary.GetSpriteAnimation(0).Sprites, spriteIndex, Frames, 0, Frames.Length);

                MeshProjector meshProjector = gameObject.GetComponentInChildren<MeshProjector>();
                meshProjector.UVRect = new Rect(0f, 0f, 1f, 1f);

                ToggleSprite toggleSprite = gameObject.AddComponent<ToggleSprite>();
                toggleSprite.Frames = Frames;
                toggleSprite.SetFrame(AnimationState + 1);

                UseAsTrigger();
            } else if (SubClass == 2) {
                if (Type == 0) {
                    // Cyberspace terminal
                } else if (Type == 1) {
                    // Energy recharge station
                }
            } else if(SubClass == 3) {
                if (Type == 0 || Type == 1 || Type == 2 || Type == 3) {
                    gameObject.AddComponent<CircuitAccess>();
                } else if (Type == 4 || Type == 5 || Type == 6) {
                    gameObject.AddComponent<ElevatorPanel>();
                } else if (Type == 7 || Type == 8) {
                    gameObject.AddComponent<KeyPad>();
                } else if (Type == 9 || Type == 10) {
                    gameObject.AddComponent<WireAccess>();
                }
            } else {
                UseAsTrigger();
            }
        }

        private void UseAsTrigger() {
            if (ClassData.Action == ActionType.Transport) {
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