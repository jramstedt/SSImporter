using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using SystemShock.Object;

namespace SystemShock.Resource {
    public class ObjectPropertyLibrary : AbstractResourceLibrary<ObjectPropertyLibrary>, ISerializationCallbackReceiver {
        [SerializeField]
        public List<ObjectData> ObjectDatas;

        [SerializeField]
        [HideInInspector]
        private List<uint> indexMap;

        [SerializeField]
        [HideInInspector]
        private List<uint> spriteOffsets;

        public ObjectPropertyLibrary() {
            ObjectDatas = new List<ObjectData>();
            indexMap = new List<uint>();
            spriteOffsets = new List<uint>();
        }

        public void AddObject(uint combinedId, ObjectData objectData) {
            if (indexMap.Contains(combinedId))
                throw new ArgumentException(string.Format(@"Object data {0} already set.", combinedId));

            indexMap.Add(combinedId);
            ObjectDatas.Add(objectData);
        }

        public T GetObject<T>(uint combinedId) where T : ObjectData {
            return (T)ObjectDatas[indexMap.IndexOf(combinedId)];
        }

        public T GetObject<T>(ObjectClass Class, byte Subclass, byte Type) where T : ObjectData {
            return GetObject<T>((uint)Class << 24 | (uint)Subclass << 16 | Type);
        }

        public int GetIndex(uint combinedId) {
            return indexMap.IndexOf(combinedId);
        }

        public int GetIndex(ObjectClass Class, byte Subclass, byte Type) {
            return GetIndex((uint)Class << 24 | (uint)Subclass << 16 | Type);
        }

        public uint GetSpriteOffset(uint combinedId) {
            return spriteOffsets[GetIndex(combinedId)];
        }

        public uint GetSpriteOffset(ObjectClass Class, byte Subclass, byte Type) {
            return spriteOffsets[GetIndex(Class, Subclass, Type)];
        }

        public void OnAfterDeserialize() { }

        public void OnBeforeSerialize() {
            spriteOffsets.Clear();

            uint spriteOffset = 1;
            spriteOffsets.Add(spriteOffset);

            for (int i = 1, lastOffset = 0; i < ObjectDatas.Count; ++i, ++lastOffset) {
                spriteOffset += 3; // Three sprites: Inventory, World, Editor.
                spriteOffset += (uint)(ObjectDatas[lastOffset].Base.ArtInfo & (ushort)BaseProperties.ArtMask.AdditionalSprites) >> 4;
                spriteOffsets.Add(spriteOffset);
            }
        }
    }

    public class ObjectData : ScriptableObject {
        public string FullName;
        public string ShortName;

        public BaseProperties Base;

        public virtual void SetGeneric(object generic) { }
        public virtual void SetSpecific(object specific) { }
    }

    public enum DrawType : byte {
        Model = 0x01,
        Sprite = 0x02,
        Screen = 0x03,
        Enemy = 0x04,
        T5 = 0x05,
        Fragments = 0x06,
        NoDraw = 0x07,
        Decal = 0x08,
        T9 = 0x09,
        T10 = 0x0A,
        Special = 0x0B,
        ForceDoor = 0x0C
    }

    [Flags]
    public enum DamageType : byte {
        None = 0x00,
        Impact = 0x01,
        Energy = 0x02,
        EMP = 0x04,
        Ion = 0x08,
        Gas = 0x10,
        Tranquil = 0x20,
        Needle = 0x40
    }

    [Flags]
    public enum Flags : ushort {
        UsefulItem = 0x0001,
        Touchable = 0x0002,
        Raycastable = 0x0004,
        NoPickup = 0x0008,
        Consumable = 0x0010,
        OpaqueClosed = 0x0020,
        F6 = 0x0040,    // Mutant, robot, cyborg.
        F7 = 0x0080,    // Explosion, mutant, robot, cyborg.
        Activable = 0x0100, 
        Collider = 0x0200,
        Explosion = 0x0400,
        ExplodeImpact = 0x0800,
        F12 = 0x1000, // Target identifier, env suit, lamp, surgery machine, x-ray, control panel
        F13 = 0x2000, // Gorilla, avian, plant, zero-g
        F14 = 0x4000, // None
        F15 = 0x8000 // Berserk, decoration, garbage
    }

    [Flags]
    public enum SizeFlags : byte {
        UseBitmapWidth = 0x01,
        UseBitmapHeight = 0x02,
    }

