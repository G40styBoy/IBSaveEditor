using Newtonsoft.Json.Linq;
using IBSaveEditor.Catalog;
using IBSaveEditor.Json;
using IBSaveEditor.Manifest;
using IBSaveEditor.Package;
using IBSaveEditor.Services;
using IBSaveEditor.Util;
using IBSaveEditor.ViewModels;
using IBSaveEditor.ViewModels.Fields;

namespace IBSaveEditor.Tests
{
    /// <summary>
    /// The Phase 3 gate: load a real .bin, change a value through a manifest-driven
    /// <see cref="FieldViewModel"/> (not the raw tree editor), export back to .bin, and
    /// re-deserialize the exported file to prove the new value actually made the trip.
    /// <para>
    /// This is what "the architecture is proven" rests on: <see cref="FieldViewModel"/>
    /// writes straight into the same <c>SaveNode</c> tree <c>NodeTreeToJson</c> and the
    /// binary serializer already round-trip correctly (see
    /// <see cref="PipelineRoundTripTests"/>), so if resolving a field, editing it, and
    /// reading the exported binary back out all agree, every other manifest field and
    /// every other <see cref="FieldKind"/> control built on the same
    /// <c>FieldViewModel.Value</c> contract inherits that guarantee for free.
    /// </para>
    /// <para>
    /// In the same collection as <see cref="PipelineRoundTripTests"/> and
    /// <see cref="NodeTreeRoundTripTests"/> so none of the classes that read the shared
    /// fixture files run concurrently against each other.
    /// </para>
    /// </summary>
    [Collection("Save fixture files")]
    public class FieldViewModelRoundTripTests
    {
        private static readonly string FILES = TestPathways.GetFileLocation();

