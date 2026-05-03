namespace IBSaveEditor.UProperties.UArray;

public enum ArrayName
{

#region IB1
    PlaythroughItemsGiven,
#endregion

#region IB2 Arrays
    PlayerCookerGems,
    SuperBoss,
    ActiveBattlePotions,
    SocialChallengeSaveEvents,
        // SUBSET //
        GiftedTo,
        GiftedFrom,
#endregion

#region VOTE
    EquippedListO,
    EquippedListR,
#endregion

#region Static Arrays
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
    SaveFiles,
#endregion 

#region Dynamic Arrays
    PlayerCustom,
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
#endregion

#region IB3 Subset Arrays
    SocketedGemData,
    Gems,
    Reagents,
    BossElementalRandList,
    PersistActorCounts,
    DontClearPersistActorCounts,
    SavedItems,
    Quests,
    PendingAction,
    CustomCharacterValues,
    OwnedFeatures,
#endregion

#region Multi-Game Arrays
    TouchTreasureAwards,
#endregion
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
    SocialChallengeSave,
    CustomCharacterSave,
    SaveFileMetaData
}