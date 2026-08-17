using Newtonsoft.Json.Linq;
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
    /// Covers the two add/remove models <see cref="CollectionViewModel"/> supports, matching
    /// the two shapes actually present in <see cref="IB3Manifest"/>:
    /// <para>
    /// <b>Owned-toggle</b> ("Owned Items" / PlayerInventory): every catalog item already has
    /// an array element, owned or not. Activating a hidden row sets its owned column above
    /// zero instead of inserting anything; deactivating zeroes it back out instead of
    /// deleting anything. The array's element count never changes.
    /// </para>
    /// <para>
    /// <b>Structural</b> ("Spare Gems" / PlayerUnequippedGems): a genuine variable-length
    /// array. Adding clones the last element and appends it; removing deletes an element
    /// outright. The array's element count actually changes.
    /// </para>
    /// Each gate follows the same shape as <c>FieldViewModelRoundTripTests</c>: mutate
    /// through the view model, export, re-deserialize the exported .bin, and confirm the
    /// change is really there - not just that the in-memory view model looks right.
    /// </summary>
    [Collection("Save fixture files")]
    public class CollectionAddRemoveTests
    {
        private static readonly string FILES = TestPathways.GetFileLocation();

        [Fact]
        public void ActivatingAHiddenRow_ExportThenReload_PersistsAsOwned()
        {
            const string fileName = "Unencrypted IB3 Save.bin";
            var sourcePath = Path.Combine(FILES, fileName);

            var vm = new MainWindowViewModel();
            vm.LoadFromBin(sourcePath);

            var spec = IB3Manifest.Instance.AllCollections.Single(c => c.Label == "Owned Items");
            var collection = new CollectionViewModel(spec, vm.CurrentGame, vm.RootNodes, vm.MarkDirty);

            Assert.True(collection.SupportsOwnedToggle);
            Assert.NotEmpty(collection.AddableCandidates);

            var candidate = collection.AddableCandidates[0];
            var candidateTitle = candidate.Title;
            var rowsBefore = collection.Rows.Count;

            Assert.False(vm.IsDirty);
            collection.ActivateRow(candidate);

            // Promoted into view, removed from the hidden list - the array itself didn't grow.
            Assert.Equal(rowsBefore + 1, collection.Rows.Count);
            Assert.DoesNotContain(candidate, collection.AddableCandidates);
            Assert.Contains(collection.Rows, r => r.Title == candidateTitle);
            Assert.True(vm.IsDirty);

            vm.ExportToBin();
            Assert.DoesNotContain("failed", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("error", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);

            var exportedPath = Path.Combine(ToolPaths.OutputDir, $"{Path.GetFileNameWithoutExtension(fileName)}.bin");
            using var reloadedPackage = new UnrealPackage(exportedPath);
            var reloadedJson = new JsonDataParser(reloadedPackage.ReadProperties(), reloadedPackage.info).ReturnDataAsString();
            var reloadedRoot = JsonToNodeTree.Convert(JObject.Parse(reloadedJson), reloadedPackage.info.game);

            var reloadedCollection = new CollectionViewModel(spec, reloadedPackage.info.game, reloadedRoot);
            Assert.Contains(reloadedCollection.Rows, r => r.Title == candidateTitle);
        }

        [Fact]
        public void DeactivatingAShownRow_RemovesItFromRows_WithoutDeletingTheElement()
        {
            var vm = new MainWindowViewModel();
            vm.LoadFromBin(Path.Combine(FILES, "Unencrypted IB3 Save.bin"));

            var spec = IB3Manifest.Instance.AllCollections.Single(c => c.Label == "Owned Items");
            var collection = new CollectionViewModel(spec, vm.CurrentGame, vm.RootNodes, vm.MarkDirty);

            var rowsBefore     = collection.Rows.Count;
            var addableBefore  = collection.AddableCandidates.Count;
            var row            = collection.Rows[0];
            var rowTitle       = row.Title;

            collection.DeactivateRow(row);

            Assert.Equal(rowsBefore - 1, collection.Rows.Count);
            Assert.Equal(addableBefore + 1, collection.AddableCandidates.Count);
            Assert.Contains(collection.AddableCandidates, c => c.Title == rowTitle);
            Assert.True(vm.IsDirty);
        }

        [Fact]
        public void AddingAStructuralRow_ClonesTheLastRow_ExportThenReload_PersistsAnExtraRow()
        {
            const string fileName = "Unencrypted IB3 Save.bin";
            var sourcePath = Path.Combine(FILES, fileName);

            var vm = new MainWindowViewModel();
            vm.LoadFromBin(sourcePath);

            var spec = IB3Manifest.Instance.AllCollections.Single(c => c.Label == "Spare Gems");
            var collection = new CollectionViewModel(spec, vm.CurrentGame, vm.RootNodes, vm.MarkDirty);

            Assert.True(collection.SupportsStructuralAddRemove);
            Assert.True(collection.CanAddRow);
            var rowsBefore = collection.Rows.Count;

            Assert.False(vm.IsDirty);
            collection.AddRow();

            Assert.Equal(rowsBefore + 1, collection.Rows.Count);
            Assert.True(vm.IsDirty);

            vm.ExportToBin();
            Assert.DoesNotContain("failed", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("error", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);

            var exportedPath = Path.Combine(ToolPaths.OutputDir, $"{Path.GetFileNameWithoutExtension(fileName)}.bin");
            using var reloadedPackage = new UnrealPackage(exportedPath);
            var reloadedJson = new JsonDataParser(reloadedPackage.ReadProperties(), reloadedPackage.info).ReturnDataAsString();
            var reloadedRoot = JsonToNodeTree.Convert(JObject.Parse(reloadedJson), reloadedPackage.info.game);

            var reloadedCollection = new CollectionViewModel(spec, reloadedPackage.info.game, reloadedRoot);
            Assert.Equal(rowsBefore + 1, reloadedCollection.Rows.Count);
        }

        [Fact]
        public void RemovingAStructuralRow_DeletesTheElement_ExportThenReload_PersistsOneFewerRow()
        {
            const string fileName = "Unencrypted IB3 Save.bin";
            var sourcePath = Path.Combine(FILES, fileName);

            var vm = new MainWindowViewModel();
            vm.LoadFromBin(sourcePath);

            var spec = IB3Manifest.Instance.AllCollections.Single(c => c.Label == "Spare Gems");
            var collection = new CollectionViewModel(spec, vm.CurrentGame, vm.RootNodes, vm.MarkDirty);

            var rowsBefore = collection.Rows.Count;
            Assert.True(rowsBefore > 0);

            collection.RemoveRow(collection.Rows[0]);
            Assert.Equal(rowsBefore - 1, collection.Rows.Count);
            Assert.True(vm.IsDirty);

            vm.ExportToBin();
            var exportedPath = Path.Combine(ToolPaths.OutputDir, $"{Path.GetFileNameWithoutExtension(fileName)}.bin");
            using var reloadedPackage = new UnrealPackage(exportedPath);
            var reloadedJson = new JsonDataParser(reloadedPackage.ReadProperties(), reloadedPackage.info).ReturnDataAsString();
            var reloadedRoot = JsonToNodeTree.Convert(JObject.Parse(reloadedJson), reloadedPackage.info.game);

            var reloadedCollection = new CollectionViewModel(spec, reloadedPackage.info.game, reloadedRoot);
            Assert.Equal(rowsBefore - 1, reloadedCollection.Rows.Count);
        }

        [Fact]
        public void AllQuests_SupportsNeitherAddNorRemove()
        {
            // GameOptions.Quests has no owned filter and isn't opted into
            // IsStructurallyEditable - a fixed roster, deliberately not add/removable.
            var vm = new MainWindowViewModel();
            vm.LoadFromBin(Path.Combine(FILES, "Unencrypted IB3 Save.bin"));

            var spec = IB3Manifest.Instance.AllCollections.Single(c => c.Label == "All Quests");
            var collection = new CollectionViewModel(spec, vm.CurrentGame, vm.RootNodes);

            Assert.False(collection.SupportsOwnedToggle);
            Assert.False(collection.SupportsStructuralAddRemove);
            Assert.All(collection.Rows, r => Assert.False(r.CanBeRemoved));

            var rowsBefore = collection.Rows.Count;
            collection.AddRow();
            collection.RemoveRow(collection.Rows[0]);
            Assert.Equal(rowsBefore, collection.Rows.Count);
        }
    }
}