    public enum Orientation : byte {
        West = 0,
        SouthWest = 1,
        South = 2,
        SouthEast = 3,
        East = 4,
        NorthEast = 5,
        North = 6,
        NorthWest = 7
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct BaseProperties {
        [Flags]
        public enum FramesMask : byte {
            Frames = 0xF0
        }

        [Flags]
        public enum ArtMask : ushort {
            AdditionalSprites = 0x00F0
        }

        public uint Mass;
        public ushort Hitpoints;
        public byte Armour;
        public DrawType DrawType;
        public byte Rigidbody;

        public byte Unknown1;
        public ushort SizeX;
        public ushort SizeY;

        [EnumMask] public DamageType Vulnerabilities;
        public byte SpecialVulnerabilities; // TODO "Special Effects"
        public ushort Unknown2;
        public byte Defence;
        public byte SizeFlags;
        public ushort Flags;
        public ushort ModelIndex;
        public byte ExtraFrames;
        public ushort ArtInfo;

        public Vector2 Size {
            get {
                Vector2 size = new Vector2();
                size.x = SizeX == 0 ? 1f : SizeX / (256f * 47f);
                size.y = SizeY == 0 ? size.x : SizeY / (256f * 47f);
                return size;
            }
        }

        public Vector3 GetRenderSize(Vector2 graphicSize) {
            Vector3 worldSize = Size;

            if (((SizeFlags)SizeFlags & SystemShock.Resource.SizeFlags.UseBitmapWidth) == SystemShock.Resource.SizeFlags.UseBitmapWidth)
                worldSize.x = graphicSize.x / 64f;

            if (((SizeFlags)SizeFlags & SystemShock.Resource.SizeFlags.UseBitmapHeight) == SystemShock.Resource.SizeFlags.UseBitmapHeight)
                worldSize.y = graphicSize.y / 64f;

            worldSize.z = Mathf.Min(worldSize.x, worldSize.y);

            return worldSize;
        }

        public override string ToString() {
            return Mass + " " + Hitpoints + " " + Armour + " " + DrawType;
        }
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct DamageInfo {
        public ushort Damage;
        public byte Offence;
        [EnumMask] public DamageType DamageType;
        public byte SpecialDamage; // TODO "Special Effects"
        private ushort Unknown;
        public byte ArmorPenetration;
    }

    #region Weapons
    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Weapon {
        [Flags]
        public enum ClipMask : byte {
            Type = 0x0F,
            Subclass = 0xF0
        }

        public byte FiringRate;
        public ClipMask ClipInfo;

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct SemiAutomatic {
            private byte Zero;
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct FullAutomatic {
            private byte Zero;
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Projectile {
            public DamageInfo DamageInfo;
            private byte Padding;
            public byte ProjectileType;
            public byte ProjectileSubclass;
            public ObjectClass ProjectileClass;
            private uint Unknown;
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Melee {
            public DamageInfo DamageInfo;
            public byte Energy;
            public byte Kickback;
            public byte Range;
            private ushort Unknown;
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Beam {
            public DamageInfo DamageInfo;
            public byte Energy;
            public byte Kickback;
            public byte Range;
            private ushort Unknown;
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct EnergyProjectile {
            public DamageInfo DamageInfo;
            public byte Energy;

            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 5)]
            private byte[] Padding;

            public byte ProjectileType;
            public byte ProjectileSubclass;
            public ObjectClass ProjectileClass;

            private byte Unknown;
        }
    }
    #endregion

    #region Ammunition
    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Ammunition {
        public DamageInfo DamageInfo;
        public byte Rounds;
        public byte Kickback;
        private ushort Unknown1;
        public byte Range;
        private byte Unknown2;

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct All {
            private byte Zero;
        }
    }
    #endregion

    #region Projectiles
    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Projectile {
        [Flags]
        public enum FeatureFlag : byte {
            Light = 0x01,
            BounceWorld = 0x02,
            BounceEnemy = 0x04,
            Cyberspace = 0x08
        }

        [EnumMask] public FeatureFlag Features;

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Tracer {
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 20)]
            private byte[] Zero;
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Normal {
            public ushort R;
            public ushort G;
            public ushort B;
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Seeker {
            private byte Zero;
        }
    }

    #endregion

    #region Explosives
    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Explosive {
        public DamageInfo DamageInfo;

        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 7)]
        private byte[] Unknown;

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Grenade {
            private byte Zero;
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Bomb {
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 3)]
            private byte[] Unknown;
        }
    }
    #endregion

    #region Dermal Patches
    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct DermalPatch {
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 23)]
        private byte[] Unknown;

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct All {
            private byte Zero;
        }
    }
    #endregion

