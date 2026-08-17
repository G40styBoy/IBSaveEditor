using Newtonsoft.Json.Linq;
using IBSaveEditor.Json;
using IBSaveEditor.Manifest;
using IBSaveEditor.Models;
using IBSaveEditor.Package;
using IBSaveEditor.Services;

namespace IBSaveEditor.Tests
{
    /// <summary>
    /// The test that keeps every game's manifest honest: for each game with a
    /// registered manifest, loads every real save in the local corpus and
    /// resolves every <see cref="FieldSpec"/>'s path against it. A path must
    /// either resolve or be marked <see cref="FieldSpec.IsOptional"/> -
    /// anything else (a typo'd path, a renamed property, a field the manifest
    /// author assumed was always present) fails here at <c>dotnet test</c>
    /// instead of showing up as a blank panel in the UI.
    /// <para>
    /// Also checks every <see cref="CollectionSpec"/>: its own array path must resolve
    /// to an array (or be marked <see cref="CollectionSpec.IsOptional"/>), and every
    /// column must resolve against the array's first element when the array is
    /// non-empty. That's a lighter check than every field gets - only the first
    /// element, not every element of every array of every save - because struct
    /// arrays here are homogeneous by construction (same UProperty layout serialized
    /// per element), so a column that resolves on element 0 resolving on element 200
    /// is a much safer assumption than "this whole save has the field" is for a
    /// scalar path across format versions.
    /// </para>
    /// <para>
    /// No-ops when the corpus (<c>SAVE STORAGE LOCATION/</c>, gitignored)
    /// isn't present, same as <see cref="SaveCensusHarness"/>. Manifests start
    /// empty in Phase 1, so this test is a green no-op today; it gains teeth
    /// as Phase 5 fills each game's manifest in.
    /// </para>
    /// <para>
    /// In the "Save fixture files" collection because <c>JsonDataParser</c> mutates
    /// <c>IBEnum</c>'s static, unsynchronized "current game" field - see the same note
    /// on <see cref="SaveCensusHarness"/>.
    /// </para>
    /// </summary>
    [Collection("Save fixture files")]
    public class ManifestCorpusHonestyTests
    {
        private static readonly string SaveStorageRoot = TestPathways.GetSaveStorageLocation();

        [Fact]
        public void EveryManifestPath_ResolvesOrIsMarkedOptional()
        {
            if (!Directory.Exists(SaveStorageRoot))
                return;

            var manifestsByGame = ManifestRegistry.All
                .Where(m => m.AllFields.Any() || m.AllCollections.Any())
                .ToDictionary(m => m.Game);

            if (manifestsByGame.Count == 0)
                return; // Nothing to check yet : Phase 1 registers only empty manifests.

            var files = Directory.EnumerateFiles(SaveStorageRoot, "*.bin", SearchOption.AllDirectories);

            var failures = new List<string>();
            var checkedFileCount = 0;

            foreach (var file in files)
            {
                UnrealPackage package;
                try
                {
                    package = new UnrealPackage(file);
                }
                catch
                {
                    continue; // Corrupt/unrecognized file : SaveCensusHarness tracks these separately.
                }

                using (package)
                {
                    if (!manifestsByGame.TryGetValue(package.info.game, out var manifest))
                        continue; // No manifest registered for this game yet.

                    List<SaveNode> root;
                    try
                    {
                        var properties = package.ReadProperties();
                        var json = new JsonDataParser(properties, package.info).ReturnDataAsString();
                        root = JsonToNodeTree.Convert(JObject.Parse(json), package.info.game);
                    }
                    catch
                    {
                        continue; // Unreadable save : not a manifest problem.
                    }

                    checkedFileCount++;
                    var rel = Path.GetRelativePath(SaveStorageRoot, file);

                    foreach (var field in manifest.AllFields)
                    {
                        if (SavePathResolver.Resolve(field.Path, root) is null && !field.IsOptional)
                            failures.Add($"[{manifest.Game}] '{field.Path}' ({field.Label}) did not resolve in '{rel}'.");
                    }

                    foreach (var collection in manifest.AllCollections)
                        CheckCollection(manifest.Game, collection, root, rel, failures);
                }
            }

            Assert.True(checkedFileCount > 0, "No saves in the corpus matched a registered manifest's game.");
            Assert.True(failures.Count == 0, $"{failures.Count} unresolved manifest path(s):\n" + string.Join('\n', failures));
        }

        private static void CheckCollection(Game game, CollectionSpec collection, List<SaveNode> root, string rel, List<string> failures)
        {
            var resolved = SavePathResolver.Resolve(collection.Path, root);
            if (resolved is not ArrayNode array)
            {
                if (!collection.IsOptional)
                    failures.Add($"[{game}] collection '{collection.Path}' ({collection.Label}) did not resolve to an array in '{rel}'.");
                return;
            }

            if (array.Items.Count == 0 || array.Items[0] is not StructNode firstElement)
                return; // Nothing to check columns against in this particular save.

            foreach (var column in collection.Columns)
            {
                if (SavePathResolver.Resolve(column.RelativePath, firstElement.Children) is null)
                    failures.Add($"[{game}] collection '{collection.Path}' column '{column.RelativePath}' ({column.Label}) did not resolve on element 0 in '{rel}'.");
            }
        }
    }
}
