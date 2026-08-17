using IBSaveEditor.Manifest;
using IBSaveEditor.Models;

namespace IBSaveEditor.Tests
{
    /// <summary>
    /// Exercises <see cref="SavePathResolver"/> against small hand-built node
    /// trees shaped like real save output (a struct with a primitive, an array
    /// of structs, nesting). <see cref="SaveCensusHarness"/> and the manifest
    /// corpus honesty test cover it against real saves; these are the fast,
    /// no-I/O tests for the resolver's actual branching logic.
    /// </summary>
    public class SavePathResolverTests
    {
        private static List<SaveNode> BuildTree() => new()
        {
            new StructNode
            {
                Name = "Player",
                Children =
                {
                    new PrimitiveNode { Name = "Level", Value = 5L },
                    new ArrayNode
                    {
                        Name = "Inventory",
                        Items =
                        {
                            new StructNode
                            {
                                Name = "[0]",
                                Children = { new PrimitiveNode { Name = "ItemId", Value = "Sword" } },
                            },
                            new StructNode
                            {
                                Name = "[1]",
                                Children = { new PrimitiveNode { Name = "ItemId", Value = "Shield" } },
                            },
                        },
                    },
                },
            },
            new ArrayNode
            {
                Name = "Currency",
                Items =
                {
                    new StructNode
                    {
                        Name = "[0]",
                        Children =
                        {
                            new PrimitiveNode { Name = "Current", Value = 100L },
                            new PrimitiveNode { Name = "Max", Value = 999L },
                        },
                    },
                },
            },
        };

        [Fact]
        public void Resolve_TopLevelStructMember_ReturnsPrimitive()
        {
            var node = SavePathResolver.Resolve("Player.Level", BuildTree());

            var primitive = Assert.IsType<PrimitiveNode>(node);
            Assert.Equal(5L, primitive.Value);
        }

        [Fact]
        public void Resolve_IndexedArrayOfStructs_ReturnsFieldInsideElement()
        {
            var node = SavePathResolver.Resolve("Currency[0].Current", BuildTree());

            var primitive = Assert.IsType<PrimitiveNode>(node);
            Assert.Equal(100L, primitive.Value);
        }

        [Fact]
        public void Resolve_NestedArrayInsideStruct_ReturnsCorrectElement()
        {
            var node = SavePathResolver.Resolve("Player.Inventory[1].ItemId", BuildTree());

            var primitive = Assert.IsType<PrimitiveNode>(node);
            Assert.Equal("Shield", primitive.Value);
        }

        [Fact]
        public void Resolve_UnknownMemberName_ReturnsNull() =>
            Assert.Null(SavePathResolver.Resolve("Player.NoSuchField", BuildTree()));

        [Fact]
        public void Resolve_IndexOutOfRange_ReturnsNull() =>
            Assert.Null(SavePathResolver.Resolve("Currency[5].Current", BuildTree()));

        [Fact]
        public void Resolve_NegativeIndex_ReturnsNull() =>
            Assert.Null(SavePathResolver.Resolve(
                new[] { new SavePathSegment("Currency", null), new SavePathSegment(null, -1) },
                BuildTree()));

        [Fact]
        public void Resolve_DrillingIntoPrimitive_ReturnsNull() =>
            Assert.Null(SavePathResolver.Resolve("Player.Level.NoSuchChild", BuildTree()));

        [Fact]
        public void Resolve_IndexWithoutPrecedingArray_ReturnsNull() =>
            Assert.Null(SavePathResolver.Resolve(
                new[] { new SavePathSegment(null, 0) },
                BuildTree()));

        [Fact]
        public void Resolve_EmptyPath_ReturnsNull() =>
            Assert.Null(SavePathResolver.Resolve(Array.Empty<SavePathSegment>(), BuildTree()));

        [Fact]
        public void Resolve_ArrayItself_ReturnsArrayNode()
        {
            var node = SavePathResolver.Resolve("Player.Inventory", BuildTree());
            Assert.IsType<ArrayNode>(node);
        }
    }
}
