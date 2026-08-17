using System.Text.Json;
using Newtonsoft.Json.Linq;
using IBSaveEditor.Json;
using IBSaveEditor.Models;
using IBSaveEditor.Package;
using IBSaveEditor.Services;

namespace IBSaveEditor.Tests
{
    /// <summary>
    /// Walks the local save corpus collecting every <see cref="EnumNode"/>, grouped by
    /// <see cref="EnumNode.EnumType"/>, into <c>docs/schema/{game}.enum-census.json</c>.
    /// <para>
    /// This is the investigation <c>FieldControlKind.EnumChoice</c>'s design note says has
    /// to happen before that control can become editable: for each EnumType, how many
    /// distinct values does the corpus actually contain, and at which save paths does the
    /// type show up. A small, closed set of types with few values each means EnumChoice is
    /// a short follow-up; a large one means picking a choice-list source (observed values
    /// vs. <c>Enum/SaveEnums.cs</c> vs. <c>Catalog/src/{game}/*.ini</c>) needs its own pass.
    /// This harness answers "how big is that problem," it doesn't solve it.
    /// </para>
    /// <para>
    /// No-ops when the corpus (<c>SAVE STORAGE LOCATION/</c>, gitignored) isn't present,
    /// same as <see cref="SaveCensusHarness"/>. In the "Save fixture files" collection for
    /// the same <c>IBEnum</c> static-state reason documented there.
    /// </para>
    /// </summary>
    [Collection("Save fixture files")]
    public class EnumCensusHarness
    {
        private static readonly string SaveStorageRoot = TestPathways.GetSaveStorageLocation();
        private static readonly string ReportDir        = TestPathways.GetDocsSchemaDir();
        private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

        [Fact]
        public void ReportEnumTypesPerGame()
        {
            if (!Directory.Exists(SaveStorageRoot))
                return;

            var files = Directory.EnumerateFiles(SaveStorageRoot, "*.bin", SearchOption.AllDirectories);

            var statsByGame   = new Dictionary<Game, Dictionary<string, EnumTypeStats>>();
            var scannedByGame = new Dictionary<Game, int>();

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
                        continue; // Unreadable save : not an enum-census problem.
                    }

                    var game = package.info.game;
                    if (!statsByGame.TryGetValue(game, out var stats))
                        statsByGame[game] = stats = new Dictionary<string, EnumTypeStats>(StringComparer.Ordinal);

                    scannedByGame[game] = scannedByGame.GetValueOrDefault(game) + 1;
                    CollectEnumNodes(root, "data", stats);
                }
            }

            if (statsByGame.Count == 0)
                return;

            Directory.CreateDirectory(ReportDir);

            foreach (var (game, stats) in statsByGame.OrderBy(kv => kv.Key.ToString(), StringComparer.Ordinal))
            {
                var dto = new EnumCensusReportDto
                {
                    Game         = game.ToString(),
                    GeneratedUtc = DateTime.UtcNow,
                    SavesScanned = scannedByGame[game],
                    EnumTypes = stats
                        .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                        .Select(kv => new EnumTypeDto
                        {
                            EnumType         = kv.Key,
                            Occurrences      = kv.Value.Occurrences,
                            DistinctValueCount = kv.Value.Values.Count,
                            DistinctValues   = kv.Value.Values.OrderBy(v => v, StringComparer.Ordinal).ToList(),
                            Paths            = kv.Value.Paths.OrderBy(p => p, StringComparer.Ordinal).ToList(),
                        })
                        .ToList(),
                };

                var outPath = Path.Combine(ReportDir, $"{game}.enum-census.json");
                File.WriteAllText(outPath, JsonSerializer.Serialize(dto, SerializerOptions));
            }

            Assert.NotEmpty(statsByGame);
        }

        #region Enum Collection

        private sealed class EnumTypeStats
        {
            public int Occurrences;
            public readonly HashSet<string> Values = new(StringComparer.Ordinal);
            public readonly HashSet<string> Paths  = new(StringComparer.Ordinal);
        }

        /// <summary>
        /// Walks a save's node tree collecting every EnumNode's (EnumType, EnumValue),
        /// grouped by EnumType. Array indices collapse to "[]" in the recorded path for
        /// the same reason <see cref="SaveCensusHarness"/> does it: the index carries no
        /// schema meaning, and per-index paths would just fragment the report.
        /// <para>
        /// Array items get their own visit path (<c>"{arrayPath}[]"</c>, not
        /// <c>"{arrayPath}[].{item.Name}"</c>) because an item's <c>Name</c> is just its
        /// positional placeholder ("[0]", "[1]", ...), not a real path segment.
        /// </para>
        /// </summary>
        private static void CollectEnumNodes(IEnumerable<SaveNode> nodes, string parentPath, Dictionary<string, EnumTypeStats> stats)
        {
            foreach (var node in nodes)
                Visit(node, $"{parentPath}.{node.Name}", stats);
        }

        private static void Visit(SaveNode node, string path, Dictionary<string, EnumTypeStats> stats)
        {
            switch (node)
            {
                case EnumNode e:
                    if (!stats.TryGetValue(e.EnumType, out var typeStats))
                        stats[e.EnumType] = typeStats = new EnumTypeStats();

                    typeStats.Occurrences++;
                    typeStats.Values.Add(e.EnumValue);
                    typeStats.Paths.Add(path);
                    break;

                case StructNode s:
                    CollectEnumNodes(s.Children, path, stats);
                    break;

                case ArrayNode a:
                    var itemPath = $"{path}[]";
                    foreach (var item in a.Items)
                        Visit(item, itemPath, stats);
                    break;
            }
        }

        #endregion

        private sealed class EnumCensusReportDto
        {
            public string   Game         { get; set; } = "";
            public DateTime GeneratedUtc { get; set; }
            public int      SavesScanned { get; set; }
            public List<EnumTypeDto> EnumTypes { get; set; } = new();
        }

        private sealed class EnumTypeDto
        {
            public string EnumType             { get; set; } = "";
            public int    Occurrences          { get; set; }
            public int    DistinctValueCount   { get; set; }
            public List<string> DistinctValues { get; set; } = new();
            public List<string> Paths          { get; set; } = new();
        }
    }
}
