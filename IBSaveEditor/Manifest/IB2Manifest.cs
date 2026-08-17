using IBSaveEditor.Package;
using static IBSaveEditor.Manifest.ManifestBuilder;

namespace IBSaveEditor.Manifest;

/// <summary>
/// IB2's manifest. The local corpus (131 files - the richest of any game here) is
/// visibly a mix of several save-format versions: presence rates split into clean
/// clusters around ~50%, ~8%, and under 1%, rather than clustering near 100% the way
/// IB3's more uniform corpus does. Every field below is real (confirmed against both
/// the "Encrypted IB2 Save.bin" and "Unencrypted IB2 Save.bin" test fixtures directly,
/// not just the aggregate census), but nearly all are <see cref="FieldSpec.Optional"/>
/// because that's what's honestly true across format versions in this corpus - a save
/// missing one of these fields isn't a bug, it's an older/newer save format.
/// <para>
/// "EquippedItemNames" is the same fixed 5-slot [Sword, Shield, Armor, Helmet, Magic]
/// order as IB1 and IB3 - confirmed directly rather than assumed, same as every other
/// game here (VOTE's order is different, so this was worth re-checking each time).
/// </para>
/// <para>
/// "CurrentTotalTrackingStats" has two visibly different field sets between the two
/// fixtures (an older, smaller set and a newer, richer one). Only the four counters
/// present in both are included here.
/// </para>
/// </summary>
public static class IB2Manifest
{
    public static readonly GameManifest Instance = new()
    {
        Game = Game.IB2,
        Tabs = new[]
        {
            Tab("Character",
                Section("Currency",
                    Field("CurrentGold", FieldKind.Money, "Gold")
                        .Optional(),
                    Field("TotalGoldAquired", FieldKind.Number, "Total Gold Acquired")
                        .Describe("Lifetime gold earned, including gold already spent.")
                        .Optional()
                ),
                Section("Identity",
                    Field("CharacterName", FieldKind.Text, "Character Name")
                        .Describe("Only present in newer save formats; often empty even when present.")
                        .Optional()
                ),
                Section("Stats",
                    Field("Health", FieldKind.Number, "Health")
                        .Optional(),
                    Field("HealthMax", FieldKind.Number, "Max Health")
                        .Optional(),
                    Field("PawnLevel", FieldKind.Number, "Level")
                        .Optional(),
                    Field("WorldLevel", FieldKind.Number, "World Level")
                        .Optional(),
                    Field("CurrentXP", FieldKind.Number, "Experience")
                        .Describe("Only present in newer save formats.")
                        .Optional()
                ),
                Section("Equipment",
                    Field("EquippedItemNames[0]", FieldKind.ItemRef, "Equipped Weapon")
                        .Category("weapon").Optional(),
                    Field("EquippedItemNames[1]", FieldKind.ItemRef, "Equipped Shield")
                        .Category("shield").Optional(),
                    Field("EquippedItemNames[2]", FieldKind.ItemRef, "Equipped Armor")
                        .Category("armor").Optional(),
                    Field("EquippedItemNames[3]", FieldKind.ItemRef, "Equipped Helmet")
                        .Category("helmet").Optional(),
                    Field("EquippedItemNames[4]", FieldKind.ItemRef, "Equipped Magic")
                        .Category("magic").Optional()
                )
            ),
            Tab("Progress",
                Section("Bloodline",
                    Field("GenerationCount", FieldKind.Number, "Bloodline Generation")
                        .Describe("Number of times this bloodline has been reborn.")
                        .Optional(),
                    Field("MaxGeneration", FieldKind.Number, "Max Bloodline Generation Reached")
                        .Optional(),
                    Field("FightFinishedCount", FieldKind.Number, "Total Fights Won")
                        .Optional(),
                    Field("PlayThroughFightFinishedCount", FieldKind.Number, "Fights Won This Playthrough")
                        .Optional()
                ),
                Section("Combat Stats",
                    Field("CurrentTotalTrackingStats.TotalParryCount", FieldKind.Number, "Total Parries")
                        .Optional(),
                    Field("CurrentTotalTrackingStats.TotalPerfectParryCount", FieldKind.Number, "Total Perfect Parries")
                        .Optional(),
                    Field("CurrentTotalTrackingStats.TotalBlockCount", FieldKind.Number, "Total Blocks")
                        .Optional(),
                    Field("CurrentTotalTrackingStats.TotalDodgeCount", FieldKind.Number, "Total Dodges")
                        .Optional()
                ),
                Section("World & Economy",
                    Field("TotalTreasureChest", FieldKind.Number, "Treasure Chests Found")
                        .Optional(),
                    Field("TotalGrabBagsUsed", FieldKind.Number, "Grab Bags Used")
                        .Optional(),
                    Field("MaxGodKingDefeatedLevel", FieldKind.Number, "Max God King Level Defeated")
                        .Describe("Only present once the God King has been fought at least once.")
                        .Optional()
                )
            ),
        },
    };
}
