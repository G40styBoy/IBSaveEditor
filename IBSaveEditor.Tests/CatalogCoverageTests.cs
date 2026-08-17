using System.Text.Json;
using Newtonsoft.Json.Linq;
using IBSaveEditor.Catalog;
using IBSaveEditor.Json;
using IBSaveEditor.Models;
using IBSaveEditor.Package;
using IBSaveEditor.Services;

namespace IBSaveEditor.Tests
{
    /// <summary>
    /// Walks the local save corpus, collects every FName-typed value each game's saves
    /// actually reference (the same "ini_" prefix / registry item-type signal
    /// <c>NodeViewModel.IsIni</c> uses to badge these in the UI), and reports what
    /// fraction resolve against that game's <see cref="CatalogService"/> catalog.
    /// <para>
    /// This is a report, not a gate: an ItemRef/GemRef control needs to know the size
    /// of the gap before depending on catalog lookups, but 100% isn't the expectation.
    /// FName values in these saves aren't all item references - boss spawn-point names
    /// and similar engine/actor names use the same property type and show up in the
    /// same collection - so some amount of unresolved noise is normal, not a catalog
    /// bug. <c>docs/schema/{game}.catalog-coverage.json</c> lists every unresolved
    /// value so a human can tell which case is which.
    /// </para>
    /// <para>
    /// No-ops when the corpus (<c>SAVE STORAGE LOCATION/</c>, gitignored) isn't present,
    /// same as <see cref="SaveCensusHarness"/>.
    /// </para>
    /// <para>
    /// In the "Save fixture files" collection because <c>JsonDataParser</c> mutates
    /// <c>IBEnum</c>'s static, unsynchronized "current game" field - see the same note
    /// on <see cref="SaveCensusHarness"/>.
    /// </para>
    /// </summary>
    [Collection("Save fixture files")]
    public class CatalogCoverageTests
    {
        private const string UnsetFNameSentinel = "None";

        private static readonly string SaveStorageRoot = TestPathways.GetSaveStorageLocation();
        private static readonly string ReportDir        = TestPathways.GetDocsSchemaDir();
        private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

        [Fact]
        public void ReportCatalogCoveragePerGame()
        {
            if (!Directory.Exists(SaveStorageRoot))
                return;

            var files = Directory.EnumerateFiles(SaveStorageRoot, "*.bin", SearchOption.AllDirectories);

            // Per game: distinct FName-reference value -> how many times it occurred across the corpus.
            var referencesByGame = new Dictionary<Game, Dictionary<string, int>>();
            var scannedByGame    = new Dictionary<Game, int>();

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
                    List<SaveNode> root;
                    try
                    {
                        var properties = package.ReadProperties();
                        var json = new JsonDataParser(properties, package.info).ReturnDataAsString();
                        root = JsonToNodeTree.Convert(JObject.Parse(json), package.info.game);
                    }
                    catch
                    {
                        continue; // Unreadable save : not a catalog problem.
                    }

                    var game = package.info.game;
                    if (!referencesByGame.TryGetValue(game, out var refs))
                        referencesByGame[game] = refs = new Dictionary<string, int>(StringComparer.Ordinal);

                    scannedByGame[game] = scannedByGame.GetValueOrDefault(game) + 1;
                    CollectNameReferences(root, refs);
                }
            }

            if (referencesByGame.Count == 0)
                return;

            Directory.CreateDirectory(ReportDir);

            foreach (var (game, refs) in referencesByGame.OrderBy(kv => kv.Key.ToString(), StringComparer.Ordinal))
            {
                var catalog = CatalogService.GetEntries(game);

                var distinctResolved   = refs.Keys.Count(catalog.ContainsKey);
                var occurrenceTotal    = refs.Values.Sum();
                var occurrenceResolved = refs.Where(kv => catalog.ContainsKey(kv.Key)).Sum(kv => kv.Value);

                var dto = new CoverageReportDto
                {
                    Game                       = game.ToString(),
                    GeneratedUtc               = DateTime.UtcNow,
                    SavesScanned               = scannedByGame[game],
                    DistinctReferences         = refs.Count,
                    DistinctResolved           = distinctResolved,
                    DistinctCoveragePercent    = Percent(distinctResolved, refs.Count),
                    TotalOccurrences           = occurrenceTotal,
                    ResolvedOccurrences        = occurrenceResolved,
                    OccurrenceCoveragePercent  = Percent(occurrenceResolved, occurrenceTotal),
                    Unresolved = refs.Keys
                        .Where(k => !catalog.ContainsKey(k))
                        .OrderBy(k => k, StringComparer.Ordinal)
                        .ToList(),
                };

                var outPath = Path.Combine(ReportDir, $"{game}.catalog-coverage.json");
                File.WriteAllText(outPath, JsonSerializer.Serialize(dto, SerializerOptions));
            }

            Assert.NotEmpty(referencesByGame);
        }

        #region Reference Collection

        /// <summary>
        /// Walks a save's node tree collecting every FName-typed value: scalar/struct
        /// fields whose JSON key carries the "ini_" prefix, and every item of an array
        /// registered as holding name-typed elements (those items are plain strings by
        /// the time they reach the node tree - the array container is the only place
        /// that knows they're names, see <c>ArrayNode.ItemTypeHint</c>).
        /// </summary>
        private static void CollectNameReferences(IEnumerable<SaveNode> nodes, Dictionary<string, int> counts)
        {
            foreach (var node in nodes)
            {
                switch (node)
                {
                    case StructNode s:
                        CollectNameReferences(s.Children, counts);
                        break;

                    case ArrayNode a when a.ItemTypeHint == "name":
                        foreach (var item in a.Items)
                            if (item is PrimitiveNode { Value: string value })
                                Record(value, counts);
                        break;

                    case ArrayNode a:
                        CollectNameReferences(a.Items, counts);
                        break;

                    case PrimitiveNode { Value: string value } when node.Name.StartsWith("ini_", StringComparison.OrdinalIgnoreCase):
                        Record(value, counts);
                        break;
                }
            }
        }

        private static void Record(string value, Dictionary<string, int> counts)
        {
            if (string.IsNullOrEmpty(value) || value.Equals(UnsetFNameSentinel, StringComparison.Ordinal))
                return; // "None" is Unreal's unset-FName sentinel, not a reference.

            counts[value] = counts.GetValueOrDefault(value) + 1;
        }

        private static double Percent(int part, int total) =>
            total == 0 ? 0 : Math.Round(100.0 * part / total, 2);

        #endregion

        private sealed class CoverageReportDto
        {
            public string   Game                      { get; set; } = "";
            public DateTime GeneratedUtc               { get; set; }
            public int      SavesScanned               { get; set; }
            public int      DistinctReferences         { get; set; }
            public int      DistinctResolved           { get; set; }
            public double   DistinctCoveragePercent    { get; set; }
            public int      TotalOccurrences           { get; set; }
            public int      ResolvedOccurrences        { get; set; }
            public double   OccurrenceCoveragePercent  { get; set; }
            public List<string> Unresolved             { get; set; } = new();
        }
    }
}
