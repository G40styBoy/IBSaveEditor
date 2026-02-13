/// <summary>
/// Used to package and pass tag data neatly. 
/// Ends up being used in uproperty construction
/// </summary>
public record struct TagContainer
{
    //UProperty
    public string name;
    public string type;
    public int size;
    public int arrayIndex;

    /// <summary>
    /// stores struct alternate name. This is unused
    /// </summary>
    public string alternateName;

    //UArray
    public int arrayEntryCount;
    public ArrayMetadata? arrayInfo;

    /// <summary>
    /// lets us know if we need to keep track of a properties total size for struct and array purposes
    /// </summary>
    public bool bShouldTrackMetadataSize;
}

/// <summary>
/// String representation of all UProperty types discoverable inside of an Infinity Blade Save.
/// </summary>
public record struct UType
{
    public const string INT_PROPERTY = "IntProperty";
    public const string FLOAT_PROPERTY = "FloatProperty";
    public const string BYTE_PROPERTY = "ByteProperty";
    public const string BOOL_PROPERTY = "BoolProperty";
    public const string STR_PROPERTY = "StrProperty";
    public const string NAME_PROPERTY = "NameProperty";
    public const string STRUCT_PROPERTY = "StructProperty";
    public const string ARRAY_PROPERTY = "ArrayProperty";
    public const string NONE = "None";
}

/// <summary>
/// Enum representation of all UProperty types discoverable inside of an Infinity Blade Save.
/// </summary>
public enum PropertyType
{
    StructProperty,
    ArrayProperty,
    IntProperty,
    StrProperty,
    NameProperty,
    FloatProperty,
    BoolProperty,
    ByteProperty
}

public enum ArrayType : byte
{
    Static,
    Dynamic
}

public enum AlternateName
{
    None,
    CurrencyStruct,
    PlayerSavedStats,
    PlayerEquippedItemList,
    NumConsumable,
    GemCookerData,
    ItemForgeData,
    PotionCauldronData,
    SavedCheevoData,
    ShowConsumableBadge,
    LastEquippedWeaponOfType,

    //ib2
    SocialChallengeSave
}

public enum ArrayName
{
    // IB1 //
    PlaythroughItemsGiven,
    // and TouchTreasureAwards

    // IB2 //
    PlayerCookerGems,
    SuperBoss,
    ActiveBattlePotions,
    SocialChallengeSaveEvents,
        // SUBSET //
        GiftedTo,
        GiftedFrom,

    // VOTE //
    EquippedListO,
    EquippedListR,

    // IB3 //
    // Static Arrays
    Currency,
    Stats,
    NumConsumable,
    ShowConsumableBadge,
    GemCooker,
    ItemForge,
    PotionCauldron,
    SavedCheevo,
    LastEquippedWeaponOfType,
    CharacterEquippedList,

    // Dynamic Arrays
    EquippedItemNames,
    EquippedItems,
    LinkNotificationBadges,
    CurrentKeyItemList,
    UsedKeyItemList,
    PlayerInventory,
    PlayerUnequippedGems,
    CurrentStoreGems,
    InActivePotionList,
    ActivePotions,
    PurchasedPerks,
    GameFlagList,
    BossFixedWorldInfo,
    TouchTreasureAwards,
    WorldItemOrderList,
    TreasureChestOpened,
    BossesGeneratedThisBloodline,
    PotentialBossElementalAttacks,
    PerLevelData,
    CurrentBattleChallengeList,
    SavedPersistentBossData,
    HardCoreCurrentQuestData,
    LoggedAnalyticsAchievements,
    McpAuthorizedServices,

    // Subset Arrays
    SocketedGemData,
    Gems,
    Reagents,
    BossElementalRandList,
    PersistActorCounts,
    DontClearPersistActorCounts,
    SavedItems,
    Quests,
    PendingAction
}