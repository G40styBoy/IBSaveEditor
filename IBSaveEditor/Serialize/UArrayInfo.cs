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

public record ArrayMetadata
{
    public ArrayName arrayName;
    public AlternateName alternateName;
    public PropertyType valueType;
    public ArrayType arrayType;

    public ArrayMetadata(ArrayName arrayName, AlternateName alternateName, PropertyType PropertyType, ArrayType type)
    {
        this.arrayName = arrayName;
        this.alternateName = alternateName;
        valueType = PropertyType;
        arrayType = type;
    }

    internal void PrintInfo()
    {
        Console.WriteLine($"Array Name: {arrayName}");
        Console.WriteLine($"Alternate Name: {alternateName}");
        Console.WriteLine($"Value Type: {valueType}");
        Console.WriteLine($"Static: {arrayType}\n");
    }
}

public class UArray
{
    public static ArrayMetadata? GetCurrentArray(List<ArrayMetadata> gameArrayInfo, string name)
    {
        try
        {
            if (!IsArray(name))
                return null;

            var arrayName = (ArrayName)Enum.Parse(typeof(ArrayName), name);
            var info = gameArrayInfo.FirstOrDefault(array => array.arrayName == arrayName);

            if (info == default)
                throw new ArgumentException($"Invalid array name: {name}");

            return info;   
        }
        catch (Exception){
            return null;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsArray(string arrayName) => Enum.IsDefined(typeof(ArrayName), arrayName);

    public static List<ArrayMetadata> PopulateArrayInfo(Game gameType)
    {
        var arraysToInstantiate = new List<ArrayMetadata>();
        arraysToInstantiate.AddRange(new List<ArrayMetadata>
        {
            // Static Arrays
            new ArrayMetadata(ArrayName.Currency, AlternateName.CurrencyStruct, PropertyType.StructProperty, ArrayType.Static),
            new ArrayMetadata(ArrayName.Stats, AlternateName.PlayerSavedStats, PropertyType.StructProperty, ArrayType.Static),
            new ArrayMetadata(ArrayName.NumConsumable, AlternateName.None, PropertyType.IntProperty, ArrayType.Static),
            new ArrayMetadata(ArrayName.ShowConsumableBadge, AlternateName.ShowConsumableBadge, PropertyType.ByteProperty, ArrayType.Static),
            new ArrayMetadata(ArrayName.GemCooker, AlternateName.GemCookerData, PropertyType.StructProperty, ArrayType.Static),
            new ArrayMetadata(ArrayName.ItemForge, AlternateName.ItemForgeData, PropertyType.StructProperty, ArrayType.Static),
            new ArrayMetadata(ArrayName.PotionCauldron, AlternateName.PotionCauldronData, PropertyType.StructProperty, ArrayType.Static),
            new ArrayMetadata(ArrayName.SavedCheevo, AlternateName.SavedCheevoData, PropertyType.StructProperty, ArrayType.Static),
            new ArrayMetadata(ArrayName.LastEquippedWeaponOfType, AlternateName.LastEquippedWeaponOfType, PropertyType.NameProperty, ArrayType.Static),
            new ArrayMetadata(ArrayName.CharacterEquippedList, AlternateName.PlayerEquippedItemList, PropertyType.StructProperty, ArrayType.Static), 

            // Dynamic Arrays
            new ArrayMetadata(ArrayName.EquippedItemNames, AlternateName.None, PropertyType.NameProperty, ArrayType.Dynamic),
            new ArrayMetadata(ArrayName.EquippedItems, AlternateName.None, PropertyType.NameProperty, ArrayType.Dynamic),
            new ArrayMetadata(ArrayName.LinkNotificationBadges, AlternateName.None, PropertyType.StructProperty, ArrayType.Dynamic),
            new ArrayMetadata(ArrayName.CurrentKeyItemList, AlternateName.None, PropertyType.NameProperty, ArrayType.Dynamic),
            new ArrayMetadata(ArrayName.UsedKeyItemList, AlternateName.None, PropertyType.NameProperty, ArrayType.Dynamic),
            new ArrayMetadata(ArrayName.PlayerInventory, AlternateName.None, PropertyType.StructProperty, ArrayType.Dynamic),
            new ArrayMetadata(ArrayName.PlayerUnequippedGems, AlternateName.None, PropertyType.StructProperty, ArrayType.Dynamic),
            new ArrayMetadata(ArrayName.CurrentStoreGems, AlternateName.None, PropertyType.StructProperty, ArrayType.Dynamic),
            new ArrayMetadata(ArrayName.InActivePotionList, AlternateName.None, PropertyType.StructProperty, ArrayType.Dynamic),
            new ArrayMetadata(ArrayName.ActivePotions, AlternateName.None, PropertyType.StructProperty, ArrayType.Dynamic),
            new ArrayMetadata(ArrayName.PurchasedPerks, AlternateName.None, PropertyType.NameProperty, ArrayType.Dynamic),
            new ArrayMetadata(ArrayName.GameFlagList, AlternateName.None, PropertyType.IntProperty, ArrayType.Dynamic),
            new ArrayMetadata(ArrayName.BossFixedWorldInfo, AlternateName.None, PropertyType.StructProperty, ArrayType.Dynamic),
            new ArrayMetadata(ArrayName.WorldItemOrderList, AlternateName.None, PropertyType.NameProperty, ArrayType.Dynamic),
            new ArrayMetadata(ArrayName.TreasureChestOpened, AlternateName.None, PropertyType.NameProperty, ArrayType.Dynamic),
            new ArrayMetadata(ArrayName.BossesGeneratedThisBloodline, AlternateName.None, PropertyType.StrProperty, ArrayType.Dynamic),
            new ArrayMetadata(ArrayName.PotentialBossElementalAttacks, AlternateName.None, PropertyType.NameProperty, ArrayType.Dynamic),
            new ArrayMetadata(ArrayName.PerLevelData, AlternateName.None, PropertyType.StructProperty, ArrayType.Dynamic),
            new ArrayMetadata(ArrayName.CurrentBattleChallengeList, AlternateName.None, PropertyType.NameProperty, ArrayType.Dynamic),
            new ArrayMetadata(ArrayName.SavedPersistentBossData, AlternateName.None, PropertyType.StructProperty, ArrayType.Dynamic),
            new ArrayMetadata(ArrayName.HardCoreCurrentQuestData, AlternateName.None, PropertyType.NameProperty, ArrayType.Dynamic),
            new ArrayMetadata(ArrayName.LoggedAnalyticsAchievements, AlternateName.None, PropertyType.NameProperty, ArrayType.Dynamic),
            new ArrayMetadata(ArrayName.McpAuthorizedServices, AlternateName.None, PropertyType.NameProperty, ArrayType.Dynamic),

                // Subset Arrays
                new ArrayMetadata(ArrayName.Gems, AlternateName.None, PropertyType.StructProperty, ArrayType.Dynamic),
                new ArrayMetadata(ArrayName.SocketedGemData, AlternateName.None, PropertyType.StructProperty, ArrayType.Dynamic),
                new ArrayMetadata(ArrayName.Reagents, AlternateName.None, PropertyType.NameProperty, ArrayType.Dynamic),
                new ArrayMetadata(ArrayName.BossElementalRandList, AlternateName.None, PropertyType.FloatProperty, ArrayType.Dynamic),
                new ArrayMetadata(ArrayName.PersistActorCounts, AlternateName.None, PropertyType.StructProperty, ArrayType.Dynamic),
                new ArrayMetadata(ArrayName.DontClearPersistActorCounts, AlternateName.None, PropertyType.StructProperty, ArrayType.Dynamic),
                new ArrayMetadata(ArrayName.SavedItems, AlternateName.None, PropertyType.NameProperty, ArrayType.Dynamic),
                new ArrayMetadata(ArrayName.Quests, AlternateName.None, PropertyType.StructProperty, ArrayType.Dynamic),
                new ArrayMetadata(ArrayName.PendingAction, AlternateName.None, PropertyType.StructProperty, ArrayType.Dynamic),

            // IB2
            new ArrayMetadata(ArrayName.PlayerCookerGems, AlternateName.None, PropertyType.StructProperty, ArrayType.Dynamic),
            new ArrayMetadata(ArrayName.SuperBoss, AlternateName.None, PropertyType.IntProperty, ArrayType.Dynamic),
            new ArrayMetadata(ArrayName.ActiveBattlePotions, AlternateName.None, PropertyType.NameProperty, ArrayType.Dynamic),

                // Subset Arrays
                new ArrayMetadata(ArrayName.SocialChallengeSaveEvents, AlternateName.SocialChallengeSave, PropertyType.StructProperty, ArrayType.Dynamic),
                new ArrayMetadata(ArrayName.GiftedTo, AlternateName.None, PropertyType.StructProperty, ArrayType.Dynamic),
                new ArrayMetadata(ArrayName.GiftedFrom, AlternateName.None, PropertyType.StructProperty, ArrayType.Dynamic),

            // IB1
            new ArrayMetadata(ArrayName.PlaythroughItemsGiven, AlternateName.None, PropertyType.NameProperty, ArrayType.Dynamic),

            // VOTE
            new ArrayMetadata(ArrayName.EquippedListO, AlternateName.None, PropertyType.NameProperty, ArrayType.Dynamic),
            new ArrayMetadata(ArrayName.EquippedListR, AlternateName.None, PropertyType.NameProperty, ArrayType.Dynamic)
        });

        if (gameType is Game.IB1)
            arraysToInstantiate.Add(new ArrayMetadata(ArrayName.TouchTreasureAwards, AlternateName.None, PropertyType.NameProperty, ArrayType.Dynamic));
        else
            arraysToInstantiate.Add(new ArrayMetadata(ArrayName.TouchTreasureAwards, AlternateName.None, PropertyType.StructProperty, ArrayType.Dynamic));


        return arraysToInstantiate;
    }
}
