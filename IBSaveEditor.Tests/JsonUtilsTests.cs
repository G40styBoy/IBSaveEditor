using IBSaveEditor.Json;
using IBSaveEditor.Package;

namespace IBSaveEditor.Tests
{
    /// <summary>
    /// JsonUtils reads/validates the "metadata" and "data" sections of the save
    /// JSON envelope. Without a valid metadata section a JSON save can't be
    /// re-serialized to .bin (see CLAUDE.md), so these failure paths matter.
    /// </summary>
    public class JsonUtilsTests : IDisposable
    {
        private readonly List<string> _tempFiles = new();

        private string WriteTempJson(string content)
        {
            var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
            File.WriteAllText(path, content);
            _tempFiles.Add(path);
            return path;
        }

        [Fact]
        public void ReadMeta_ValidEnvelope_ReturnsPopulatedRecord()
        {
            var path = WriteTempJson("""
            {
              "metadata": {
                "packageName": "MySave",
                "game": "IB3",
                "isEncrypted": true,
                "saveVersion": 5,
                "saveMagic": 541812089
              },
              "data": {}
            }
            """);

            var meta = JsonUtils.ReadMeta(path);

            Assert.Equal("MySave", meta.PackageName);
            Assert.Equal(Game.IB3, meta.Game);
            Assert.True(meta.IsEncrypted);
            Assert.Equal(5u, meta.SaveVersion);
            Assert.Equal(541812089u, meta.SaveMagic);
        }

        [Fact]
        public void ReadMeta_MissingMetadataSection_Throws()
        {
            var path = WriteTempJson("""{ "data": {} }""");
            Assert.Throws<InvalidOperationException>(() => JsonUtils.ReadMeta(path));
        }

        [Fact]
        public void ReadMeta_UnknownGameValue_Throws()
        {
            var path = WriteTempJson("""
            {
              "metadata": {
                "packageName": "MySave",
                "game": "NotAGame",
                "isEncrypted": false,
                "saveVersion": 5,
                "saveMagic": 0
              },
              "data": {}
            }
            """);

            Assert.Throws<InvalidOperationException>(() => JsonUtils.ReadMeta(path));
        }

        [Fact]
        public void ExtractDataObjectJson_ValidEnvelope_ReturnsRawDataText()
        {
            var path = WriteTempJson("""{ "metadata": {}, "data": { "Gold": 100 } }""");
            var dataJson = JsonUtils.ExtractDataObjectJson(path, "data");
            Assert.Contains("\"Gold\"", dataJson);
            Assert.Contains("100", dataJson);
        }

        [Fact]
        public void ExtractDataObjectJson_MissingObject_Throws()
        {
            var path = WriteTempJson("""{ "metadata": {} }""");
            Assert.Throws<InvalidOperationException>(() => JsonUtils.ExtractDataObjectJson(path, "data"));
        }

        [Fact]
        public void RequireFileExists_MissingFile_ThrowsFileNotFoundException()
        {
            var missing = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
            Assert.Throws<FileNotFoundException>(() => JsonUtils.RequireFileExists(missing));
        }

        [Fact]
        public void RequireString_NullOrWhitespace_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => JsonUtils.RequireString(null, "context"));
            Assert.Throws<InvalidOperationException>(() => JsonUtils.RequireString("   ", "context"));
        }

        public void Dispose()
        {
            foreach (var file in _tempFiles)
                if (File.Exists(file)) File.Delete(file);
        }
    }
}
