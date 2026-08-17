using IBSaveEditor.Manifest;

namespace IBSaveEditor.Tests
{
    public class SavePathTests
    {
        [Fact]
        public void Parse_SimpleDottedPath_ReturnsOneNameSegmentPerPart()
        {
            var segments = SavePath.Parse("Player.Stats.Level");

            Assert.Equal(3, segments.Count);
            Assert.Equal(new SavePathSegment("Player", null), segments[0]);
            Assert.Equal(new SavePathSegment("Stats", null), segments[1]);
            Assert.Equal(new SavePathSegment("Level", null), segments[2]);
        }

        [Fact]
        public void Parse_IndexedPath_SplitsNameAndIndexIntoSeparateSegments()
        {
            var segments = SavePath.Parse("Currency[0].Current");

            Assert.Equal(3, segments.Count);
            Assert.Equal(new SavePathSegment("Currency", null), segments[0]);
            Assert.Equal(new SavePathSegment(null, 0), segments[1]);
            Assert.Equal(new SavePathSegment("Current", null), segments[2]);
        }

        [Fact]
        public void Parse_ChainedIndices_ProducesOneSegmentPerBracket()
        {
            var segments = SavePath.Parse("Grid[2][3]");

            Assert.Equal(3, segments.Count);
            Assert.Equal(new SavePathSegment("Grid", null), segments[0]);
            Assert.Equal(new SavePathSegment(null, 2), segments[1]);
            Assert.Equal(new SavePathSegment(null, 3), segments[2]);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Parse_EmptyOrWhitespace_Throws(string path) =>
            Assert.Throws<ArgumentException>(() => SavePath.Parse(path));

        [Theory]
        [InlineData("Foo[abc]")]
        [InlineData("Foo[0")]
        [InlineData("Foo]0[")]
        public void Parse_MalformedBracket_ThrowsFormatException(string path) =>
            Assert.Throws<FormatException>(() => SavePath.Parse(path));
    }
}
