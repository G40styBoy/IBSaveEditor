public class ArrayMetadata
{
    public ArrayName arrayName;
    public AlternateName alternateName;
    public PropertyType valueType;
    public ArrayType arrayType;

    public ArrayMetadata(ArrayName arrayName, AlternateName alternateName, PropertyType valueType, ArrayType arrayType)
    {
        this.arrayName = arrayName;
        this.alternateName = alternateName;
        this.valueType = valueType;
        this.arrayType = arrayType;
    }

    public void PrintInfo()
    {
        Console.WriteLine($"Array Name: {arrayName}");
        Console.WriteLine($"Alternate Name: {alternateName}");
        Console.WriteLine($"Value Type: {valueType}");
        Console.WriteLine($"Static: {arrayType}\n");
    }
}

public static class UArrayRegistry
{
    private static readonly IReadOnlyDictionary<ArrayName, ArrayMetadata> Common =
        new Dictionary<ArrayName, ArrayMetadata>
        {
            // Static Arrays
            [ArrayName.Currency] = new(ArrayName.Currency, AlternateName.CurrencyStruct, PropertyType.StructProperty, ArrayType.Static),
            [ArrayName.Stats] = new(ArrayName.Stats, AlternateName.PlayerSavedStats, PropertyType.StructProperty, ArrayType.Static),
            [ArrayName.NumConsumable] = new(ArrayName.NumConsumable, AlternateName.None, PropertyType.IntProperty, ArrayType.Static),
            [ArrayName.ShowConsumableBadge] = new(ArrayName.ShowConsumableBadge, AlternateName.ShowConsumableBadge, PropertyType.ByteProperty, ArrayType.Static),
            [ArrayName.GemCooker] = new(ArrayName.GemCooker, AlternateName.GemCookerData, PropertyType.StructProperty, ArrayType.Static),
            [ArrayName.ItemForge] = new(ArrayName.ItemForge, AlternateName.ItemForgeData, PropertyType.StructProperty, ArrayType.Static),
            [ArrayName.PotionCauldron] = new(ArrayName.PotionCauldron, AlternateName.PotionCauldronData, PropertyType.StructProperty, ArrayType.Static),
            [ArrayName.SavedCheevo] = new(ArrayName.SavedCheevo, AlternateName.SavedCheevoData, PropertyType.StructProperty, ArrayType.Static),
            [ArrayName.LastEquippedWeaponOfType] = new(ArrayName.LastEquippedWeaponOfType, AlternateName.LastEquippedWeaponOfType, PropertyType.NameProperty, ArrayType.Static),
            [ArrayName.CharacterEquippedList] = new(ArrayName.CharacterEquippedList, AlternateName.PlayerEquippedItemList, PropertyType.StructProperty, ArrayType.Static),

            // Dynamic Arrays
            [ArrayName.EquippedItemNames] = new(ArrayName.EquippedItemNames, AlternateName.None, PropertyType.NameProperty, ArrayType.Dynamic),
            [ArrayName.EquippedItems] = new(ArrayName.EquippedItems, AlternateName.None, PropertyType.NameProperty, ArrayType.Dynamic),
            [ArrayName.LinkNotificationBadges] = new(ArrayName.LinkNotificationBadges, AlternateName.None, PropertyType.StructProperty, ArrayType.Dynamic),
            [ArrayName.CurrentKeyItemList] = new(ArrayName.CurrentKeyItemList, AlternateName.None, PropertyType.NameProperty, ArrayType.Dynamic),
            [ArrayName.UsedKeyItemList] = new(ArrayName.UsedKeyItemList, AlternateName.None, PropertyType.NameProperty, ArrayType.Dynamic),
            [ArrayName.PlayerInventory] = new(ArrayName.PlayerInventory, AlternateName.None, PropertyType.StructProperty, ArrayType.Dynamic),
            [ArrayName.PlayerUnequippedGems] = new(ArrayName.PlayerUnequippedGems, AlternateName.None, PropertyType.StructProperty, ArrayType.Dynamic),
            [ArrayName.CurrentStoreGems] = new(ArrayName.CurrentStoreGems, AlternateName.None, PropertyType.StructProperty, ArrayType.Dynamic),
            [ArrayName.InActivePotionList] = new(ArrayName.InActivePotionList, AlternateName.None, PropertyType.StructProperty, ArrayType.Dynamic),
            [ArrayName.ActivePotions] = new(ArrayName.ActivePotions, AlternateName.None, PropertyType.StructProperty, ArrayType.Dynamic),
            [ArrayName.PurchasedPerks] = new(ArrayName.PurchasedPerks, AlternateName.None, PropertyType.NameProperty, ArrayType.Dynamic),
            [ArrayName.GameFlagList] = new(ArrayName.GameFlagList, AlternateName.None, PropertyType.IntProperty, ArrayType.Dynamic),
            [ArrayName.BossFixedWorldInfo] = new(ArrayName.BossFixedWorldInfo, AlternateName.None, PropertyType.StructProperty, ArrayType.Dynamic),
            [ArrayName.WorldItemOrderList] = new(ArrayName.WorldItemOrderList, AlternateName.None, PropertyType.NameProperty, ArrayType.Dynamic),
            [ArrayName.TreasureChestOpened] = new(ArrayName.TreasureChestOpened, AlternateName.None, PropertyType.NameProperty, ArrayType.Dynamic),
            [ArrayName.BossesGeneratedThisBloodline] = new(ArrayName.BossesGeneratedThisBloodline, AlternateName.None, PropertyType.StrProperty, ArrayType.Dynamic),
            [ArrayName.PotentialBossElementalAttacks] = new(ArrayName.PotentialBossElementalAttacks, AlternateName.None, PropertyType.NameProperty, ArrayType.Dynamic),
            [ArrayName.PerLevelData] = new(ArrayName.PerLevelData, AlternateName.None, PropertyType.StructProperty, ArrayType.Dynamic),
            [ArrayName.CurrentBattleChallengeList] = new(ArrayName.CurrentBattleChallengeList, AlternateName.None, PropertyType.NameProperty, ArrayType.Dynamic),
            [ArrayName.SavedPersistentBossData] = new(ArrayName.SavedPersistentBossData, AlternateName.None, PropertyType.StructProperty, ArrayType.Dynamic),
            [ArrayName.HardCoreCurrentQuestData] = new(ArrayName.HardCoreCurrentQuestData, AlternateName.None, PropertyType.NameProperty, ArrayType.Dynamic),
            [ArrayName.LoggedAnalyticsAchievements] = new(ArrayName.LoggedAnalyticsAchievements, AlternateName.None, PropertyType.NameProperty, ArrayType.Dynamic),
            [ArrayName.McpAuthorizedServices] = new(ArrayName.McpAuthorizedServices, AlternateName.None, PropertyType.NameProperty, ArrayType.Dynamic),

            // Subset Arrays
            [ArrayName.Gems] = new(ArrayName.Gems, AlternateName.None, PropertyType.StructProperty, ArrayType.Dynamic),
            [ArrayName.SocketedGemData] = new(ArrayName.SocketedGemData, AlternateName.None, PropertyType.StructProperty, ArrayType.Dynamic),
            [ArrayName.Reagents] = new(ArrayName.Reagents, AlternateName.None, PropertyType.NameProperty, ArrayType.Dynamic),
            [ArrayName.BossElementalRandList] = new(ArrayName.BossElementalRandList, AlternateName.None, PropertyType.FloatProperty, ArrayType.Dynamic),
            [ArrayName.PersistActorCounts] = new(ArrayName.PersistActorCounts, AlternateName.None, PropertyType.StructProperty, ArrayType.Dynamic),
            [ArrayName.DontClearPersistActorCounts] = new(ArrayName.DontClearPersistActorCounts, AlternateName.None, PropertyType.StructProperty, ArrayType.Dynamic),
            [ArrayName.SavedItems] = new(ArrayName.SavedItems, AlternateName.None, PropertyType.NameProperty, ArrayType.Dynamic),
            [ArrayName.Quests] = new(ArrayName.Quests, AlternateName.None, PropertyType.StructProperty, ArrayType.Dynamic),
            [ArrayName.PendingAction] = new(ArrayName.PendingAction, AlternateName.None, PropertyType.StructProperty, ArrayType.Dynamic),

            // IB2-ish / shared
            [ArrayName.PlayerCookerGems] = new(ArrayName.PlayerCookerGems, AlternateName.None, PropertyType.StructProperty, ArrayType.Dynamic),
            [ArrayName.SuperBoss] = new(ArrayName.SuperBoss, AlternateName.None, PropertyType.IntProperty, ArrayType.Dynamic),
            [ArrayName.ActiveBattlePotions] = new(ArrayName.ActiveBattlePotions, AlternateName.None, PropertyType.NameProperty, ArrayType.Dynamic),

            [ArrayName.SocialChallengeSaveEvents] = new(ArrayName.SocialChallengeSaveEvents, AlternateName.SocialChallengeSave, PropertyType.StructProperty, ArrayType.Dynamic),
            [ArrayName.GiftedTo] = new(ArrayName.GiftedTo, AlternateName.None, PropertyType.StructProperty, ArrayType.Dynamic),
            [ArrayName.GiftedFrom] = new(ArrayName.GiftedFrom, AlternateName.None, PropertyType.StructProperty, ArrayType.Dynamic),

            // IB1
            [ArrayName.PlaythroughItemsGiven] = new(ArrayName.PlaythroughItemsGiven, AlternateName.None, PropertyType.NameProperty, ArrayType.Dynamic),

            // VOTE
            [ArrayName.EquippedListO] = new(ArrayName.EquippedListO, AlternateName.None, PropertyType.NameProperty, ArrayType.Dynamic),
            [ArrayName.EquippedListR] = new(ArrayName.EquippedListR, AlternateName.None, PropertyType.NameProperty, ArrayType.Dynamic),
        };

