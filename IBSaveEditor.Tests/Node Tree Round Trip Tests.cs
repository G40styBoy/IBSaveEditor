using Newtonsoft.Json.Linq;
using IBSaveEditor.Package;
using IBSaveEditor.Services;
using IBSaveEditor.Json;

namespace IBSaveEditor.Tests
{
    /// <summary>
    /// Verifies that <see cref="NodeTreeToJson"/> is the exact inverse of
    /// <see cref="JsonToNodeTree"/>, per the invariant documented in CLAUDE.md.
    /// Uses real save data (via the deserializer + JsonDataParser) rather than
    /// hand-built JSON so every array/struct/enum shape actually present in a
    /// save gets exercised.
    /// <para>
    /// In the same collection as <see cref="PipelineRoundTripTests"/> so both classes
    /// (which read the same fixture files) never run concurrently against each other :
    /// doing so intermittently trips a transient file-share IOException.
    /// </para>
    /// </summary>
    [Collection("Save fixture files")]
    public class NodeTreeRoundTripTests
    {
        private static readonly string FILES = TestPathways.GetFileLocation();

        public static IEnumerable<object[]> ValidFixtures => new[]
        {
            "Encrypted IB1 Save.bin",
            "Unencrypted IB1 Save.bin",
            "Encrypted IB2 Save.bin",
            "Unencrypted IB2 Save.bin",
            "Encrypted VOTE Save.bin",
            "Unencrypted VOTE Save.bin",
            "Unencrypted IB3 Save.bin",
            "Unencrypted IB3 Save - 1.bin",
        }.Select(name => new object[] { name });

        [Theory]
        [MemberData(nameof(ValidFixtures))]
        public void JsonToNodeTreeThenBack_IsLossless(string fileName)
        {
            var path = Path.Combine(FILES, fileName);

            using var package = new UnrealPackage(path);
            var properties = package.ReadProperties();
            var json = new JsonDataParser(properties, package.info).ReturnDataAsString();

            var root = JObject.Parse(json);
            var originalData = (JObject)root["data"]!;

            var nodes = JsonToNodeTree.Convert(root, package.info.game);
            var rebuiltData = NodeTreeToJson.Convert(nodes);

            Assert.True(
                JToken.DeepEquals(originalData, rebuiltData),
                $"Node tree round-trip diverged for '{fileName}'.\nOriginal: {originalData}\nRebuilt: {rebuiltData}");
        }
    }
}
