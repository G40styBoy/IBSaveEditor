using System.Reflection;

public enum Game
{
    IB1,
    IB2,
    IB3,
    VOTE
}

/// <summary>
/// Class designed to aid in dealing with Infinity Blade Enums
/// </summary>
public static class IBEnum
{
    private const string CONSUMABLE = "NumConsumable";
    private const string CHEEVO = "SavedCheevo";
    private const string VOTE_COUNT = "LastVoteCount";
    public static Game game;

    public static string GetEnumEntryFromIndex(string alias, int idx)
    {
        return (game, alias) switch
        {
            (Game.IB2 or Game.IB1, CONSUMABLE) => EnumToString<eTouchRewardActor_IB2>(idx),
            (Game.IB2 or Game.IB1, CHEEVO) => EnumToString<eAchievements_IB2>(idx),
            (Game.IB3, CONSUMABLE) => EnumToString<eTouchRewardActor_IB3>(idx),
            (Game.IB3, CHEEVO) => EnumToString<eAchievements_IB3>(idx),
            (Game.VOTE, VOTE_COUNT) => EnumToString<CharacterFilterEnum>(idx),
            _ => $"Element{idx + 1}"
        };
    }

    private static string EnumToString<T>(int idx) where T : Enum => ((T)(object)idx).ToString();

    /// <summary>
    /// Associates a string t from the generic enum passed.
    /// Thisz
    /// </summary>
    /// <returns>Index position of the enum value, or -1 if not found</returns>
    public static int GetArrayIndexFromEnum<T>(string fName) where T : Enum
    {
        if (Enum.IsDefined(typeof(T), fName))
        {
            var enumNames = Enum.GetNames(typeof(T));
            return Array.IndexOf(enumNames, fName);
        }

        throw new InvalidDataException($"{fName} not found inside of {typeof(T)}");
    }

