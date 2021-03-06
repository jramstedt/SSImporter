﻿using UnityEngine;
using System;
using System.Collections;
using System.Runtime.InteropServices;

using SystemShock.Resource;
using UnityEngine.EventSystems;
using SystemShock.UserInterface;
using SystemShock.Gameplay;

namespace SystemShock.Object {
    public abstract class SystemShockObject : MonoBehaviour, IPointerClickHandler {
        public ObjectInstance ObjectInstance;

        public abstract ushort ObjectId { get; }
        public ObjectClass Class { get { return ObjectInstance.Class; } }
        public byte SubClass { get { return ObjectInstance.SubClass; } }
        public byte Type { get { return ObjectInstance.Type; } }
        public byte State { get { return ObjectInstance.State; } set { ObjectInstance.State = value; } }
        public bool InUse { get { return ObjectInstance.InUse != 0; } }

        public uint CombinedType { get { return ObjectInstance.CombinedType; } }

        public void Setup(ObjectInstance objectInstance, IClassData instanceData) {
            ObjectInstance = objectInstance;
            SetClassData(instanceData);

            InitializeInstance();
        }

        protected virtual void InitializeInstance() { }

        protected abstract void SetClassData(IClassData classData);
        public abstract IClassData GetClassData();

        public virtual void OnPointerClick(PointerEventData eventData) {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;

            MessageBus.GetController().Send(new ItemInspectionMessage(this));

            if (eventData.clickCount >= 2)
                MessageBus.GetController().Send(new UseObjectMessage(this));
        }

        // TODO update X,Y,Z etc. if changed in unity.
    }

    public abstract class SystemShockObject<T> : SystemShockObject where T : IClassData {
        public T ClassData;

        public override ushort ObjectId { get { return ClassData.ObjectId; } }

        protected override void SetClassData(IClassData classData) {
            ClassData = (T)classData;
        }

        public override IClassData GetClassData() {
            return ClassData;
        }
    }

    public abstract class SystemShockObjectProperties : MonoBehaviour {
        public abstract ushort Index { get; }

        public abstract ushort ClassIndex { get; }

        public abstract BaseProperties Base { get; }

        public abstract void SetProperties(ObjectData properties);

    }

    public abstract class SystemShockObjectProperties<G, S> : SystemShockObjectProperties {
        public abstract G Generic { get; }
        public abstract S Specific { get; }
    }

    public interface IClassData {
        ushort ObjectId { get; set; }
        IClassData Clone();
    }
    
    public enum ObjectClass : byte {
        Weapon,
        Ammunition,
        Projectile,
        Explosive,
        DermalPatch,
        Hardware,
        SoftwareAndLog,
        Decoration,
        Item,
        Interface,
        DoorAndGrating,
        Animated,
        Trigger,
        Container,
        Enemy
    }

    public enum ActionType : byte {
        NoOp = 0x00,
        TeleportPlayer = 0x01,
        ChangePlayerVitality = 0x02,
        CloneOrMove = 0x03,
        SetVariable = 0x04,
        Cutscene = 0x05,
        Propagate = 0x06,
        Lighting = 0x07,
        Effect = 0x08,
        MovePlatform = 0x09,
        Unknown0x0A = 0x0A,
        PropagateRepeat = 0x0B,
        PropagateCycle = 0x0C,
        Destroy = 0x0D,
        Unknown0x0E = 0x0E,
        EmailPlayer = 0x0F,
        ChangeContamination = 0x10,
        ChangeClassData = 0x11,
        ChangeFrameLoop = 0x12,
        ChangeInstance = 0x13,
        Unknown0x14 = 0x14,
        Awaken = 0x15,
        Message = 0x16,
        Spawn = 0x17,
        ChangeType = 0x18
    }

