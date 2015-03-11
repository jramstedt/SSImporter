using UnityEngine;
using System;
using System.Collections;
using System.Runtime.InteropServices;

using SystemShock.Resource;

namespace SystemShock.Object {
    public abstract class SystemShockObject : MonoBehaviour {
        public ObjectClass Class;
        public byte SubClass;
        public byte Type;

        public byte AIIndex;
        public ushort Hitpoints;
        public byte Unknown1;
        public byte AnimationState;

        public byte Unknown2;
        public byte Unknown3;

        public virtual void SetClassData(object classData) { }

        public virtual void InitializeInstance() { }
    }
    public abstract class SystemShockObject<T> : SystemShockObject {
        public T ClassData;
    }

    public abstract class SystemShockObjectProperties : MonoBehaviour {
        public abstract BaseProperties Base { get; }

        public virtual void SetProperties(ObjectData properties) { }
    }

    public abstract class SystemShockObjectProperties<G, S> : SystemShockObjectProperties {
        public abstract G Generic { get; }
        public abstract S Specific { get; }
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
        Toggle = 0x00,
        Transport = 0x01,
        Resurrection = 0x02,
        Clone = 0x03,
        SetVariable = 0x04,
        Unknown0x05 = 0x05,
        Propagate = 0x06,
        Lighting = 0x07,
        Effext = 0x08,
        MovePlatform = 0x09,
        Unknown0x0A = 0x0A,
        Unknown0x0B = 0x0B,
        PropagateConditional = 0x0C,
        Unknown0x0D = 0x0D,
        Unknown0x0E = 0x0E,
        EmailPlayer = 0x0F,
        RadiationTreatment = 0x10,
        Unknown0x11 = 0x11,
        Unknown0x12 = 0x12,
        ChangeState = 0x13,
        Unknown0x14 = 0x14,
        Unknown0x15 = 0x15,
        Message = 0x16,
        Spawn = 0x17,
        ChangeType = 0x18
    }

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
        public byte AnimationState;
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
        public class Weapon {
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
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class Ammunition {
            public Link Link;
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class Projectile {
            public Link Link;

            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 34)]
            public byte[] Data;
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class Explosive {
            public Link Link;

            public ushort Unknown1;
            public uint Flags;
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class DermalPatch {
            public Link Link;
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class Hardware {
            public Link Link;

            public byte Version;
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class SoftwareAndLog {
            public Link Link;

            public byte Version;
            public byte LogIndex;
            public byte LevelIndex;
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class Decoration {
            public Link Link;

            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 10)]
            public byte[] Data;

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
                public ushort PingPong;
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
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class Item {
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
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class Interface {
            public Link Link;

            public ActionType Action;
            public byte Unknown;
            public ushort ConditionVariableIndex;
            public ushort ConditionMessageIndex;

            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 18)]
            public byte[] Data;

            [Serializable]
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public class NumberPad {
                public ushort Combination;
                public ushort FirstObjectToTrigger;
                public ushort SecondObjectToTrigger;

                [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 12)]
                public byte[] Data;
            }

            [Serializable]
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public class Block {
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
            public class Wire {
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
                public ushort Unknown1;
                public ushort TargetPanel4;
                public uint Unknown2;
                public ushort LevelsVisible;
                public ushort LevelsAccessible;
                public ushort Unknown3;
            }
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 14)]
        public class DoorAndGrating {
            public Link Link;

            public ushort TriggerIndex;
            public byte Message;
            public byte ForceColor;
            public byte AccessRequired;
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class Animated {
            public Link Link;

            public uint Data;
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class Trigger {
            public Link Link;

            public ActionType Action;
            public byte Unknown;
            public uint Condition;

            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 16)]
            public byte[] Data;

            [Serializable]
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public class MovingPlatform {
                public uint TileX;
                public uint TileY;
                public ushort TargetFloorHeight;
                public ushort TargetCeilingHeight;
                public ushort Speed;
                public ushort Unknown;
            }
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class Container {
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
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class Enemy {
            public Link Link;

            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 40)]
            public byte[] Data;
        }
    }
}
