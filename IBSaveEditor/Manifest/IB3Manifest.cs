using IBSaveEditor.Package;
using static IBSaveEditor.Manifest.ManifestBuilder;

namespace IBSaveEditor.Manifest;

/// <summary>
/// IB3's manifest. Filled in tab by tab (Phase 5); each tab lands with the
/// manifest corpus honesty test green before the next one starts.
/// <para>
/// Every field path below was picked from <c>docs/schema/IB3.census.json</c>
/// (the save-corpus schema census) using its real presence rates; fields
/// below 100% are marked <see cref="FieldSpec.Optional"/> so the manifest
/// corpus honesty test stays green instead of masking the gap.
/// </para>
/// <para>
/// "EquippedItemNames" is a fixed 5-slot array; slot order was confirmed by
/// inspecting a real save rather than guessed - index 0 is always a
/// "Sword_*" internalName, matching the weapon catalog category.
/// </para>
/// </summary>
public static class IB3Manifest
{
    public static readonly GameManifest Instance = new()
    {
        Game = Game.IB3,
        Tabs = new[]
        {
            Tab("Character",
                Section("Currency",
                    Field("Currency[0].Current", FieldKind.Money, "Gold")
                        .Describe("Spendable gold."),
                    Field("Currency[0].TotalAcquired", FieldKind.Number, "Total Gold Acquired")
                        .Describe("Lifetime gold earned, including gold already spent.")
                ),
                Section("Identity",
                    Field("CharacterName", FieldKind.Text, "Character Name")
                ),
                Section("Stats",
                    Field("Health", FieldKind.Number, "Health"),
                    Field("HealthMax", FieldKind.Number, "Max Health"),
                    Field("PawnLevel", FieldKind.Number, "Level"),
                    Field("WorldLevel", FieldKind.Number, "World Level"),
                    Field("CurrentXP", FieldKind.Number, "Experience")
                        .Optional(), // Absent on at least one save in the corpus - not always tracked pre-first-battle.
                    Field("CurrentMagicLevel", FieldKind.Number, "Magic Level")
                        .Describe("Magic power multiplier, not a level count.")
                        .Optional(),
                    Field("MaxGodKingDefeatedLevel", FieldKind.Number, "Max God King Defeated Level")
                        .Describe("Only present once the God King has been fought at least once.")
                        .Optional()
                ),
                Section("Equipment",
                    Field("EquippedItemNames[0]", FieldKind.ItemRef, "Equipped Weapon")
                        .Category("weapon"),
                    Field("EquippedItemNames[1]", FieldKind.ItemRef, "Equipped Shield")
                        .Category("shield"),
                    Field("EquippedItemNames[2]", FieldKind.ItemRef, "Equipped Armor")
                        .Category("armor"),
                    Field("EquippedItemNames[3]", FieldKind.ItemRef, "Equipped Helmet")
                        .Category("helmet"),
                    Field("EquippedItemNames[4]", FieldKind.ItemRef, "Equipped Magic")
                        .Category("magic")
                ),
                Section("Flags",
                    Field("GameOptions.bSeenCustomChar", FieldKind.Toggle, "Seen Custom Character Screen")
                        .Optional(),
                    Field("bHasSeenCustomizeNotificationBadge", FieldKind.Toggle, "Seen Customize Notification Badge")
                        .Optional()
                )
            ),
            Tab("Progress",
                Section("Bloodline",
                    Field("GenerationCount", FieldKind.Number, "Bloodline Generation")
                        .Describe("Number of times this bloodline has been reborn."),
                    Field("FightFinishedCount", FieldKind.Number, "Total Fights Won"),
                    Field("PlayThroughFightFinishedCount", FieldKind.Number, "Fights Won This Playthrough")
                        .Optional()
                ),
                Section("Combat Stats",
                    Field("CurrentTotalTrackingStats.TotalComboCount", FieldKind.Number, "Total Combos"),
                    Field("CurrentTotalTrackingStats.TotalGoodHits", FieldKind.Number, "Total Good Hits"),
                    Field("CurrentTotalTrackingStats.TotalBlockCount", FieldKind.Number, "Total Blocks"),
                    Field("CurrentTotalTrackingStats.TotalPerfectBlockCount", FieldKind.Number, "Total Perfect Blocks")
                        .Optional(),
                    Field("CurrentTotalTrackingStats.TotalParryCount", FieldKind.Number, "Total Parries"),
                    Field("CurrentTotalTrackingStats.TotalPerfectParryCount", FieldKind.Number, "Total Perfect Parries")
                        .Optional(),
                    Field("CurrentTotalTrackingStats.TotalRealPerfectParryCount", FieldKind.Number, "Total Real Perfect Parries")
                        .Describe("Perfect parries against a live attack, excluding parries thrown against nothing.")
                        .Optional(),
                    Field("CurrentTotalTrackingStats.TotalDodgeCount", FieldKind.Number, "Total Dodges")
                ),
                Section("World & Economy",
                    Field("TotalTreasureChest", FieldKind.Number, "Treasure Chests Found")
                        .Optional(),
                    Field("TotalGrabBagsUsed", FieldKind.Number, "Grab Bags Used")
                        .Optional()
                )
            ),
            Tab("Inventory",
                Section("Consumables",
                    Field("NumConsumable[0].TRA_Potion_HealthL", FieldKind.Number, "Health Potions"),
                    Field("NumConsumable[0].TRA_GrabBag_Small", FieldKind.Number, "Small Grab Bags")
                        .Optional(),
                    Field("NumConsumable[0].TRA_GrabBag_SmallGem", FieldKind.Number, "Small Gem Grab Bags")
                        .Optional()
                )
            ).WithCollections(
                // PlayerInventory tracks every catalog item (owned or not) - 423 rows in a
                // typical save. Filtered to what's actually owned, per the design call: a
                // heavily-progressed save can still leave 400+ rows, which is a real,
                // acknowledged UI-performance cost of the no-virtualization ItemsControl
                // this renders through, not a false economy.
                Collection("PlayerInventory", "Owned Items",
                    Column("ini_ItemName", FieldKind.ItemRef, "Item"),
                    Column("NumberPlayerHas", FieldKind.Number, "Count"),
                    Column("ForgeLevel", FieldKind.Number, "Forge Level"),
                    Column("bIsMastered", FieldKind.Toggle, "Mastered")
                ).Title("ini_ItemName").WhereOwned("NumberPlayerHas"),

                // Spare gems sitting in inventory, not socketed into anything - naturally
                // small (a handful to a few dozen), no filter needed.
                Collection("PlayerUnequippedGems", "Spare Gems",
                    Column("ini_GemName", FieldKind.GemRef, "Gem").Category("gem"),
                    Column("bGemTier", FieldKind.Number, "Tier"),
                    Column("RandomAddPct", FieldKind.Number, "Bonus %")
                ).Title("ini_GemName").Optional() // Absent entirely on saves with no spare gems yet.
            ),
            Tab("Quests",
                Section("Overview",
                    Field("GameOptions.ini_CurrentQuest", FieldKind.Text, "Current Quest")
                        .Describe("Raw quest identifier (FName), e.g. \"02_MainQuest\".")
                        .Optional(),
                    // First real EnumChoice entry in a shipped manifest. Confirms the
                    // two-tier candidate source against a real save field rather than
                    // only the synthetic ones in EnumChoiceFieldTests.
                    Field("GameOptions.eShowQuests", FieldKind.EnumChoice, "Quest Notification State")
                )
            ).WithCollections(
                Collection("GameOptions.Quests", "All Quests",
                    Column("ini_QuestName", FieldKind.Text, "Quest"),
                    Column("NumCompletions", FieldKind.Number, "Completions"),
                    Column("bIsActive", FieldKind.Toggle, "Active"),
                    Column("bIsAvailable", FieldKind.Toggle, "Available"),
                    Column("ePlayerType", FieldKind.EnumChoice, "Player Type"),
                    Column("eArenaDifficulty", FieldKind.EnumChoice, "Arena Difficulty")
                ).Title("ini_QuestName")
            ),
        },
    };
}