    #region Hardware
    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Hardware {
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 10)]
        private byte[] Unknown;

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct All {
            private byte Zero;
        }
    }
    #endregion

    #region Software
    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SoftwareAndLog {
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 4)]
        private byte[] Unknown;

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct All {
            private byte Zero;
        }
    }
    #endregion

    #region Decoration
    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Decoration {
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 2)]
        public byte[] Unknown;

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct All {
            private byte Zero;
        }
    }
    #endregion

    #region Items
    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Item {
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 2)]
        private byte[] Unknown;

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Common {
            private byte Zero;
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Cyberspace {
            public ushort R;
            public ushort G;
            public ushort B;
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Quest {
            private ushort Unknown;
        }
    }
    #endregion

    #region Interfaces
    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Interface {
        private byte Zero;

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Common {
            private byte Zero;
        }

        [Serializable]
        [StructLayout(LayoutKind.Explicit, Size = 0)]
        public struct Vending {
        }
    }
    #endregion

    #region Doors
    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct DoorAndGrating {
        private byte Zero;

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct All {
            private byte Zero;
        }
    }
    #endregion

    #region Animated
    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Animated {
        public ushort Unknown;

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct All {
            public byte Unknown;
        }
    }

    #endregion

    #region Triggers
    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Trigger {
        private byte Zero;

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct All {
            private byte Zero;
        }
    }

    #endregion

    #region Containers
    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Container {
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 3)]
        private byte[] Unknown;

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct All {
            private byte Zero;
        }
    }

    #endregion

    #region Enemies
    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Enemy {
        [Flags]
        public enum TraitFlags : ushort {
            IgnoreGravity = 0x0001,
            Robot = 0x0004, // set for repair-, serv-, exec-, maint- bots
            Unknown1 = 0x0200, // set for Mutantborg
            Unknown2 = 0x1000, // set for Mutantborg
            Unknown3 = 0x2000, // set for Elite Guard
        }

        public enum LootTemplate : byte {
            Nothing,
            Mutants,
            CyborgDrone,
            CyborgAssasin,
            CyborgWarrior,
            Flier,
            Sec1Bot,
            ExecBot,
            Enforcer,
            Sec2Bot,
            EliteMutantborg,
            Misc,
            MLStandard,
            RepairMaintenanceBot,
            ServiceBot
        }

        public enum InjuryEffectType : byte {
            Flesh,
            Plant,
            Metal
        }

        // TODO Weapons have minimum usage range? (Cortex reaver secondary grenade launcher)

        public WeaponInfo Primary;

        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 8)]
        private byte[] Unknown3;

        public WeaponInfo Secondary;
        public ushort FieldOfView;              // Are these
        private ushort Unknown4;                // Weapon 
        public uint SecondaryProjectileClass;  // info? (8 bytes...)

        public ushort Perception;
        [EnumMask] public TraitFlags Trait;

        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 12)]
        private byte[] Unknown5;

        public byte DeathAnimationFramerate;
        public byte AttackSound;
        public byte TargetSound;
        public byte PainSound;
        public byte DeathSound;
        public byte UnknownSound; // Used by null-G mutants
        public uint CorpseClass;
        private byte Unknown6;
        public byte PropablyOfSecondaryAttack; // /256
        public byte Disruptability;
        public LootTemplate Loot;
        public InjuryEffectType InjuryEffect;
        private ushort Unknown7;

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct WeaponInfo {
            // FiringRate? Offence? SpecialDamage?

            public uint DamageType;
            public ushort Damage;
            public byte ArmorPenetration;
            private byte Unknown1;
            public byte Kickback;

            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 3)]
            private byte[] Unknown2;

            public byte Range;
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Mutant {
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 3)]
            private byte[] Unknown;
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Bot {
            private byte Unknown;
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Cyborg {
            private byte Zero;
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Cyberspace {
            public ushort R;
            public ushort G;
            public ushort B;
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Boss {
            private byte Unknown;
        }
    }
    #endregion
}