using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using SS.ObjectProperties;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine;

namespace SS {
  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public unsafe struct Hacker : IComponentData {
    public const int NUM_LEVELS = 22;
    public const int NUM_DAMAGE_TYPES = 8;
    public const int DEGREES_OF_FREEDOM = 6;
    public const int NUM_GENERAL_SLOTS = 14;
    public const int NUM_QUESTBITS = 512;
    public const int NUM_QUESTVARS = 64;
    public const byte PLAYER_MAX_HP = 255;

    public const int NUM_MFDS = 2;
    public const int MFD_NUM_VIRTUAL_SLOTS = 5;
    public const int MFD_NUM_REAL_SLOTS = NUM_MFDS + MFD_NUM_VIRTUAL_SLOTS;
    public const int MFD_NUM_FUNCS = 32;

    public const int NUM_EMAIL_PROPER = 47;
    public const int NUM_LOG_LEVELS = 14;
    public const int LOGS_PER_LEVEL = 16;
    public const int NUM_DATA = 23;

    public const int NUM_EMAIL = NUM_EMAIL_PROPER + NUM_DATA + NUM_LOG_LEVELS * LOGS_PER_LEVEL;

    public const int NUM_WEAPON_SLOTS = 7;
    public const int EMPTY_WEAPON_SLOT = 0xFF;

    public const int NUM_GRENADES = 7;

    public const int NUM_ACTIVES = 10;

    public const int MAX_ENERGY = 255;
    public const int MAX_ACCURACY = 100;


    public const int MISSION_DIFF_QUEST_VAR = 0xD;
    public const int CYBER_DIFF_QUEST_VAR = 0xE;
    public const int COMBAT_DIFF_QUEST_VAR = 0xF;
    public const int PUZZLE_DIFF_QUEST_VAR = 0x1E;

    public enum MFDStatus : byte {
      Empty,
      Flash,
      Active,
      Unavailable
    }

    public fixed byte name[20];
    public byte realspaceLevel;

    public fixed sbyte difficulty[4];
    private fixed byte Unused[(NUM_LEVELS >> 3) + 1];

    public uint gameTime;
    public uint lastSecondUpdate;
    public uint lastDrugUpdate;
    public uint lastWareUpdate;
    public uint lastAnimCheck;
    public int queueTime;
    public int deltaTime;
    public sbyte detailLevel;

    public byte currentLevel;
    public fixed short initialShodanSecurityLevels[NUM_LEVELS];
    // public Span<short> InitialShodanSecurityLevels { get { fixed (short* a = initialShodanSecurityLevels) { return new Span<short>(a, UnsafeUtility.SizeOf<short>() * NUM_LEVELS); } } }

    public fixed sbyte controls[DEGREES_OF_FREEDOM];
    public short playerObjectId;
    public Location realspaceLocation;
    public int versionNumber;
    public fixed short inventory[NUM_GENERAL_SLOTS];

    public byte posture;
    [MarshalAs(UnmanagedType.U1)] public bool footPlanted;
    public sbyte leanX;                  // -100-+100
    public sbyte leanY;               

    private ushort eye;

    public byte hitPoints;
    public byte cyperspaceHitPoints;
    public ushort hitPointsRegenRate;
    public fixed byte hitPointsLost[NUM_DAMAGE_TYPES];
    public ushort bioPostExpose;
    public ushort radPostExpose;
    public byte energy;
    public byte energySpend;
    public byte energyRegen;
    [MarshalAs(UnmanagedType.U1)] public bool energyOut;
    public short cyberspaceTrips;
    public int cyberspaceTimeBase;

    private fixed byte questBits[NUM_QUESTBITS >> 3];
    private fixed short questVars[NUM_QUESTVARS];

    public uint hudModes;
    [MarshalAs(UnmanagedType.U1)] private bool experience;
    public int fatigue;
    public ushort fatigueSpend;
    public ushort fatigueRegen;
    public ushort fatigueRegenBase;
    public ushort fatigueRegenMax;
    public sbyte accuracy;
    public byte shieldAbsorbRate;
    public byte shieldThreshold;
    public byte lightValue;

    public fixed byte mfdVirtualSlots[NUM_MFDS * MFD_NUM_VIRTUAL_SLOTS];
    public fixed /*MFDStatus*/ byte mfdSlotStatus[MFD_NUM_REAL_SLOTS];
    public fixed byte mfdAllSlots[MFD_NUM_REAL_SLOTS];
    public fixed byte mfdFuncStatus[MFD_NUM_FUNCS];
    public fixed byte mfdFuncData[MFD_NUM_FUNCS * 8];
    public fixed byte mfdCurrentSlots[NUM_MFDS];
    public fixed byte mfdEmptyFuncs[NUM_MFDS];
    public fixed byte mfdAccessPuzzles[64];
    public fixed sbyte mfdSaveSlot[NUM_MFDS];

    public fixed byte hardware[Hardware.NUM_HARDWARE];

    public fixed byte softwareCombat[Software.NUM_COMBAT_SOFTS];
    public fixed byte softwareDefense[Software.NUM_DEFENSE_SOFTS];
    public fixed byte softwareMisc[Software.NUM_MISC_SOFTS];

    public fixed byte cartridges[Ammunition.NUM_AMMUNITION];
    public fixed byte partialClip[Ammunition.NUM_AMMUNITION];

    public fixed byte drugs[DermalPatch.NUM_DRUGS];
    public fixed byte grenades[NUM_GRENADES];

    public fixed byte email[NUM_EMAIL];
    public fixed byte logs[NUM_LOG_LEVELS];

    public fixed /*WeaponSlot*/ byte weapons[5 * NUM_WEAPON_SLOTS];

    public fixed byte hardwareStatus[Hardware.NUM_HARDWARE];

    public fixed byte softwareCombatStatus[Software.NUM_COMBAT_SOFTS];
    public fixed byte softwareDefenseStatus[Software.NUM_DEFENSE_SOFTS];
    public fixed byte softwareMiscStatus[Software.NUM_MISC_SOFTS];

    public byte jumpjetEnergyFraction;
    public fixed byte emailSenderCounts[32];
    public fixed sbyte drugStatus[DermalPatch.NUM_DRUGS];
    public fixed byte drugIntensity[DermalPatch.NUM_DRUGS];
    public fixed ushort grenadesTimeSetting[NUM_GRENADES];

    public ushort time2dest;
    public ushort time2comp;

    public short currentTargetObjectId;
    public uint lastFire;
    public ushort fireRate;

    public fixed byte actives[NUM_ACTIVES];

    public short saveObjCursorObjectId;
    public short panelRefObjectId;

    public int numVictories;
    public int timeInCyberspace;
    public int roundsFired;
    public int numHits;
    public int numDeaths;

    public long eyePosition;

    public fixed int edmsState[12];

    public byte currentActiveCategory;
    public byte activeBioTracks;

    public short currentEmail;

    public fixed byte version[6];
    [MarshalAs(UnmanagedType.U1)] public bool dead;

    public ushort leanFilterState;
    private ushort FREE_BITS_HERE;
    public byte mfdSaveVis;

    public uint autoFireClick;

    public uint postureSlamState;

    [MarshalAs(UnmanagedType.U1)] public bool terseness;

    public uint lastHeadBob;

    private fixed byte pad[9];

    /// <summary>
    /// Initializes new player
    /// init_player
    /// </summary>
    public void Initialize () { // TODO name, difficulty
      UnsafeUtility.MemClear(UnsafeUtility.AddressOf(ref this), UnsafeUtility.SizeOf<Hacker>());

      ReadOnlySpan<short> turnOnQuestBits = stackalloc short[] {
        0x1,0x2,0x3,0x10,0x12,0x15,0x16,0x17,0x18,0x19,0x1a,
        0x20,0x21,0x24,0x25,
        0x4b,0x4c,0x4d,0x4e,0x4f,
        0x50,0x51,0x52,0x53,0x54,0x55,0x56,0x57,0x58,0x59,0x5a,0x5b,0x5c,0x5d,0x5e,0x5f,
        0x70,0x71,0x72,0x73,0x74,0x75,0x76,0x77,0x78,0x79,0x7a,0x7b,0x7c,0x7d,0x7e,0x7f,
        0xa0,0xa1,0xa2,0xa3,0xa4,0xa5,0xa6,0xa7,0xa8,0xa9,
        0xc0,0xc1,0xc2,0xc3,0xc4,0xc5,0xc6,0xc7,0xc8,0xc9,0xca,0xcb,0xcc,0xcd,0xce,0xcf,
        0xe1,0xe3,0xe5,0xe7,0xe9,0xeb,0xed,0xef,
        0xf1,0xf3,0xf5,0xf7,0xf9,0xfb,0xfd,0xff,
        0x101,0x103,0x105,0x107,0x109,0x10b,0x10d,0x10f,
        0x111,0x113,0x115,0x117,0x119,0x11b,0x11d,0x11f,
        0x121,0x123,0x125,0x127,0x129,0x12b,
      };

      ReadOnlySpan<(ushort index, short value)> initQuestVars = stackalloc (ushort, short)[] {
        (0x3, 2), // initial engine state
        (0xC, 3), // number of groves
        (0x33, 256),
      };

      currentLevel = 1;
      for (var i = 0; i < NUM_LEVELS; ++i)
        initialShodanSecurityLevels[i] = -1;

      hitPoints = PLAYER_MAX_HP * 5/6;
      cyperspaceHitPoints = PLAYER_MAX_HP;
      accuracy = MAX_ACCURACY;
      energy = MAX_ENERGY;

      for (var i = 0; i < turnOnQuestBits.Length; ++i)
        SetQuestBit(turnOnQuestBits[i], true);

      for (var i = 0; i < initQuestVars.Length; ++i)
        SetQuestVar(initQuestVars[i].index, initQuestVars[i].value);

      // TODO reactor code

      fatigueRegenBase = 100;
      fatigueRegenMax = 400;

      for (var i = 0; i < NUM_MFDS; ++i) {
        mfdSaveSlot[i] = -1;
        mfdEmptyFuncs[i] = (byte)MFDStatus.Empty;

        for (byte j = 0; j < MFD_NUM_VIRTUAL_SLOTS; ++j)
          mfdVirtualSlots[i * NUM_MFDS + j] = j;
      }

      for (byte i = 0; i < MFD_NUM_REAL_SLOTS; ++i)
        mfdAllSlots[i] = i;

      fixed (byte* wptr = weapons) {
        var weaponSlots = (WeaponSlot*)wptr;
        for (byte i = 0; i < NUM_WEAPON_SLOTS; ++i)
          weaponSlots[i].type = EMPTY_WEAPON_SLOT;
      }

      for (var i = 0; i < NUM_GRENADES; ++i)
        grenadesTimeSetting[i] = 70;

      var fullscreenTriple = (Triple)0x50105;
      hardware[Hardware.NUM_GOGGLE_HARDWARE + fullscreenTriple.Type] = 1; // TODO index in class count array

      email[26] = (byte)EmailFlags.Got;

      activeBioTracks = 0xFF;
      actives[(int)Active.Email] = 0xFF;
    }

    public bool GetQuestBit(int index) => (questBits[index >> 3] & (1 << (index & 0xF))) != 0;
    public void SetQuestBit(int index, bool value) {
      if (value) questBits[index >> 3] |= (byte)(1 << (index & 0xF));
      else questBits[index >> 3] &= (byte)~(1 << (index & 0xF));
    }

    public short GetQuestVar(int index) => questVars[index];
    public void SetQuestVar(int index, short value) => questVars[index] = value;

    [Flags]
    public enum EmailFlags : byte {
      Sequence = 0x3F,
      Read = 0x40,
      Got = 0x80
    }

    public enum Active : byte {
      Weapon,
      Grenade,
      Drug,
      Cart,
      Hardware,
      CombatSoftware,
      DefenseSoftware,
      MiscSoftware,
      General,
      Email,
      NumActives
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct WeaponSlot {
      public byte type;
      public byte subtype;
      public byte ammo;
      public byte ammo_type;
      public byte manufacturer;

      public byte Heat => ammo;
      public byte Setting => ammo_type;
    }
  }
}