        [Theory]
        [InlineData("Unencrypted IB3 Save.bin")]
        [InlineData("Unencrypted IB3 Save - 1.bin")]
        public void SetGoldViaFieldViewModel_ExportThenReload_PersistsNewValue(string fileName)
        {
            var sourcePath = Path.Combine(FILES, fileName);

            var vm = new MainWindowViewModel();
            vm.LoadFromBin(sourcePath);
            Assert.Equal(sourcePath, vm.FilePath);
            Assert.Equal(Game.IB3, vm.CurrentGame);

            var goldSpec = IB3Manifest.Instance.AllFields.Single(f => f.Label == "Gold");
            var field    = new FieldViewModel(goldSpec, vm.CurrentGame, vm.RootNodes);
            Assert.True(field.IsResolved);

            const long newGold = 424242L;
            Assert.NotEqual(newGold, Convert.ToInt64(field.Value));
            field.Value = newGold;

            vm.ExportToBin();
            Assert.DoesNotContain("failed", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("error", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);

            var packageName = Path.GetFileNameWithoutExtension(fileName);
            var exportedPath = Path.Combine(ToolPaths.OutputDir, $"{packageName}.bin");
            Assert.True(File.Exists(exportedPath), $"Expected exported file at '{exportedPath}'.");

            using var reloadedPackage = new UnrealPackage(exportedPath);
            var reloadedProperties = reloadedPackage.ReadProperties();
            var reloadedJson = new JsonDataParser(reloadedProperties, reloadedPackage.info).ReturnDataAsString();
            var reloadedRoot = JsonToNodeTree.Convert(JObject.Parse(reloadedJson), reloadedPackage.info.game);

            var reresolvedField = new FieldViewModel(goldSpec, reloadedPackage.info.game, reloadedRoot);
            Assert.True(reresolvedField.IsResolved);
            Assert.Equal(newGold, Convert.ToInt64(reresolvedField.Value));
        }

        /// <summary>
        /// Phase 4's gate for the ItemRef control: pick a catalog candidate through
        /// <see cref="FieldViewModel.SelectCatalogPickerCandidate"/> (exactly how the picker's
        /// row click drives it), export, and confirm the written internalName - not a
        /// display label, not the candidate object - is what comes back out of the .bin.
        /// </summary>
        [Theory]
        [InlineData("Unencrypted IB3 Save.bin")]
        [InlineData("Unencrypted IB3 Save - 1.bin")]
        public void SetEquippedWeaponViaFieldViewModel_ExportThenReload_PersistsNewInternalName(string fileName)
        {
            var sourcePath = Path.Combine(FILES, fileName);

            var vm = new MainWindowViewModel();
            vm.LoadFromBin(sourcePath);
            Assert.Equal(sourcePath, vm.FilePath);
            Assert.Equal(Game.IB3, vm.CurrentGame);

            var weaponSpec = IB3Manifest.Instance.AllFields.Single(f => f.Label == "Equipped Weapon");
            var field      = new FieldViewModel(weaponSpec, vm.CurrentGame, vm.RootNodes);
            Assert.True(field.IsResolved);
            Assert.True(field.IsCatalogPickerControl);
            Assert.NotEmpty(field.CatalogPickerCandidates);

            var originalInternalName = field.Value as string;
            var newCandidate = field.CatalogPickerCandidates.First(c => c.InternalName != originalInternalName);

            field.SelectCatalogPickerCandidate(newCandidate);
            Assert.Equal(newCandidate.InternalName, field.Value as string);
            Assert.Equal(newCandidate, field.SelectedCatalogPickerCandidate);

            vm.ExportToBin();
            Assert.DoesNotContain("failed", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("error", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);

            var packageName = Path.GetFileNameWithoutExtension(fileName);
            var exportedPath = Path.Combine(ToolPaths.OutputDir, $"{packageName}.bin");
            Assert.True(File.Exists(exportedPath), $"Expected exported file at '{exportedPath}'.");

            using var reloadedPackage = new UnrealPackage(exportedPath);
            var reloadedProperties = reloadedPackage.ReadProperties();
            var reloadedJson = new JsonDataParser(reloadedProperties, reloadedPackage.info).ReturnDataAsString();
            var reloadedRoot = JsonToNodeTree.Convert(JObject.Parse(reloadedJson), reloadedPackage.info.game);

            var reresolvedField = new FieldViewModel(weaponSpec, reloadedPackage.info.game, reloadedRoot);
            Assert.True(reresolvedField.IsResolved);
            Assert.Equal(newCandidate.InternalName, reresolvedField.Value as string);
        }

        /// <summary>
        /// GemRef's gate. GemRef reuses the exact same <see cref="FieldControlKind.CatalogPicker"/>
        /// mechanism as ItemRef - only the category filter differs - so this doesn't exercise new
        /// production code, it proves that reuse is actually correct for a real gem-category field.
        /// <para>
        /// "CurrentStoreGems[0]" (the shop's currently offered gems) isn't a field that belongs in
        /// the shipped Character tab - it's rotating shop inventory, not a character stat - so this
        /// uses a standalone <see cref="FieldSpec"/> built in the test rather than adding a
        /// low-value field to <see cref="IB3Manifest"/> just to exercise the control. Phase 5's
        /// census-driven curation pass is where a real gem-related field (if any) belongs.
        /// </para>
        /// </summary>
        [Theory]
        [InlineData("Unencrypted IB3 Save.bin")]
        [InlineData("Unencrypted IB3 Save - 1.bin")]
        public void SetGemViaFieldViewModel_ExportThenReload_PersistsNewInternalName(string fileName)
        {
            var sourcePath = Path.Combine(FILES, fileName);

            var vm = new MainWindowViewModel();
            vm.LoadFromBin(sourcePath);
            Assert.Equal(sourcePath, vm.FilePath);
            Assert.Equal(Game.IB3, vm.CurrentGame);

            var gemSpec = new FieldSpec
            {
                Path            = "CurrentStoreGems[0].ini_GemName",
                Kind            = FieldKind.GemRef,
                Label           = "Test Gem Slot",
                CatalogCategory = "gem",
            };
            var field = new FieldViewModel(gemSpec, vm.CurrentGame, vm.RootNodes);
            Assert.True(field.IsResolved);
            Assert.True(field.IsCatalogPickerControl);
            Assert.NotEmpty(field.CatalogPickerCandidates);

            // The category filter actually filtered : every candidate is really a "gem", not
            // just whatever the catalog happened to contain.
            Assert.All(field.CatalogPickerCandidates, c =>
            {
                Assert.True(CatalogService.TryResolve(vm.CurrentGame, c.InternalName, out var entry));
                Assert.Equal("gem", entry!.Category);
            });

            var originalInternalName = field.Value as string;
            var newCandidate = field.CatalogPickerCandidates.First(c => c.InternalName != originalInternalName);

            field.SelectCatalogPickerCandidate(newCandidate);
            Assert.Equal(newCandidate.InternalName, field.Value as string);

            vm.ExportToBin();
            Assert.DoesNotContain("failed", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("error", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);

            var packageName = Path.GetFileNameWithoutExtension(fileName);
            var exportedPath = Path.Combine(ToolPaths.OutputDir, $"{packageName}.bin");
            Assert.True(File.Exists(exportedPath), $"Expected exported file at '{exportedPath}'.");

            using var reloadedPackage = new UnrealPackage(exportedPath);
            var reloadedProperties = reloadedPackage.ReadProperties();
            var reloadedJson = new JsonDataParser(reloadedProperties, reloadedPackage.info).ReturnDataAsString();
            var reloadedRoot = JsonToNodeTree.Convert(JObject.Parse(reloadedJson), reloadedPackage.info.game);

            var reresolvedField = new FieldViewModel(gemSpec, reloadedPackage.info.game, reloadedRoot);
            Assert.True(reresolvedField.IsResolved);
            Assert.Equal(newCandidate.InternalName, reresolvedField.Value as string);
        }

        /// <summary>
        /// EnumChoice's gate, now that it edits (see the two-tier candidate source design
        /// note on <see cref="FieldControlKind.EnumChoice"/>). Uses
        /// "GameOptions.Quests[0].ePlayerType" specifically because its EnumType
        /// (ePlayerCharacterType) has an authoritative <c>SaveEnums.cs</c> definition with
        /// a value ("EPCT_AllValid") never observed in the corpus for this field - proving
        /// the write path handles a candidate that came from the enum, not just ones the
        /// census happened to see.
        /// </summary>
        [Theory]
        [InlineData("Unencrypted IB3 Save.bin")]
        [InlineData("Unencrypted IB3 Save - 1.bin")]
        public void SetQuestPlayerTypeViaFieldViewModel_ExportThenReload_PersistsNewEnumValue(string fileName)
        {
            var sourcePath = Path.Combine(FILES, fileName);

            var vm = new MainWindowViewModel();
            vm.LoadFromBin(sourcePath);
            Assert.Equal(sourcePath, vm.FilePath);
            Assert.Equal(Game.IB3, vm.CurrentGame);

            var enumSpec = new FieldSpec
            {
                Path  = "GameOptions.Quests[0].ePlayerType",
                Kind  = FieldKind.EnumChoice,
                Label = "Test Quest Player Type",
            };
            var field = new FieldViewModel(enumSpec, vm.CurrentGame, vm.RootNodes);
            Assert.True(field.IsResolved);
            Assert.True(field.IsEnumChoiceAuthoritative);

            const string newValue = "EPCT_AllValid";
            Assert.NotEqual(newValue, field.SelectedEnumChoice);
            Assert.Contains(newValue, field.EnumChoiceCandidates);

            field.SelectedEnumChoice = newValue;
            Assert.Equal(newValue, field.SelectedEnumChoice);

            vm.ExportToBin();
            Assert.DoesNotContain("failed", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("error", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);

            var packageName = Path.GetFileNameWithoutExtension(fileName);
            var exportedPath = Path.Combine(ToolPaths.OutputDir, $"{packageName}.bin");
            Assert.True(File.Exists(exportedPath), $"Expected exported file at '{exportedPath}'.");

            using var reloadedPackage = new UnrealPackage(exportedPath);
            var reloadedProperties = reloadedPackage.ReadProperties();
            var reloadedJson = new JsonDataParser(reloadedProperties, reloadedPackage.info).ReturnDataAsString();
            var reloadedRoot = JsonToNodeTree.Convert(JObject.Parse(reloadedJson), reloadedPackage.info.game);

            var reresolvedField = new FieldViewModel(enumSpec, reloadedPackage.info.game, reloadedRoot);
            Assert.True(reresolvedField.IsResolved);
            Assert.Equal(newValue, reresolvedField.SelectedEnumChoice);
        }

        /// <summary>
        /// The collection mechanism's gate: edit one column of one row of a real
        /// <see cref="CollectionSpec"/> ("All Quests"/NumCompletions on the first quest),
        /// export, and confirm it's still there after reloading the exported .bin. Proves
        /// the whole path - CollectionSpec resolves its array, builds a row from one
        /// element's children, the column's FieldViewModel writes through exactly like a
        /// scalar field does - not just that each piece works in isolation.
        /// </summary>
        [Theory]
        [InlineData("Unencrypted IB3 Save.bin")]
        [InlineData("Unencrypted IB3 Save - 1.bin")]
        public void SetQuestCompletionsViaCollectionRow_ExportThenReload_PersistsNewValue(string fileName)
        {
            var sourcePath = Path.Combine(FILES, fileName);

            var vm = new MainWindowViewModel();
            vm.LoadFromBin(sourcePath);
            Assert.Equal(sourcePath, vm.FilePath);
            Assert.Equal(Game.IB3, vm.CurrentGame);

            var questsSpec = IB3Manifest.Instance.AllCollections.Single(c => c.Label == "All Quests");
            var collection = new CollectionViewModel(questsSpec, vm.CurrentGame, vm.RootNodes);
            Assert.NotEmpty(collection.Rows);

            var completionsField = collection.Rows[0].Columns.Single(c => c.Spec.Path == "NumCompletions");

            const long newCompletions = 777L;
            Assert.NotEqual(newCompletions, Convert.ToInt64(completionsField.Value));
            completionsField.IntEditValue = newCompletions.ToString();
            Assert.Equal(newCompletions, completionsField.Value);

            vm.ExportToBin();
            Assert.DoesNotContain("failed", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("error", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);

            var packageName = Path.GetFileNameWithoutExtension(fileName);
            var exportedPath = Path.Combine(ToolPaths.OutputDir, $"{packageName}.bin");
            Assert.True(File.Exists(exportedPath), $"Expected exported file at '{exportedPath}'.");

            using var reloadedPackage = new UnrealPackage(exportedPath);
            var reloadedProperties = reloadedPackage.ReadProperties();
            var reloadedJson = new JsonDataParser(reloadedProperties, reloadedPackage.info).ReturnDataAsString();
            var reloadedRoot = JsonToNodeTree.Convert(JObject.Parse(reloadedJson), reloadedPackage.info.game);

            var reloadedCollection = new CollectionViewModel(questsSpec, reloadedPackage.info.game, reloadedRoot);
            Assert.NotEmpty(reloadedCollection.Rows);
            var reloadedField = reloadedCollection.Rows[0].Columns.Single(c => c.Spec.Path == "NumCompletions");
            Assert.Equal(newCompletions, Convert.ToInt64(reloadedField.Value));
        }
    }
}