    private static readonly IReadOnlyDictionary<Game, IReadOnlyDictionary<ArrayName, ArrayMetadata>> PerGame =
    new Dictionary<Game, IReadOnlyDictionary<ArrayName, ArrayMetadata>>
    {
        [Game.IB1] = new Dictionary<ArrayName, ArrayMetadata>
        {
            [ArrayName.TouchTreasureAwards] = new(ArrayName.TouchTreasureAwards, AlternateName.None, PropertyType.NameProperty, ArrayType.Dynamic)
        },
        [Game.IB2] = new Dictionary<ArrayName, ArrayMetadata>
        {
            [ArrayName.TouchTreasureAwards] = new(ArrayName.TouchTreasureAwards, AlternateName.None, PropertyType.StructProperty, ArrayType.Dynamic)
        },
        [Game.IB3] = new Dictionary<ArrayName, ArrayMetadata>
        {
            [ArrayName.TouchTreasureAwards] = new(ArrayName.TouchTreasureAwards, AlternateName.None, PropertyType.StructProperty, ArrayType.Dynamic)
        },
        [Game.VOTE] = new Dictionary<ArrayName, ArrayMetadata>
        {
            [ArrayName.TouchTreasureAwards] = new(ArrayName.TouchTreasureAwards, AlternateName.None, PropertyType.StructProperty, ArrayType.Dynamic)
        },
    };

    private static readonly Dictionary<Game, IReadOnlyDictionary<ArrayName, ArrayMetadata>> Cache = new();

    public static IReadOnlyDictionary<ArrayName, ArrayMetadata> GetAll(Game game)
    {
        if (Cache.TryGetValue(game, out var cached))
            return cached;

        var merged = new Dictionary<ArrayName, ArrayMetadata>(Common);

        if (PerGame.TryGetValue(game, out var gameMap))
        {
            foreach (var kv in gameMap)
                merged[kv.Key] = kv.Value;
        }

        Cache[game] = merged;
        return merged;
    }

    public static bool TryGet(Game game, string name, out ArrayMetadata? metadata)
    {
        metadata = null;

        if (!Enum.TryParse<ArrayName>(name, out var arrayName) ||
            !Enum.IsDefined(typeof(ArrayName), arrayName))
            return false;

        return GetAll(game).TryGetValue(arrayName, out metadata);
    }
}