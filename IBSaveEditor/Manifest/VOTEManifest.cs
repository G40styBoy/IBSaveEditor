using IBSaveEditor.Package;
using static IBSaveEditor.Manifest.ManifestBuilder;

namespace IBSaveEditor.Manifest;

/// <summary>
/// VOTE's manifest. The local corpus is only 2 files, and unusually for this project,
/// their census presence rates are useless as evidence: every field below shows exactly
/// 50% in <c>docs/schema/VOTE.census.json</c>, with zero field-name overlap between the
/// two files. That's not "sometimes present" - it means the two files are different save
/// *shapes* entirely: one is a full in-progress character save (the shape this manifest
/// targets), the other looks like a save-slot index/summary (SaveFiles[], CloudDocIndex,
/// UpdateSaveCount - no Health, no PlayerInventory, no CurrentTotalTrackingStats at all).
/// <para>
/// Every field here was instead confirmed directly against the "Encrypted VOTE Save.bin"
/// test fixture (a real character save, correctly game-detected - see
/// <c>UnrealPackageTests.PackageRecognitionTest</c>), not the corpus census. All are
/// marked <see cref="FieldSpec.Optional"/> because that's honestly what the flawed corpus
/// will show <c>ManifestCorpusHonestyTests</c> when it runs - not because the fields are
/// actually unreliable in a real character save.
/// </para>
/// <para>
/// "EquippedItemNames" is still a fixed 5-slot array, but the slot ORDER differs from
/// IB1/IB3 - VOTE's is [Armor, Sword, Shield, Helmet, Magic], not [Sword, Shield, Armor,
/// Helmet, Magic]. Confirmed by inspecting the fixture rather than assumed from the other
/// games' layout.
/// </para>
/// </summary>
public static class VOTEManifest
{
    public static readonly GameManifest Instance = new()
    {
        Game = Game.VOTE,
        Tabs = new[]
        {
            Tab("Character",
                Section("Currency",
                    Field("CurrentGold", FieldKind.Money, "Gold")
                        .Optional(),
                    Field("TotalGoldAquired", FieldKind.Number, "Total Gold Acquired")
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
                        .Optional()
                ),
                Section("Equipment",
                    Field("EquippedItemNames[0]", FieldKind.ItemRef, "Equipped Armor")
                        .Category("armor").Optional(),
                    Field("EquippedItemNames[1]", FieldKind.ItemRef, "Equipped Weapon")
                        .Category("weapon").Optional(),
                    Field("EquippedItemNames[2]", FieldKind.ItemRef, "Equipped Shield")
                        .Category("shield").Optional(),
                    Field("EquippedItemNames[3]", FieldKind.ItemRef, "Equipped Helmet")
                        .Category("helmet").Optional(),
                    Field("EquippedItemNames[4]", FieldKind.ItemRef, "Equipped Magic")
                        .Category("magic").Optional()
                )
            ),
            Tab("Progress",
                Section("Bloodline",
                    Field("GenerationCount", FieldKind.Number, "Bloodline Generation")
                        .Optional(),
                    Field("MaxGeneration", FieldKind.Number, "Max Bloodline Generation Reached")
                        .Optional(),
                    Field("FightFinishedCount", FieldKind.Number, "Total Fights Won")
                        .Optional(),
                    Field("DefeatedByGodKing", FieldKind.Number, "Deaths To God King")
                        .Optional()
                ),
                Section("Combat Stats",
                    Field("CurrentTotalTrackingStats.TotalComboCount", FieldKind.Number, "Total Combos")
                        .Optional(),
                    Field("CurrentTotalTrackingStats.TotalGoodHits", FieldKind.Number, "Total Good Hits")
                        .Optional(),
                    Field("CurrentTotalTrackingStats.TotalBlockCount", FieldKind.Number, "Total Blocks")
                        .Optional(),
                    Field("CurrentTotalTrackingStats.TotalPerfectBlockCount", FieldKind.Number, "Total Perfect Blocks")
                        .Optional(),
                    Field("CurrentTotalTrackingStats.TotalParryCount", FieldKind.Number, "Total Parries")
                        .Optional(),
                    Field("CurrentTotalTrackingStats.TotalPerfectParryCount", FieldKind.Number, "Total Perfect Parries")
                        .Optional(),
                    Field("CurrentTotalTrackingStats.TotalDodgeCount", FieldKind.Number, "Total Dodges")
                        .Optional(),
                    Field("CurrentTotalTrackingStats.TotalStabCount", FieldKind.Number, "Total Stabs")
                        .Optional()
                )
            ),
        },
    };
}
