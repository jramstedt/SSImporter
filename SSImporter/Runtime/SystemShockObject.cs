using UnityEngine;
using System;
using System.Collections;
using System.Runtime.InteropServices;

using SystemShock.Resource;

namespace SystemShock.Object {
    public abstract class SystemShockObject : MonoBehaviour {
        public ObjectInstance ObjectInstance;

        public abstract ushort ObjectId { get; }
        public ObjectClass Class { get { return ObjectInstance.Class; } }
        public byte SubClass { get { return ObjectInstance.SubClass; } }
        public byte Type { get { return ObjectInstance.Type; } }
        public byte State { get { return ObjectInstance.State; } }

        public void Setup(ObjectInstance objectInstance, IClassData instanceData) {
            ObjectInstance = objectInstance;
            SetClassData(instanceData);

            InitializeInstance();
        }

        protected virtual void InitializeInstance() { }

        protected abstract void SetClassData(IClassData classData);
        public abstract IClassData GetClassData();

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
        ResurrectPlayer = 0x02,
        SetPosition = 0x03,
        SetVariable = 0x04,
        Unknown0x05 = 0x05,
        Propagate = 0x06,
        Lighting = 0x07,
        Effect = 0x08,
        MovePlatform = 0x09,
        Unknown0x0A = 0x0A,
        PropagateRepeat = 0x0B,
        PropagateConditional = 0x0C,
        Destroy = 0x0D,
        Unknown0x0E = 0x0E,
        EmailPlayer = 0x0F,
        RadiationTreatment = 0x10,
        ChangeClassData = 0x11,
        ChangeFrameLoop = 0x12,
        ChangeState = 0x13,
        Unknown0x14 = 0x14,
        Awaken = 0x15,
        Message = 0x16,
        Spawn = 0x17,
        ChangeType = 0x18
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
        public byte Unknown1;
        public byte State;
        public byte Unknown2;
        public byte Unknown3;

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

            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 2)]
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

            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 34)]
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

            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 10)]
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

                [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 3)]
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

            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 10)]
            public byte[] Data;

            [Serializable]
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public class AccessCard {
                public ushort Unknown1;
                public ushort AccessBitmask;

                [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 6)]
                public byte[] Fill;
            }

            public ushort ObjectId { get { return Link.ObjectIndex; } set { Link.ObjectIndex = value; } }
            public IClassData Clone() { return (IClassData)MemberwiseClone(); }
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class Interface : IClassData {
            public Link Link;

            public ActionType Action;
            public byte Unknown;
            public ushort ConditionVariableIndex;
            public ushort ConditionMessageIndex;

            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 18)]
            public byte[] Data;

            [Serializable]
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public class KeyPad {
                public ushort Combination;
                public ushort ObjectToTrigger1;
                public ushort Delay1;
                public ushort ObjectToTrigger2;
                public ushort Delay2;

                [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 12)]
                public byte[] Data;
            }

            [Serializable]
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public class CircuitAccess {
                public ushort ObjectToTrigger;
                public ushort Unknown1;

                public ushort StateObject;
                public byte Unknown2;
                public byte TypeIndicator;
                public ushort Configuration;

                [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 6)]
                public byte[] Data;
            }

            [Serializable]
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public class WireAccess {
                public ushort ObjectToTrigger;
                public ushort Unknown1;
                public byte Size;
                public byte TargetPower;
                public byte Unknown2;
                public byte TypeIndicator;
                public uint TargetWiresState;
                public uint CurrentWiresState;
                public byte Unknown3;
                public byte Unknown4;
            }

            [Serializable]
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public class Cyberjack {
                public ushort X;
                public ushort Unknown1;
                public ushort Y;
                public ushort Unknown2;
                public ushort Z;
                public ushort Unknown3;
                public ushort Level;
                public uint Unknown4;
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
                public ushort Unknown3;
            }

            [Serializable]
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public class ChargeStation {
                public uint Charge;
                public uint SecurityLimit;

                public uint Unknown1;
                public uint Unknown2;
                public ushort Unknown3;
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
            public byte Unknown2;
            public ushort ObjectToTrigger;

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

            public ActionType Action;
            public byte OnceOnly;
            public ushort ConditionVariable;
            public ushort ConditionValue;

            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 16)]
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
            public class Resurrect {
                [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 16)]
                public byte[] Unknown;
            }

            [Serializable]
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public class SetPosition {
                public ushort ObjectId;
                public ushort Scale;
                public uint TileX;
                public uint TileY;
                public uint Z;
            }

            [Serializable]
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public class SetVariable {
                public uint Variable;
                public ushort Value;
                public VariableAction Action;
                public ushort Message;

                [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 8)]
                public byte[] Unknown;

                public enum VariableAction : ushort {
                    Set,
                    Add
                }
            }

            [Serializable]
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public class Propagate {
                public ushort ObjectToTrigger1;
                public ushort Delay1;

                public ushort ObjectToTrigger2;
                public ushort Delay2;

                public ushort ObjectToTrigger3;
                public ushort Delay3;

                public ushort ObjectToTrigger4;
                public ushort Delay4;
            }

            [Serializable]
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public class Lighting {
                [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 16)]
                public byte[] Unknown;
            }

            [Serializable]
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public class Effect {
                [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 16)]
                public byte[] Unknown;
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
                public uint ObjectId;
                public uint Delay;
                public ushort Count;
                public ushort DelayVariationMin;
                public ushort DelayVariationMax;
                public ushort Unknown;
            }

            [Serializable]
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public class EmailPlayer {
                public ushort Message;

                [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 14)]
                public byte[] Unknown;
            }

            [Serializable]
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public class RadiationTreatment {
                [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 16)]
                public byte[] Unknown;
            }

            [Serializable]
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public class ChangeClassData {
                public uint ObjectId;

                [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 12)]
                public byte[] Data;
            }

            [Serializable]
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public class ChangeFrameLoop {
                public uint ObjectId;
                public byte StartFrameIndex;
                public ushort AnimationType;

                [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 9)]
                public byte[] Data;
            }

            [Serializable]
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public class ChangeState {
                public uint Unknown;
                public uint ObjectId;

                [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 8)]
                public byte[] Data;
            }

            [Serializable]
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public class Awaken {
                public uint Unknown1;
                public ushort Corner1ObjectId;
                public ushort Corner2ObjectId;
                public uint Unknown2;
                public ushort Unknown3;
                public ushort Unknown4;
            }

            [Serializable]
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public class Message {
                public ushort Success;
                public ushort Fail;

                [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 12)]
                public byte[] Unknown;
            }

            [Serializable]
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public class Spawn {
                public uint ObjectId;
                public ushort Corner1ObjectId;
                public ushort Corner2ObjectId;
                public uint Amount;
                public uint State;
            }

            [Serializable]
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public class ChangeType {
                public uint ObjectId;
                public byte NewType;

                [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 11)]
                public byte[] Unknown;
            }

            [Serializable]
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public class Disable {
                public ushort ObjectId;

                [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 14)]
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

            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 40)]
            public byte[] Data;

            public ushort ObjectId { get { return Link.ObjectIndex; } set { Link.ObjectIndex = value; } }
            public IClassData Clone() { return (IClassData)MemberwiseClone(); }
        }

        public ObjectInstance Clone() { return (ObjectInstance)MemberwiseClone(); }
    }
}
