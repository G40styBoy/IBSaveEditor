using IBSaveEditor.Package;
using static IBSaveEditor.Manifest.ManifestBuilder;

namespace IBSaveEditor.Manifest;

/// <summary>
/// IB1's manifest. Same corpus-driven process as IB3, but the corpus here is tiny
/// (3 local saves - see <c>docs/schema/IB1.census.json</c>), so 100% presence carries
/// much less statistical weight than it does for IB3's 25-save corpus. Treat it as
/// "observed every time so far", not "guaranteed always present".
/// <para>
/// IB1's save shape is flatter than IB3's : no Currency struct wrapper (gold is a bare
/// top-level "CurrentGold"), and there's no character-name field in the save at all -
/// this manifest has no Identity section because there's nothing to put in one.
/// </para>
/// <para>
/// "EquippedItemNames" is the same fixed 5-slot [Sword, Shield, Armor, Helmet, Magic]
/// layout as IB3 - confirmed the same way, by inspecting two real IB1 saves rather
/// than assuming the layout carried over between games.
/// </para>
/// </summary>
public static class IB1Manifest
{
    public static readonly GameManifest Instance = new()
    {
        Game = Game.IB1,
        Tabs = new[]
        {
            Tab("Character",
                Section("Currency",
                    Field("CurrentGold", FieldKind.Money, "Gold"),
                    Field("TotalGoldAquired", FieldKind.Number, "Total Gold Acquired")
                        .Describe("Lifetime gold earned, including gold already spent."),
                    Field("TotalGoldFoundInWorld", FieldKind.Number, "Gold Found In World"),
                    Field("TotalGoldFromBattle", FieldKind.Number, "Gold Earned From Battle")
                ),
                Section("Stats",
                    Field("Health", FieldKind.Number, "Health"),
                    Field("PawnLevel", FieldKind.Number, "Level"),
                    Field("WorldLevel", FieldKind.Number, "World Level"),
                    Field("CurrentXP", FieldKind.Number, "Experience"),
                    Field("CurrentMagicLevel", FieldKind.Number, "Magic Level")
                        .Describe("Magic power multiplier, not a level count."),
                    Field("CurrentSuperLevel", FieldKind.Number, "Super Move Level")
                        .Describe("Super attack charge multiplier, not a level count.")
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
                )
            ),
            Tab("Progress",
                Section("Bloodline",
                    Field("GenerationCount", FieldKind.Number, "Bloodline Generation")
                        .Describe("Number of times this bloodline has been reborn."),
                    Field("MaxGeneration", FieldKind.Number, "Max Bloodline Generation Reached"),
                    Field("FightFinishedCount", FieldKind.Number, "Total Fights Won"),
                    Field("PlayThroughFightFinishedCount", FieldKind.Number, "Fights Won This Playthrough")
                ),
                Section("God King",
                    Field("GodKingDefeated", FieldKind.Number, "God King Defeats")
                        .Describe("Times the God King has been defeated."),
                    Field("DefeatedByGodKing", FieldKind.Number, "Deaths To God King"),
                    Field("MaxGodKingDefeatedLevel", FieldKind.Number, "Max God King Level Defeated")
                ),
                Section("World & Economy",
                    Field("TotalTreasureChest", FieldKind.Number, "Treasure Chests Found"),
                    Field("CurrentHealthPotions", FieldKind.Number, "Health Potions")
                )
            ),
        },
    };
}
