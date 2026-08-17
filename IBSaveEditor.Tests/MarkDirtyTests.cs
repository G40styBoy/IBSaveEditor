using IBSaveEditor.ViewModels;
using IBSaveEditor.ViewModels.Fields;

namespace IBSaveEditor.Tests
{
    /// <summary>
    /// Phase 6 step 4's gate: before this step, <c>MainWindowViewModel.MarkDirty()</c> was
    /// never called from anywhere - the "unsaved changes" indicator was decorative in both
    /// editing surfaces. Covers both write paths (a manifest-driven <see cref="FieldViewModel"/>
    /// and the raw tree's <c>NodeViewModel.EditValue</c>) reaching it, and a save clearing it.
    /// </summary>
    [Collection("Save fixture files")]
    public class MarkDirtyTests
    {
        private static readonly string FILES = TestPathways.GetFileLocation();

        [Fact]
        public void EditingThroughManifestField_MarksDirty_AndSaveClearsIt()
        {
            var vm = new MainWindowViewModel();
            vm.LoadFromBin(Path.Combine(FILES, "Unencrypted IB3 Save.bin"));
            Assert.False(vm.IsDirty);

            var goldField = vm.ManifestTabs!.Tabs
                .SelectMany(t => t.Sections)
                .SelectMany(s => s.Fields)
                .Single(f => f.Spec.Label == "Gold");

            goldField.IntEditValue = "1";
            Assert.True(vm.IsDirty);

            var jsonPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
            try
            {
                vm.SaveToPath(jsonPath);
                Assert.False(vm.IsDirty);
            }
            finally
            {
                if (File.Exists(jsonPath)) File.Delete(jsonPath);
            }
        }

        [Fact]
        public void EditingThroughRawTree_MarksDirty()
        {
            var vm = new MainWindowViewModel();
            vm.LoadFromBin(Path.Combine(FILES, "Unencrypted IB3 Save.bin"));
            Assert.False(vm.IsDirty);

            var primitiveNode = vm.AdvancedTree.VisibleNodes.First(n => n.IsPrimitive);
            primitiveNode.EditValue = primitiveNode.EditValue;

            Assert.True(vm.IsDirty);
        }
    }
}