    public static int GetArrayIndexUsingReflection(Type enumType, string value)
    {
        MethodInfo method = typeof(IBEnum).GetMethod(nameof(GetArrayIndexFromEnum))!;
        MethodInfo genericMethod = method.MakeGenericMethod(enumType);
        try
        {
            return (int)genericMethod.Invoke(null, new object[] { value });
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(exception.Message);
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    /// <exception cref="InvalidDataException"></exception>
    public static Type GetArrayIndexEnum(string alias)
    {
        return (game, alias) switch
        {
            (Game.IB2 or Game.IB1, CONSUMABLE) => typeof(eTouchRewardActor_IB2),
            (Game.IB2 or Game.IB1, CHEEVO) => typeof(eAchievements_IB2),
            (Game.IB3, CONSUMABLE) => typeof(eTouchRewardActor_IB3),
            (Game.IB3, CHEEVO) => typeof(eAchievements_IB3),
            (Game.VOTE, VOTE_COUNT) => typeof(CharacterFilterEnum),
            _ => throw new InvalidDataException("")
        };
    }

    #region IB3 Enums
    public enum eTouchRewardActor_IB3
    {
        TRA_Random,                     // 0
        TRA_Random_Potion,              // 1
        TRA_Random_Gold,                // 2
        TRA_Random_Key,                 // 3
        TRA_Random_Gem,                 // 4
        TRA_Random_Item,                // 5
        TRA_Random_World,               // 6
        TRA_None,                       // 7
        TRA_Gold_Small,                 // 8
        TRA_Gold_Medium,                // 9
        TRA_Gold_Large,                 // 10
        TRA_Key_Small,                  // 11
        TRA_Key_Medium,                 // 12
        TRA_Key_Large,                  // 13
        TRA_Key_Item,                   // 14
        TRA_Gem_Fixed,                  // 15
        TRA_Item_Fixed,                 // 16
        TRA_Item_Weapon,                // 17
        TRA_Item_Shield,                // 18
        TRA_Item_Armor,                 // 19
        TRA_Item_Helmet,                // 20
        TRA_Item_Magic,                 // 21
        TRA_GrabBag_Small,              // 22
        TRA_GrabBag_Medium,             // 23
        TRA_GrabBag_Large,              // 24
        TRA_GrabBag_SmallGem,           // 25
        TRA_GrabBag_MediumGem,          // 26
        TRA_GrabBag_LargeGem,           // 27
        TRA_GrabBag_Uber,               // 28
        TRA_Potion_HealthL,             // 29
        TRA_Potion_Fixed,               // 30
        TRA_World_1_Cactus,             // 31
        TRA_World_2_Berries,            // 32
        TRA_World_3_PinkPlant,          // 33
        TRA_World_4_Reeds,              // 34
        TRA_World_5_DesertBulb,         // 35
        TRA_World_6_Mushroom,           // 36
        TRA_World_7_Root,               // 37
        TRA_World_8_Butterfly,          // 38
        TRA_World_9_Skullfly,           // 39
        TRA_World_10_Cocoon,            // 40
        TRA_World_11_Bones,             // 41
        TRA_World_12_Future,            // 42
        TRA_World_13_Future,            // 43
        TRA_World_14_Future,            // 44
        TRA_World_15_Future,            // 45
        TRA_World_16_Future,            // 46
        TRA_Chips_Small,                // 47
        TRA_Chips_Medium,               // 48
        TRA_Chips_Large,                // 49
        TRA_MAX                         // 50
    }

    public enum eAchievements_IB3
    {
        A_NONE,                         // 0
        TRACK_Combo,                    // 1
        TRACK_Parry,                    // 2
        TRACK_Dodge,                    // 3
        TRACK_Block,                    // 4
        TRACK_Magic,                    // 5
        TRACK_BonusCombo,               // 6
        TRACK_SuperMove,                // 7
        TRACK_Stab,                     // 8
        TRACK_FinishingHit,             // 9
        TRACK_PerfectParry,             // 10
        TRACK_PerfectBlock,             // 11
        TRACK_SuperDodge,               // 12
        TRACK_SirisLevel,               // 13
        TRACK_IsaLevel,                 // 14
        TRACK_MasteredItem,             // 15
        TRACK_Forge,                    // 16
        TRACK_ItemLevel,                // 17
        TRACK_GemCook,                  // 18
        TRACK_PotionCraft,              // 19
        TRACK_PotionUse,                // 20
        TRACK_GrabBags,                 // 21
        TRACK_Ingredients,              // 22
        TRACK_Merchant,                 // 23
        TRACK_ClashMob,                 // 24
        GOAL_Gold,                      // 25
        GOAL_InfinityBlade,             // 26
        GOAL_Combat1,                   // 27
        GOAL_Combat2,                   // 28
        GOAL_Combat3,                   // 29
        GOAL_Combat4,                   // 30
        GOAL_Combat5,                   // 31
        GOAL_Combat6,                   // 32
        PROG_Story1,                    // 33
        PROG_Story2,                    // 34
        PROG_Story3,                    // 35
        PROG_Story4,                    // 36
        PROG_Story5,                    // 37
        PROG_Story6,                    // 38
        PROG_Story7,                    // 39
        PROG_Story8,                    // 40
        PROG_Story9,                    // 41
        PROG_Story10,                   // 42
        PROG_Story11,                   // 43
        PROG_Story12,                   // 44
        PROG_NewGamePlus,               // 45
        TRACK_TitanKill,                // 46
        TRACK_Slash,                    // 47
        TRACK_RealPerfectParry,         // 48
        TRACK_ArenaMode,                // 49
        TRACK_DeathlessQuest,           // 50
        TRACK_BossPerkKils,             // 51
        GOAL_Collector1,                // 52
        GOAL_Collector2,                // 53
        GOAL_HolidayHelm1,              // 54
        GOAL_HolidayHelm2,              // 55
        GOAL_HolidayHelm3,              // 56
        GOAL_HolidayHelm4,              // 57
        GOAL_HolidayHelm5,              // 58
        GOAL_HolidayHelm6,              // 59
        GOAL_HolidayHelm7,              // 60
        GOAL_HolidayHelm8,              // 61
        GOAL_HolidayHelm9,              // 62
        GOAL_HolidayHelm10,             // 63
        GOAL_HolidayHelmAll,            // 64
        TRACK_AvatarEquipmentCost,      // 65
        AIB3_MAX_CHEEVO,                // 66
        TRACK_Weekly,                   // 67
        eAchievements_MAX               // 68
    };

    public enum EExternalType
    {
        ExType_GameCenter,              // 0
        ExType_None,                    // 1
        ExType_Twitter,                 // 2
        ExType_Facebook,                // 3
        ExType_Google,                  // 4
        ExType_MAX                      // 5
    };

    public enum ePlayerCharacterType
    {
        EPCT_Siris,                     // 0
        EPCT_Isa,                       // 1
        EPCT_AllValid,                  // 2
        EPCT_MAX                        // 3
    };
    #endregion

    #region IB2 Enums
    public enum eNewWorldType
    {
        NWT_SaveStart,                  // 0
        NWT_NewBloodline,               // 1
        NWT_NoSave,                     // 2
        NWT_NoSave2,                    // 3
        NWT_NoSave3,                    // 4
        NWT_MinusStart,                 // 5
        NWT_MAX                         // 6
    };

    public enum eTouchRewardActor_IB2
    {
        TRA_Random,                     // 0
        TRA_Random_Potion,              // 1
        TRA_Random_Gold,                // 2
        TRA_Random_Key,                 // 3
        TRA_Random_Gem,                 // 4
        TRA_Random_Item,                // 5
        TRA_None,                       // 6
        TRA_Gold_Small,                 // 7
        TRA_Gold_Medium,                // 8
        TRA_Gold_Large,                 // 9
        TRA_Key_Small,                  // 10
        TRA_Key_Medium,                 // 11
        TRA_Key_Large,                  // 12
        TRA_Key_Item,                   // 13
        TRA_Gem_Fixed,                  // 14
        TRA_Item_Fixed,                 // 15
        TRA_Item_Weapon,                // 16
        TRA_Item_Shield,                // 17
        TRA_Item_Armor,                 // 18
        TRA_Item_Helmet,                // 19
        TRA_Item_Magic,                 // 20
        TRA_GrabBag_Small,              // 21
        TRA_GrabBag_Medium,             // 22
        TRA_GrabBag_Large,              // 23
        TRA_GrabBag_SmallGem,           // 24
        TRA_GrabBag_MediumGem,          // 25
        TRA_GrabBag_LargeGem,           // 26
        TRA_GrabBag_Uber,               // 27
        TRA_Potion_HealthL,             // 28
        TRA_Potion_HealthRegen,         // 29
        TRA_Potion_ShieldRegen,         // 30
        TRA_Potion_EasyParry,           // 31
        TRA_Potion_HealthM,             // 32
        TRA_Potion_HealthS,             // 33
        TRA_Potion_HealthRegenL,        // 34
        TRA_Potion_DoubleXP,            // 35
        TRA_Potion_New5,                // 36
        TRA_Potion_New6,                // 37
        TRA_Potion_New7,                // 38
        TRA_Potion_New8,                // 39
        TRA_Potion_New9,                // 40
        TRA_MAX                         // 41
    };

    public enum eAchievements_IB2
    {
        A_NONE,                         // 0
        IB2_Combo1,                     // 1
        IB2_Block1,                     // 2
        IB2_Parry1,                     // 3
        IB2_Parry2,                     // 4
        IB2_Dodge1,                     // 5
        IB2_Stab1,                      // 6
        IB2_PerfectBlock1,              // 7
        IB2_PerfectParry1,              // 8
        IB2_PerfectSlash1,              // 9
        IB2_Level1,                     // 10
        IB2_Level2,                     // 11
        IB2_Gold1,                      // 12
        IB2_Treasure1,                  // 13
        IB2_ModifiedItems1,             // 14
        IB2_GrabBags1,                  // 15
        IB2_Master1,                    // 16
        IB2_Master2,                    // 17
        IB2_InfinityBlade,              // 18
        IB2_ClashMob1,                  // 19
        IB2_ClashMob2,                  // 20
        IB2_WinWOTakingDamage_SnS,      // 21
        IB2_WinWOTakingDamage_2S,       // 22
        IB2_WinWOTakingDamage_2H,       // 23
        IB2_WinWOAttacking,             // 24
        IB2_AllEquippedModified,        // 25
        IB2_KillUberBoss1,              // 26
        IB2_KillUberBoss2,              // 27
        IB2_KillUberBoss3,              // 28
        IB2_KillUberBoss4,              // 29
        IB2_KillUberBoss5,              // 30
        IB2_NewGamePlus,                // 31
        IB2_KillUberBoss6,              // 32
        IB2_KillUberBoss7,              // 33
        IB2_KillUberBoss8,              // 34
        IB2_KillUberBoss9,              // 35
        IB2_GemCooker1,                 // 36
        IB2_GemCooker2,                 // 37
        IB2_GemCooker3,                 // 38
        IB2_TreasureMapUsed,            // 39
        IB2_TreasureMapCollected,       // 40
        IB2_MeetTEL,                    // 41
        IB2_EquipLaserWeapon,           // 42
        IB2_ModifyALaserWeapon,         // 43
        IB2_KillUberBoss10,             // 44
        IB2_KillUberBoss11,             // 45
        IB2_KillUberBoss12,             // 46
        IB2_SpareUberBoss,              // 47
        IB2_FindUberBoss,               // 48
        IB2_GemCooker4,                 // 49
        IB2_ItemSet,                    // 50
        IB2_NegaGodKing,                // 51
        AIB2_MAX_CHEEVO,                // 52
        eAchievements_MAX               // 53
    };

    public enum eElementalType
    {
        AET_Random,                     // 0
        AET_Fire,                       // 1
        AET_Ice,                        // 2
        AET_Electric,                   // 3
        AET_Poison,                     // 4
        AET_Light,                      // 5
        AET_Dark,                       // 6
        AET_Wind,                       // 7
        AET_Water,                      // 8
        AET_MAX                         // 9
    };
    #endregion

    #region IB1 Enums

    #endregion

    #region VOTE
    enum CharacterFilterEnum
    {
        CFE_All,                        // 0
        CFE_Obama,                      // 1
        CFE_Romney,                     // 2
        CFE_MAX                         // 3
    };
    #endregion
}