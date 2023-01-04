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

    // Static Game Data
    public fixed byte name[20];
    public byte realspace_level;  // this is the last realspace level we were in

    // Difficulty related stuff
    public fixed sbyte difficulty[4];
    private fixed byte Unused[(NUM_LEVELS >> 3) + 1];

    // system stuff
    public uint gameTime;
    public uint lastSecondUpdate; // when was last do_stuff_every_second
    public uint lastDrugUpdate;
    public uint lastWareUpdate;
    public uint lastAnimCheck;
    public int queueTime;
    public int deltaTime;
    public sbyte detailLevel;

    // World stuff
    public byte currentLevel;
    public fixed short initialShodanSecurityLevels[NUM_LEVELS];
    // public Span<short> InitialShodanSecurityLevels { get { fixed (short* a = initialShodanSecurityLevels) { return new Span<short>(a, UnsafeUtility.SizeOf<short>() * NUM_LEVELS); } } }

    public fixed sbyte controls[DEGREES_OF_FREEDOM];
    public short playerObjectIndex;
    public Location realspaceLocation;            // This is where the player will come back out of cspace into
    public int versionNumber;
    public fixed short inventory[NUM_GENERAL_SLOTS];   // general inventory

    // Random physics state.
    public byte posture;                   // current posture (standing/stooped/prone)
    [MarshalAs(UnmanagedType.U1)] public bool footPlanted;              // Player's foot is planted
    public sbyte leanX;                  // leaning, -100-+100
    public sbyte leanY;               

    private ushort eye;                      // eye position 

    // Gamesys stuff
    public byte hitPoints;                // I bet we will want these.
    public byte cyperspaceHitPoints;                 // after hit_points so we can array ref this stuff
    public ushort hitPointsRegenRate;         // Rate at which hit points regenerate, per minute
    public fixed byte hitPointsLost[NUM_DAMAGE_TYPES];  // Rate at which damage is taken, per minute
    public ushort bioPostExpose;          // expose damage from bio squares long past.
    public ushort radPostExpose;          // expose damage from rad squares long past.
    public byte energy;                    // suit power charge
    public byte energySpend;              // rate of energy burn
    public byte energyRegen;              // Rate at which suit recharges
    [MarshalAs(UnmanagedType.U1)] public bool energyOut;                // out of energy last check
    public short cyberspaceTrips;
    public int cyberspaceTimeBase;

    private fixed byte questBits[NUM_QUESTBITS >> 3];       // Mask of which "quests" you have completed
    private fixed short questVars[NUM_QUESTVARS];

    public uint hudModes;                 // What hud functions are currently active?
    [MarshalAs(UnmanagedType.U1)] public bool experience;                // Are you experienced?
    public int fatigue;                   // how fatigued are you
    public ushort fatigueSpend;          // Current rate of fatigue expenditure in pts/sec
    public ushort fatigueRegen;          // Current rate of fatigue regeneration
    public ushort fatigueRegenBase;     // base fatigue regen rate
    public ushort fatigueRegenMax;      // max fatigue regen rate 
    public sbyte accuracy;
    public byte shieldAbsorbRate;       // % of damage shields absorb
    public byte shieldThreshold;        // Level where shields turn off
    public byte lightValue;               // current lamp setting

    // MFD State
    public fixed byte mfdVirtualSlots[NUM_MFDS * MFD_NUM_VIRTUAL_SLOTS]; // ptrs to mfd_slot id's
    public fixed /*MFDStatus*/ byte mfdSlotStatus[MFD_NUM_REAL_SLOTS];
    public fixed byte mfdAllSlots[MFD_NUM_REAL_SLOTS];          // ptrs to mfd_func id's
    public fixed byte mfdFuncStatus[MFD_NUM_FUNCS];             // ptrs to mfd_func flags
    public fixed byte mfdFuncData[MFD_NUM_FUNCS * 8];
    public fixed byte mfdCurrentSlots[NUM_MFDS];                // ptrs to mfd's curr slots
    public fixed byte mfdEmptyFuncs[NUM_MFDS];                  // ptrs to mfd's empty func
    public fixed byte mfdAccessPuzzles[64];             // this is 4 times as much as that hardcoded 8 up there
                                              // who knows how much we really need, hopefully in soon
                                              // KLC - changed to 64
    public fixed sbyte mfdSaveSlot[NUM_MFDS];

    // Inventory stuff, in general, a value of zero will indicate an empty slot
    // indices are drug/grenade/ware "types" 
    public fixed byte hardware[Hardware.NUM_HARDWARE];  // Which warez do we have? (level of each type?)

    public fixed byte softwareCombat[Software.NUM_COMBAT_SOFTS];
    public fixed byte softwareDefense[Software.NUM_DEFENSE_SOFTS];
    public fixed byte softwareMisc[Software.NUM_MISC_SOFTS];

    public fixed byte cartridges[Ammunition.NUM_AMMO]; // Cartridges for each ammo type.
    public fixed byte partialClip[Ammunition.NUM_AMMO];

    public fixed byte drugs[DermalPatch.NUM_DRUG];          // Quantity of each drug
    public fixed byte grenades[NUM_GRENADES];    // Quantity of each grenade.

    public fixed byte email[NUM_EMAIL];  // Which email messages do you have.
    public fixed byte logs[NUM_LOG_LEVELS]; // on which levels do we have logs. 

    // Weapons are arranged into "slots" 
    public fixed /*WeaponSlot*/ byte weapons[5 * NUM_WEAPON_SLOTS]; // Which weapons do you have?

    // Inventory status
    public fixed byte hardwareStatus[Hardware.NUM_HARDWARE];    // Status of active wares (on/off, activation time, recharge time?)

    public fixed byte softwareCombatStatus[Software.NUM_COMBAT_SOFTS];
    public fixed byte softwareDefenseStatus[Software.NUM_DEFENSE_SOFTS];
    public fixed byte softwareMiscStatus[Software.NUM_MISC_SOFTS];

    public byte jumpjetEnergyFraction;  // fractional units of energy spent on jumpjets.
    public fixed byte emailSenderCounts[32];  // who has sent how many emails 
    public fixed sbyte drugStatus[DermalPatch.NUM_DRUG];     // Time left on active drugs, 0 if inactive
    public fixed byte drugIntensity[DermalPatch.NUM_DRUG];  // Intensity of active drugs, 0 if inactive
    public fixed ushort grenadesTimeSetting[NUM_GRENADES];      // Time setting for each grenade

    // PLOT STUFF
    public ushort time2dest;  // Time to destination (seconds)
    public ushort time2comp;  // time to completion of current program (seconds)

    // Combat shtuff <tm>
    public short currentTargetObjectId;                   // creature currently "targeted"
    public uint lastFire;                 // last gametime the weapon fired.
    public ushort fireRate;                 // game time required between weapon fires.

    // Selectied items
    public fixed byte actives[NUM_ACTIVES];

    // Other transitory state
    public short saveObjCursorObjectId;            // saving object cursor when you change to cyberspace
    public short panelRefObjectId;                 // Last panel utilized.  stuffed here for reference

    // Stats...
    public int numVictories;
    public int timeInCyberspace;
    public int roundsFired;
    public int numHits;

    // Playtesting data
    public int numDeaths;

    // from this point on - data is taking the time_to_level space
    public long eyePosition;       // physics eye position

    // let's hope State stays at 12 fixes
    public fixed int edmsState[12];

    // the player's actively selected inventory category.  
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
        (0x3, 2), // engine state
        (0xC, 3), // num groves
        (0x33, 256), // joystick sensitivity
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