    [Flags]
    public enum InstanceFlags : byte {
        Nothing = 0x00,
        F1 = 0x01,
        F2 = 0x02,
        F3 = 0x04,
        F4 = 0x08,
        Loot = 0x10,
        LootNoRef = 0x20,
        F7 = 0x40,
        ContainerActivate = 0x80
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ObjectInstance {
        public byte InUse;
        public ObjectClass Class;
        public byte SubClass;
        public ushort ClassTableIndex;
        public ushort CrossReferenceTableIndex;
        public ushort Prev;
        public ushort Next;
        public ushort X;
        public ushort Y;
        public byte Z;
        public byte Pitch;
        public byte Yaw;
        public byte Roll;
        public byte AIIndex;
        public byte Type;
        public ushort Hitpoints;
        public byte Details;
        public byte State;
        public byte Unknown2;
        public InstanceFlags Flags;

        public uint CombinedType { get { return (uint)Class << 16 | (uint)SubClass << 8 | Type; } }

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Link {
            public ushort ObjectIndex;
            public ushort Prev;
            public ushort Next;
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class Weapon : IClassData {
            public Link Link;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            public byte[] Data;

            [Serializable]
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public class Bullet {
                public byte AmmoType;
                public byte AmmoCount;
            }

            [Serializable]
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public class Energy {
                public byte Charge;
                public byte Temperature;
            }

            public ushort ObjectId { get { return Link.ObjectIndex; } set { Link.ObjectIndex = value; } }
            public IClassData Clone() { return (IClassData)MemberwiseClone(); }
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class Ammunition : IClassData {
            public Link Link;

            public ushort ObjectId { get { return Link.ObjectIndex; } set { Link.ObjectIndex = value; } }
            public IClassData Clone() { return (IClassData)MemberwiseClone(); }
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class Projectile : IClassData {
            public Link Link;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 34)]
            public byte[] Data;

            public ushort ObjectId { get { return Link.ObjectIndex; } set { Link.ObjectIndex = value; } }
            public IClassData Clone() { return (IClassData)MemberwiseClone(); }
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class Explosive : IClassData {
            public Link Link;

            public ushort Unknown1;
            public uint Flags;

            public ushort ObjectId { get { return Link.ObjectIndex; } set { Link.ObjectIndex = value; } }
            public IClassData Clone() { return (IClassData)MemberwiseClone(); }
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class DermalPatch : IClassData {
            public Link Link;

            public ushort ObjectId { get { return Link.ObjectIndex; } set { Link.ObjectIndex = value; } }
            public IClassData Clone() { return (IClassData)MemberwiseClone(); }
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class Hardware : IClassData {
            public Link Link;

            public byte Version;

            public ushort ObjectId { get { return Link.ObjectIndex; } set { Link.ObjectIndex = value; } }
            public IClassData Clone() { return (IClassData)MemberwiseClone(); }
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class SoftwareAndLog : IClassData {
            public Link Link;

            public byte Version;
            public byte LogIndex;
            public byte LevelIndex;

            public ushort ObjectId { get { return Link.ObjectIndex; } set { Link.ObjectIndex = value; } }
            public IClassData Clone() { return (IClassData)MemberwiseClone(); }
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class Decoration : IClassData {
            public Link Link;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
            public byte[] Data;

            [Serializable]
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public class SoftwareAndLogExtra {
                public ushort Version;
                public ushort Subclass;
                public ushort LevelIndex;
                public ushort Type;
                public ushort Fill;
            }

            [Serializable]
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public class Text {
                public ushort TextIndex;
                public ushort Font;
                public ushort Color;
                public uint Fill;
            }

            [Serializable]
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public class MaterialOverride {
                public ushort Frames;
                public ushort Unknown; // somewhat correlates with loop type
                public ushort Unknown1;
                public ushort StartFrameIndex;
                public ushort Unknown2;
            }

            [Serializable]
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public class Bridge {
                public enum SizeMask : byte {
                    X = 0x0F,
                    Y = 0xF0
                }

                public enum TextureMask : byte {
                    Texture = 0x7F,
                    MapTexture = 0x80
                }

                public ushort Unknown;
                public byte Size;
                public byte Height;
                public byte TopBottomTextures;
                public byte SideTextures;
                public byte ForceColor;

                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
                public byte[] Fill;
            }

            [Serializable]
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public class Camera {
                public ushort Unknown1;
                public ushort Rotating;
                public ushort Unknown2;
                public ushort Unknown3;
                public ushort Unknown4;
            }

            public ushort ObjectId { get { return Link.ObjectIndex; } set { Link.ObjectIndex = value; } }
            public IClassData Clone() { return (IClassData)MemberwiseClone(); }
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class Item : IClassData {
            public Link Link;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
            public byte[] Data;

            [Serializable]
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public class AccessCard {
                public ushort Unknown1;
                public uint AccessBitmask;

                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
                public byte[] Fill;
            }

            public ushort ObjectId { get { return Link.ObjectIndex; } set { Link.ObjectIndex = value; } }
            public IClassData Clone() { return (IClassData)MemberwiseClone(); }
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class Interface : IClassData {
            public Link Link;

            public ActionType ActionType;
            public byte Unknown;
            public ushort ConditionVariable;
            public byte ConditionValue;
            public byte ConditionFailedMessage;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 18)]
            public byte[] Data;

            [Serializable]
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public class KeyPad {
                public ushort Combination1;
                [ObjectReference] public ushort ObjectToTrigger1;
                public ushort Combination2;
                [ObjectReference] public ushort ObjectToTrigger2;
                public uint Unknown;
                public uint Message;
                public ushort Unknown2;
            }

            [Serializable]
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public class CircuitAccess {
                [ObjectReference] public ushort ObjectToTrigger;
                public ushort Unknown1;

                [ObjectReference] public ushort StateObject;
                public byte Unknown2;
                public byte TypeIndicator;
                public ushort Configuration;

                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
                public byte[] Data;
            }

            [Serializable]
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public class WireAccess {
                [ObjectReference] public ushort ObjectToTrigger;
                public ushort Unknown1;
                public byte Size;
                public byte TargetPower;
                public byte Unknown2;
                public byte TypeIndicator;
                public uint TargetWiresState;
                public uint CurrentWiresState;
            }

            [Serializable]
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public class Cyberjack {
                public uint X;
                public uint Y;
                public uint Z;
                public uint Level;
            }

            [Serializable]
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public class Elevator {
                public ushort TargetPanel1;
                public ushort TargetPanel2;
                public ushort TargetPanel3;
                public ushort TargetPanel4;
                public uint Unknown2;
                public ushort LevelsVisible;
                public ushort LevelsAccessible;
            }

            [Serializable]
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public class ChargeStation {
                public uint Charge;
                public uint RechargeTime;

                [ObjectReference] public ushort ObjectToTrigger1;
                public ushort Delay1;

                [ObjectReference] public ushort ObjectToTrigger2;
                public ushort Delay2;
            }

            public ushort ObjectId { get { return Link.ObjectIndex; } set { Link.ObjectIndex = value; } }
            public IClassData Clone() { return (IClassData)MemberwiseClone(); }
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class DoorAndGrating : IClassData {
            public Link Link;

            public ushort Lock;
            public byte LockMessage;
            public byte ForceColor;
            public byte AccessRequired;
            public byte SoundEffect;
            [ObjectReference] public ushort ObjectToTrigger;

            public ushort ObjectId { get { return Link.ObjectIndex; } set { Link.ObjectIndex = value; } }
            public IClassData Clone() { return (IClassData)MemberwiseClone(); }
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class Animated : IClassData {
            public Link Link;

            public uint Data;

            public ushort ObjectId { get { return Link.ObjectIndex; } set { Link.ObjectIndex = value; } }
            public IClassData Clone() { return (IClassData)MemberwiseClone(); }
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class Trigger : IClassData {
            public Link Link;

            public ActionType ActionType;
            public byte OnceOnly;
            public ushort ConditionVariable;
            public ushort ConditionValue;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public byte[] Data;

            [Serializable]
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public class TeleportPlayer {
                public uint TileX;
                public uint TileY;
                public ushort Z;
                public ushort Pitch;
                public ushort Yaw;
                public ushort Roll;
            }

            [Serializable]
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public class ChangePlayerVitality {
                public uint Unknown;
                public ushort HealthDelta;
                public ChangeOperator HealthChangeOperator;
                public ushort PowerDelta;
                public ChangeOperator PowerChangeOperator;
                public uint Unknown2;

                public enum ChangeOperator : ushort {
                    Remove,
                    Add
                }
            }

            [Serializable]
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public class CloneOrMove {
                [ObjectReference] public ushort ObjectId;
                public Actions Action;
                public uint TileX;
                public uint TileY;
                public uint Z;

                public enum Actions : ushort {
                    Clone,
                    Move
                }
            }

            [Serializable]
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public class SetVariable {
                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
                public ushort[] Variable;

                public ushort Value;

                public VariableOperation Operation;

                public uint MessageOn;
                public uint MessageOff;

                public enum BooleanAction : ushort {
                    SetZero = 0x0000,
                    SetOne = 0x0001,
                    Toggle = 0x0010
                }

                public enum VariableOperation : ushort {
                    Set,
                    Add,
                    Subtract,
                    Multiply,
                    Divide
                }
            }

            [Serializable]
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public class Cutscene {
                public CutsceneTypes CutsceneType;
                public uint EndGame;

                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
                public byte[] Unknown;

                public enum CutsceneTypes : uint {
                    Death,
                    Intro,
                    Ending
                }
            }

            [Serializable]
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public class Propagate {
                [ObjectReference] public ushort ObjectToTrigger1;
                public ushort Delay1;

                [ObjectReference] public ushort ObjectToTrigger2;
                public ushort Delay2;

                [ObjectReference] public ushort ObjectToTrigger3;
                public ushort Delay3;

                [ObjectReference] public ushort ObjectToTrigger4;
                public ushort Delay4;
            }

            [Serializable]
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public class Lighting {
                public ushort Unknown;
                [ObjectReference] public ushort Object;
                public TransitionTypes TransitionType;
                public uint Unknown2;
                public Surfaces Surface;
                public uint Unknown3;
                
                public enum TransitionTypes : ushort {
                    Immediate = 0x000,
                    Fade = 0x0001,
                    Flicker = 0x0100
                }
                
                public enum Surfaces : ushort {
                    Floor = 0x0000,
                    Ceiling = 0x0001,
                    FloorCeiling = 0x0002
                }
            }

            [Serializable]
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public class Effect {
                public ushort SoundId;
                public ushort LoopCount;
                public VisualEffects VisualEffect;
                public ushort Unknown; // length?
                public AdditionalVisualEffects AdditionalVisualEffect;
                public ushort Unknown2; // length?
                public uint Uknown3;

                public enum VisualEffects : ushort {
                    None,
                    PowerFailure,
                    ShakeCamera,
                    EscapePodSequence,
                    RedStaticFullscreen,
                    RedStatic
                }

                public enum AdditionalVisualEffects : ushort {
                    None,
                    White,
                    Pink,
                    GreyStatic,
                    VerticalPanning
                }
            }

            [Serializable]
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public class MovePlatform {
                public uint TileX;
                public uint TileY;
                public ushort TargetFloorHeight;
                public ushort TargetCeilingHeight;
                public ushort Speed;
                public ushort Unknown;
            }

            [Serializable]
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public class PropagateRepeat {
                [ObjectReference] public uint ObjectId;
                public uint Delay;
                public ushort Count;
                public ushort DelayVariationMin;
                public ushort DelayVariationMax;
                public ushort Unknown;
            }

            [Serializable]
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public class PropagateCycle {
                [ObjectReference]
                public ushort ObjectToTrigger1;
                public ushort Delay1;

                [ObjectReference]
                public ushort ObjectToTrigger2;
                public ushort Delay2;

                [ObjectReference]
                public ushort ObjectToTrigger3;
                public ushort Delay3;

                public uint NextIndex;
            }

            [Serializable]
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public class Destroy {
                [ObjectReference]
                public ushort ObjectToDestroy1;
                public ushort Delay1;

                [ObjectReference]
                public ushort ObjectToDestroy2;
                public ushort Delay2;

                [ObjectReference]
                public ushort ObjectToDestroy3;
                public ushort Delay3;

                public uint Message;
            }


           [Serializable]
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public class EmailPlayer {
                public ushort Message;

                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 14)]
                public byte[] Unknown;
            }

            [Serializable]
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public class ChangeContamination {
                public ushort DeltaValue;
                public ChangeOperators ChangeOperator;
                public ContaminationTypes ContaminationType;

                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
                public byte[] Unknown;

                public enum ChangeOperators : ushort {
                    Add,
                    Remove
                }

                [Flags]
                public enum ContaminationTypes : int {
                    Radiation = 0x00000004,
                    BioContamination = 0x00000008
                }
            }

            [Serializable]
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public class ChangeClassData {
                [ObjectReference] public ushort ObjectId;

                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 14)]
                public byte[] Data;
            }

            [Serializable]
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public class ChangeFrameLoop {
                [ObjectReference] public ushort ObjectId1;
                [ObjectReference] public ushort ObjectId2;
                public byte StartFrameIndex;
                public ushort AnimationType;

                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 9)]
                public byte[] Data;
            }

            [Serializable]
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public class ChangeInstance {
                public enum ChangeAction : uint {
                    ChangeRepulsor = 0x00000001,
                    ChangeScreen = 0x00000002,
                    ChangeCode = 0x00000003,
                    ResetButton = 0x00000004,
                    ActivateDoor = 0x00000005,
                    ReturnToMenu = 0x00000006,
                    ChangeYaw = 0x00000007,
                    ChangeEnemy = 0x00000008, // Behaviour unknown
                    Shodan = 0x00000009,
                    ChangeInterfaceCondition = 0x0000000A,
                    ShowSystemAnalyzer = 0x0000000B,
                    RadiatePlayer = 0x0000000C,
                    ActivateIfPlayerYaw = 0x0000000D,
                    CloseMFD = 0x0000000E,
                    LaserDestructionMessage = 0x0000000F,
                    ChangeEnemyType = 0x00000010
                }

                public ChangeAction Action;

                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
                public byte[] Data;

                [StructLayout(LayoutKind.Sequential, Pack = 1)]
                public class ChangeRepulsor {
                    [ObjectReference] public uint ObjectId;
                    public byte OffTextureIndex;
                    public byte OnTextureIndex;
                    public ushort Unknown;
                    public Direction ForceDirection;

                    public enum Direction : byte {
                        Toggle = 0x00,
                        Up = 0x01,
                        Down = 0x02
                    }
                }

                [StructLayout(LayoutKind.Sequential, Pack = 1)]
                public class ChangeScreen {
                    [ObjectReference] public uint ObjectId;
                    public uint NumberIndex;
                    public uint Unknown;
                }

                [StructLayout(LayoutKind.Sequential, Pack = 1)]
                public class ChangeCode {
                    [ObjectReference] public uint ObjectId;
                    public uint CodeIndex;
                    public uint Code;
                }

                [StructLayout(LayoutKind.Sequential, Pack = 1)]
                public class ResetButton {
                    public enum State : byte {
                        Inactive = 0x00,
                        Active = 0x01
                    }

                    [ObjectReference] public uint ObjectId;
                    public uint Unknown1;
                    public State TargetState;
                }

                [StructLayout(LayoutKind.Sequential, Pack = 1)]
                public class ActivateDoor {
                    // TODO Lock door at Diego boss fight!

                    [ObjectReference] public uint ObjectId;
                    public State TargetState;
                    public uint Unknown;


                    public enum State : uint {
                        Open = 0x01,
                        Close = 0x02,
                        Toggle = 0x03,
                        StopAutoClose = 0x04
                    }
                }

                [StructLayout(LayoutKind.Sequential, Pack = 1)]
                public class ChangeYaw {
                    [ObjectReference] public uint ObjectId;

                    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
                    public byte[] Step;

                    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
                    public byte[] Limit;
                }

                [StructLayout(LayoutKind.Sequential, Pack = 1)]
                public class ChangeEnemy {
                    public uint CombinedType;
                    public byte Unknown;
                }

                [StructLayout(LayoutKind.Sequential, Pack = 1)]
                public class ChangeInterfaceCondition {
                    [ObjectReference] public uint ObjectId;
                    public ushort Variable;
                    public byte Value;
                    public byte FailedMessage;
                }

                [StructLayout(LayoutKind.Sequential, Pack = 1)]
                public class RadiatePlayer {
                    [ObjectReference] public uint ObjectId;
                    public ushort WatchedObjectId;
                    public ushort MinimumState;
                    public uint Unknown;
                }

                [StructLayout(LayoutKind.Sequential, Pack = 1)]
                public class ActivateIfPlayerYaw {
                    public uint Angle;
                    [ObjectReference] public uint ObjectId;
                    public uint Unknown;
                }

                [StructLayout(LayoutKind.Sequential, Pack = 1)]
                public class CloseMFD {
                    [ObjectReference] public uint ObjectId;
                }

                [StructLayout(LayoutKind.Sequential, Pack = 1)]
                public class ChangeEnemyType {
                    public uint CombinedType;
                    public byte NewType;
                }
            }

            [Serializable]
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public class Awaken {
                public uint Unknown1;
                [ObjectReference] public ushort Corner1ObjectId;
                [ObjectReference] public ushort Corner2ObjectId;
                public uint Unknown2;
                public ushort Unknown3;
                public ushort Unknown4;
            }

            [Serializable]
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public class Message {
                public enum ImageMask : uint {
                    IsSpecial = 0x80000000
                }

                public uint BackgroundImage;

                public uint MessageId;

                public uint TextColor;

                public MessageLocations MessageLocation;

                public enum MessageLocations : uint {
                    MFD,
                    View
                }
            }

            [Serializable]
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public class Spawn {
                public uint CombinedType;
                [ObjectReference] public ushort Corner1ObjectId;
                [ObjectReference] public ushort Corner2ObjectId;
                public uint Amount;
                public uint State;
            }

            [Serializable]
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public class ChangeType {
                [ObjectReference] public uint ObjectId;
                public ushort NewType;
                public ushort Resettable;

                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
                public byte[] Unknown;
            }

            public ushort ObjectId { get { return Link.ObjectIndex; } set { Link.ObjectIndex = value; } }
            public IClassData Clone() { return (IClassData)MemberwiseClone(); }
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class Container : IClassData {
            public Link Link;

            public ushort Object1;
            public ushort Object2;
            public ushort Object3;
            public ushort Object4;
            public byte Width;
            public byte Depth;
            public byte Height;
            public byte TopBottomTexture;
            public byte SideTexture;
            public ushort Flags;

            public ushort ObjectId { get { return Link.ObjectIndex; } set { Link.ObjectIndex = value; } }
            public IClassData Clone() { return (IClassData)MemberwiseClone(); }
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class Enemy : IClassData {
            public Link Link;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 40)]
            public byte[] Data;

            public ushort ObjectId { get { return Link.ObjectIndex; } set { Link.ObjectIndex = value; } }
            public IClassData Clone() { return (IClassData)MemberwiseClone(); }
        }
    }
}
