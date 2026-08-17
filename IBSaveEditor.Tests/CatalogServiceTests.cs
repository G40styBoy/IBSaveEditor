using IBSaveEditor.Catalog;
using IBSaveEditor.Package;

namespace IBSaveEditor.Tests
{
    /// <summary>
    /// Fast, no-corpus-needed tests proving the embedded-resource pipeline actually
    /// works: the JSONs are found (see the EmbeddedResource wiring in
    /// IBSaveEditor.csproj), parse, and resolve known entries. The much bigger
    /// question - how much of what real saves reference actually resolves - is
    /// <see cref="CatalogCoverageTests"/>, which needs the local save corpus.
    /// </summary>
    public class CatalogServiceTests
    {
        [Theory]
        [InlineData(Game.IB1)]
        [InlineData(Game.IB2)]
        [InlineData(Game.IB3)]
        [InlineData(Game.VOTE)]
        public void GetEntries_EveryGame_LoadsFromEmbeddedResourceNonEmpty(Game game)
        {
            var entries = CatalogService.GetEntries(game);
            Assert.NotEmpty(entries);
        }

        [Fact]
        public void TryResolve_KnownIB1Item_ReturnsExpectedEntry()
        {
            var resolved = CatalogService.TryResolve(Game.IB1, "Armor_1", out var entry);

            Assert.True(resolved);
            Assert.NotNull(entry);
            Assert.Equal("Leather Armor", entry!.DisplayName);
            Assert.Equal("armor", entry.Category);
        }

        [Fact]
        public void TryResolve_UnknownName_ReturnsFalse()
        {
            var resolved = CatalogService.TryResolve(Game.IB1, "ThisIsNotARealItem_12345", out var entry);

            Assert.False(resolved);
            Assert.Null(entry);
        }

        [Fact]
        public void GetEntries_IsCaseSensitive()
        {
            // internalName is an exact Unreal FName token : "armor_1" must not
            // silently match "Armor_1", or an ItemRef could write back the wrong case.
            var resolved = CatalogService.TryResolve(Game.IB1, "armor_1", out _);
            Assert.False(resolved);
        }

        [Fact]
        public void GetEntries_ResultIsCachedAcrossCalls() =>
            Assert.Same(CatalogService.GetEntries(Game.IB3), CatalogService.GetEntries(Game.IB3));
    }
}
