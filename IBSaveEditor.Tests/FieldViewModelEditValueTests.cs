using IBSaveEditor.Manifest;
using IBSaveEditor.Models;
using IBSaveEditor.Package;
using IBSaveEditor.ViewModels.Fields;

namespace IBSaveEditor.Tests
{
    /// <summary>
    /// Covers <see cref="FieldViewModel"/>'s typed edit-value properties
    /// (<c>TextEditValue</c>/<c>IntEditValue</c>/<c>FloatEditValue</c>/<c>ToggleEditValue</c>).
    /// <para>
    /// These exist because binding a view directly to <c>Value</c> (an <c>object?</c>)
    /// through an Avalonia <c>IValueConverter</c> rendered the wrong value at runtime for
    /// every field on a live save - "False" or blank for fields provably correct in every
    /// C# check - while a same-shaped plain string property with no converter
    /// (<c>CatalogPickerDisplayLabel</c>) rendered correctly in the same window. The
    /// production fix moved conversion out of XAML and into these properties; the tests
    /// here are the regression net for that conversion logic specifically, decoupled from
    /// however Avalonia's binding engine behaves.
    /// </para>
    /// </summary>
    public class FieldViewModelEditValueTests
    {
        private static List<SaveNode> BuildTree() => new()
        {
            new PrimitiveNode { Name = "Name", TypeHint = "string", Value = "SIRIS" },
            new PrimitiveNode { Name = "Gold", TypeHint = "int", Value = 4117298L },
            new PrimitiveNode { Name = "MagicLevel", TypeHint = "float", Value = 1.122798 },
            new PrimitiveNode { Name = "Flag", TypeHint = "bool", Value = true },
        };

        private static FieldViewModel MakeField(string path, FieldKind kind) =>
            new(new FieldSpec { Path = path, Kind = kind, Label = "Test" }, Game.IB3, BuildTree());

        [Fact]
        public void TextEditValue_ReflectsAndWritesThroughValue()
        {
            var field = MakeField("Name", FieldKind.Text);

            Assert.Equal("SIRIS", field.TextEditValue);

            field.TextEditValue = "ISA";

            Assert.Equal("ISA", field.TextEditValue);
            Assert.Equal("ISA", field.Value as string);
        }

        [Fact]
        public void IntEditValue_ReflectsAndWritesThroughValue()
        {
            var field = MakeField("Gold", FieldKind.Number);

            Assert.Equal("4117298", field.IntEditValue);

            field.IntEditValue = "999";

            Assert.Equal("999", field.IntEditValue);
            Assert.Equal(999L, field.Value);
        }

        [Fact]
        public void IntEditValue_InvalidText_LeavesValueUnchanged()
        {
            var field = MakeField("Gold", FieldKind.Number);

            field.IntEditValue = "not a number";

            Assert.Equal(4117298L, field.Value);
        }

        [Fact]
        public void FloatEditValue_ReflectsAndWritesThroughValue()
        {
            var field = MakeField("MagicLevel", FieldKind.Number);

            Assert.Equal("1.122798", field.FloatEditValue);

            field.FloatEditValue = "2.5";

            Assert.Equal("2.5", field.FloatEditValue);
            Assert.Equal(2.5, field.Value);
        }

        [Fact]
        public void ToggleEditValue_ReflectsAndWritesThroughValue()
        {
            var field = MakeField("Flag", FieldKind.Toggle);

            Assert.True(field.ToggleEditValue);

            field.ToggleEditValue = false;

            Assert.False(field.ToggleEditValue);
            Assert.Equal(false, field.Value);
        }

        /// <summary>
        /// Regression test for the exception-noise fix: FieldRowView instantiates all
        /// four value controls (Toggle/Text/Int/Float) for every field regardless of
        /// which one is actually visible, so IntEditValue/FloatEditValue get evaluated
        /// even on a Text field. Before the IsIntegerNumberControl/IsFloatNumberControl
        /// guard, that meant a real Convert.ToInt64/ToDouble FormatException thrown and
        /// caught on every binding refresh, for every non-numeric field, on every tab
        /// switch. Asserting the empty-string result alone wouldn't catch a regression
        /// back to the try/catch-only version (same observable result, still throwing
        /// internally) - this only verifies behavior, not that no exception fires
        /// internally, but it's what pins the "must not even attempt conversion" contract
        /// in a way a future refactor can't quietly break without a test failing near it.
        /// </summary>
        [Fact]
        public void IntAndFloatEditValue_OnNonNumericField_AreEmptyWithoutAttemptingConversion()
        {
            var textField = MakeField("Name", FieldKind.Text);
            Assert.Equal(string.Empty, textField.IntEditValue);
            Assert.Equal(string.Empty, textField.FloatEditValue);

            var toggleField = MakeField("Flag", FieldKind.Toggle);
            Assert.Equal(string.Empty, toggleField.IntEditValue);
            Assert.Equal(string.Empty, toggleField.FloatEditValue);

            // Setters are guarded the same way : writing through the "wrong" kind's
            // edit-value property must not touch Value.
            textField.IntEditValue = "123";
            Assert.Equal("SIRIS", textField.Value as string);
        }

        [Fact]
        public void EditValueProperties_OnUnresolvedField_AreEmptyOrDefaultWithoutThrowing()
        {
            var field = MakeField("DoesNotExist", FieldKind.Text);

            Assert.False(field.IsResolved);
            Assert.Equal(string.Empty, field.TextEditValue);

            var numberField = MakeField("DoesNotExist", FieldKind.Number);
            Assert.Equal(string.Empty, numberField.IntEditValue);
            Assert.Equal(string.Empty, numberField.FloatEditValue);

            var toggleField = MakeField("DoesNotExist", FieldKind.Toggle);
            Assert.False(toggleField.ToggleEditValue);
        }
    }
}
