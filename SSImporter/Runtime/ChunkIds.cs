﻿using UnityEngine;
using System.Collections;

namespace SystemShock.Resource {
    public enum KnownChunkId : ushort {
        ObjectNames = 0x0024,

        Textures = 0x004B,
        Textures16x16 = 0x004C,
        Textures32x32 = 0x004D,
        AnimationsStart = 0x0141,
        Textures64x64Start = 0x02C3,
        Textures128x128Start = 0x03E8,
        Palette = 0x02BC,
        ObjectSprites = 0x0546,

        EnemyAnimationStart = 0x0578,
        EnemyAttackStart = EnemyAnimationStart,
        EnemyEvadeStart = 0x059D,
        EnemyDeathStart = 0x05C2,
        EnemySevereDamageStart = 0x05E7,
        EnemyLightDamageStart = 0x060C,
        EnemyIdleStart = 0x0631,

        EnemyWalkStart = 0x0758,
        EnemyAttackSecondaryStart = 0x0841,

        DecalWords = 0x0868,
        TextureNames = 0x086A,
        CantUseMessages = 0x086B,
        ShortObjectNames = 0x086D,
        StateMessages = 0x0871,
        LogoNames = 0x0876, // Engineering, Robotics ...
        ScreenTexts = 0x0877,

        ModelsStart = 0x08FC,

        LevelRStart = 0x0FA0,
        Level1Start = 0x1004,
        Level2Start = 0x1068,
        Level3Start = 0x10CC,
        Level4Start = 0x1130,
        Level5Start = 0x1194,
        Level6Start = 0x11F8,
        Level7Start = 0x125C,
        Level8Start = 0x12C0,
        Level9Start = 0x1324,
        LevelShodanCyberspaceStart = 0x1388,
        LevelDeltaGroveStart = 0x13EC,
        LevelAlphaGroveStart = 0x1450,
        LevelBetaGroveStart = 0x14B4,
        LevelCyberspace12Start = 0x1518,
        LevelCyberspace39Start = 0x157C
    }
}
