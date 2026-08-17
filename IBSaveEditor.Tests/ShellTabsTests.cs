using IBSaveEditor.ViewModels;
using IBSaveEditor.ViewModels.Fields;

namespace IBSaveEditor.Tests
{
    /// <summary>
    /// Phase 6 steps 5 and 6's gate: <c>MainWindowViewModel.ShellTabs</c> is the list the main
    /// shell's TabControl actually binds to, and <c>AdvancedTreeViewModel.IsEmpty</c> drives the
    /// "no file loaded" empty state. Both are plain view-model state, so covering them here is
    /// cheaper and more precise than a screenshot for what they specifically guarantee.
    /// </summary>
    [Collection("Save fixture files")]
    public class ShellTabsTests
    {
        private static readonly string FILES = TestPathways.GetFileLocation();

        [Fact]
        public void BeforeAnyLoad_ShellHasOnlyTheAdvancedTab_AndIsEmpty()
        {
            var vm = new MainWindowViewModel();

            Assert.Single(vm.ShellTabs);
            Assert.Same(vm.AdvancedTree, vm.ShellTabs[0]);
            Assert.Same(vm.AdvancedTree, vm.SelectedShellTab);
            Assert.True(vm.AdvancedTree.IsEmpty);
        }

        [Fact]
        public void AfterLoad_ShellListsManifestTabsThenAdvanced_SelectsFirstTab_AndClearsEmpty()
        {
            var vm = new MainWindowViewModel();
            vm.LoadFromBin(Path.Combine(FILES, "Unencrypted IB3 Save.bin"));

            // IB3Manifest ships 4 tabs (Character, Progress, Inventory, Quests) + Advanced
            Assert.Equal(5, vm.ShellTabs.Count);
            Assert.Same(vm.AdvancedTree, vm.ShellTabs[^1]);
            Assert.All(vm.ShellTabs.Take(vm.ShellTabs.Count - 1),
                tab => Assert.IsType<TabViewModel>(tab));

            Assert.Same(vm.ShellTabs[0], vm.SelectedShellTab);
            Assert.False(vm.AdvancedTree.IsEmpty);
        }
    }
}
