using IBSaveEditor.Package;
using IBSaveEditor.UProperties;
using IBSaveEditor.UProperties.UArray;

namespace IBSaveEditor.Tests
{
    /// <summary>
    /// UArrayRegistry is mandatory, not a hint (see CLAUDE.md): every array a save
    /// can contain must be registered per-game, and an unregistered array must
    /// fail fast rather than silently misreading the stream.
    /// </summary>
    public class ArrayRegistryTests
    {
        [Theory]
        [InlineData(Game.IB1)]
        [InlineData(Game.IB2)]
        [InlineData(Game.IB3)]
        [InlineData(Game.VOTE)]
        public void UnknownArrayName_IsNotRegisteredForAnyGame(Game game)
        {
            Assert.False(UArrayRegistry.TryGet(game, "ThisArrayDoesNotExist", out var metadata));
            Assert.Null(metadata);
        }

        [Theory]
        [InlineData(Game.IB3)]
        [InlineData(Game.VOTE)]
        public void CommonStaticArray_IsRegisteredWithExpectedShape(Game game)
        {
            Assert.True(UArrayRegistry.TryGet(game, nameof(ArrayName.Currency), out var metadata));
            Assert.NotNull(metadata);
            Assert.Equal(ArrayType.Static, metadata!.arrayType);
            Assert.Equal(PropertyType.StructProperty, metadata.valueType);
        }

        [Theory]
        [InlineData(Game.IB3)]
        public void CommonDynamicArray_IsRegisteredWithExpectedShape(Game game)
        {
            Assert.True(UArrayRegistry.TryGet(game, nameof(ArrayName.PlayerInventory), out var metadata));
            Assert.NotNull(metadata);
            Assert.Equal(ArrayType.Dynamic, metadata!.arrayType);
            Assert.Equal(PropertyType.StructProperty, metadata.valueType);
        }

        // TouchTreasureAwards is registered per-game with a different element type :
        // IB1 stores names, everyone else stores structs. This is exactly the kind of
        // per-game override UArrayRegistry.GetAll merges in over the common table.
        [Theory]
        [InlineData(Game.IB1, PropertyType.NameProperty)]
        [InlineData(Game.IB2, PropertyType.StructProperty)]
        [InlineData(Game.IB3, PropertyType.StructProperty)]
        [InlineData(Game.VOTE, PropertyType.StructProperty)]
        public void TouchTreasureAwards_HasGameSpecificValueType(Game game, PropertyType expectedValueType)
        {
            Assert.True(UArrayRegistry.TryGet(game, nameof(ArrayName.TouchTreasureAwards), out var metadata));
            Assert.NotNull(metadata);
            Assert.Equal(expectedValueType, metadata!.valueType);
        }

        [Fact]
        public void GetAll_ReturnsEveryCommonArrayForEachGame()
        {
            foreach (Game game in Enum.GetValues<Game>())
            {
                var all = UArrayRegistry.GetAll(game);
                Assert.True(all.ContainsKey(ArrayName.TouchTreasureAwards));
            }
        }
    }
}
