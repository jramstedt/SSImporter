using UnityEngine;
using System;
using System.Collections;

using SystemShock.Object;
using SystemShock.Resource;
using SystemShock.TriggerActions;
using SystemShock.Interfaces;

namespace SystemShock.InstanceObjects {
    public partial class Interface : SystemShockObject<ObjectInstance.Interface>, IActionProvider {
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
                toggleSprite.SetFrame(State + 1);

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

            gameObject.AddComponent<Button>();
        }

        public byte[] ActionData { get { return ClassData.Data; } }
    }
}