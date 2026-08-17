using IBSaveEditor.ViewModels;
using IBSaveEditor.ViewModels.Fields;

namespace IBSaveEditor.Tests
{
    /// <summary>
    /// Regression test for the orphaned-tree bug Phase 6 step 3 exists to design against:
    /// <see cref="FieldViewModel"/> resolves its backing node once, in its constructor.
    /// <c>MainWindowViewModel.LoadFromJsonString</c> clears and repopulates its root
    /// <c>NodeViewModel</c> collection on every load, so if <c>ManifestTabs</c> weren't
    /// rebuilt on every load too, every field in it would keep silently pointing at the
    /// previous save's discarded <see cref="Models.SaveNode"/> instances - edits would
    /// appear to work while export wrote nothing. Nothing about that failure mode shows
    /// up in a screenshot, which is why this is a headless test rather than a manual check.
    /// <para>
    /// In the same collection as the other fixture-touching tests so none of them run
    /// concurrently against each other (shared <c>IBEnum</c> static state).
    /// </para>
    /// </summary>
    [Collection("Save fixture files")]
    public class ManifestTabsLifecycleTests
    {
        private static readonly string FILES = TestPathways.GetFileLocation();

        [Fact]
        public void ManifestTabs_RebuildsOnReload_ReflectsNewSavesValues()
        {
            var vm = new MainWindowViewModel();

            vm.LoadFromBin(Path.Combine(FILES, "Unencrypted IB3 Save.bin"));
            var tabsA = vm.ManifestTabs;
            Assert.NotNull(tabsA);
            var goldFieldA = FindGoldField(tabsA!);
            Assert.Equal(4117298L, Convert.ToInt64(goldFieldA.Value));

            vm.LoadFromBin(Path.Combine(FILES, "Unencrypted IB3 Save - 1.bin"));
            var tabsB = vm.ManifestTabs;
            Assert.NotNull(tabsB);

            // A fresh tab set was built against the new save's nodes, not the old one
            // mutated or reused in place.
            Assert.NotSame(tabsA, tabsB);

            var goldFieldB = FindGoldField(tabsB!);
            Assert.Equal(996577798L, Convert.ToInt64(goldFieldB.Value));

            // The old tab set is untouched by the reload : its field still reads save A's
            // value, proving the two ManifestTabsViewModel instances don't share mutable
            // state (which would be a different, quieter way to fail this same test).
            Assert.Equal(4117298L, Convert.ToInt64(goldFieldA.Value));
        }

        private static FieldViewModel FindGoldField(ManifestTabsViewModel tabs) =>
            tabs.Tabs
                .SelectMany(t => t.Sections)
                .SelectMany(s => s.Fields)
                .Single(f => f.Spec.Label == "Gold");
    }
}
