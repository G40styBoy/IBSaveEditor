using IBSaveEditor.Json;
using IBSaveEditor.Manifest;
using IBSaveEditor.Package;
using IBSaveEditor.Services;
using IBSaveEditor.ViewModels.Fields;
using Newtonsoft.Json.Linq;

namespace IBSaveEditor.Tests
{
    /// <summary>
    /// EnumChoice's two-tier candidate source (see the design note on
    /// <see cref="FieldControlKind.EnumChoice"/>): an authoritative C# enum when one
    /// matches by name, corpus-observed values otherwise. The full set-export-reload
    /// proof lives in <see cref="FieldViewModelRoundTripTests"/> alongside every other
    /// kind's round-trip test ; these are the faster, no-export checks specific to
    /// EnumChoice's candidate-list logic.
    /// </summary>
    [Collection("Save fixture files")]
    public class EnumChoiceFieldTests
    {
        private static readonly string FILES = TestPathways.GetFileLocation();

        [Theory]
        [InlineData("Unencrypted IB3 Save.bin")]
        [InlineData("Unencrypted IB3 Save - 1.bin")]
        public void EnumChoiceField_ResolvesAndDisplaysCurrentValue(string fileName)
        {
            var sourcePath = Path.Combine(FILES, fileName);

            using var package = new UnrealPackage(sourcePath);
            var properties = package.ReadProperties();
            var json = new JsonDataParser(properties, package.info).ReturnDataAsString();
            var root = JsonToNodeTree.Convert(JObject.Parse(json), package.info.game);

            var spec = new FieldSpec
            {
                Path  = "PlayerInventory[0].eItemState",
                Kind  = FieldKind.EnumChoice,
                Label = "Test Item State",
            };
            var field = new FieldViewModel(spec, package.info.game, root);

            Assert.True(field.IsResolved);
            Assert.True(field.IsEnumChoiceControl);
            Assert.Equal("SIS_ItemNormal", field.EnumChoiceDisplayValue);
            Assert.Equal("SIS_ItemNormal", field.SelectedEnumChoice);
        }

        [Fact]
        public void EnumChoiceField_WithAuthoritativeEnum_UsesFullCandidateListNotJustObservedValues()
        {
            var sourcePath = Path.Combine(FILES, "Unencrypted IB3 Save.bin");

            using var package = new UnrealPackage(sourcePath);
            var properties = package.ReadProperties();
            var json = new JsonDataParser(properties, package.info).ReturnDataAsString();
            var root = JsonToNodeTree.Convert(JObject.Parse(json), package.info.game);

            var spec = new FieldSpec
            {
                Path  = "GameOptions.Quests[0].ePlayerType",
                Kind  = FieldKind.EnumChoice,
                Label = "Test Quest Player Type",
            };
            var field = new FieldViewModel(spec, package.info.game, root);

            Assert.True(field.IsResolved);
            Assert.True(field.IsEnumChoiceAuthoritative);
            Assert.False(field.ShowsUnverifiedEnumChoiceWarning);
            // ePlayerCharacterType has 3 real values (Siris/Isa/AllValid) - the corpus
            // only ever shows Siris/Isa for this specific field, so a candidate list
            // limited to "observed values" would silently hide AllValid.
            Assert.Equal(new[] { "EPCT_AllValid", "EPCT_Isa", "EPCT_Siris" }, field.EnumChoiceCandidates.OrderBy(v => v));
        }

        [Fact]
        public void EnumChoiceField_WithoutAuthoritativeEnum_FallsBackToObservedValuesAndWarns()
        {
            var sourcePath = Path.Combine(FILES, "Unencrypted IB3 Save.bin");

            using var package = new UnrealPackage(sourcePath);
            var properties = package.ReadProperties();
            var json = new JsonDataParser(properties, package.info).ReturnDataAsString();
            var root = JsonToNodeTree.Convert(JObject.Parse(json), package.info.game);

            var spec = new FieldSpec
            {
                Path  = "PlayerInventory[0].eItemState", // SwordItemState : no authoritative enum
                Kind  = FieldKind.EnumChoice,
                Label = "Test Item State",
            };
            var field = new FieldViewModel(spec, package.info.game, root);

            Assert.False(field.IsEnumChoiceAuthoritative);
            Assert.True(field.ShowsUnverifiedEnumChoiceWarning);
            Assert.Contains("SIS_ItemNormal", field.EnumChoiceCandidates);
        }

        [Fact]
        public void EnumChoiceField_ValueSetterIsANoOp()
        {
            var sourcePath = Path.Combine(FILES, "Unencrypted IB3 Save.bin");

            using var package = new UnrealPackage(sourcePath);
            var properties = package.ReadProperties();
            var json = new JsonDataParser(properties, package.info).ReturnDataAsString();
            var root = JsonToNodeTree.Convert(JObject.Parse(json), package.info.game);

            var spec = new FieldSpec
            {
                Path  = "PlayerInventory[0].eItemState",
                Kind  = FieldKind.EnumChoice,
                Label = "Test Item State",
            };
            var field = new FieldViewModel(spec, package.info.game, root);

            // EnumChoice writes through SelectedEnumChoice, backed by EnumNode, not
            // Value (backed by PrimitiveNode) - setting the wrong one must not throw
            // or corrupt the node.
            field.Value = "SomeUnvalidatedString";

            Assert.Equal("SIS_ItemNormal", field.EnumChoiceDisplayValue);
        }
    }
}
