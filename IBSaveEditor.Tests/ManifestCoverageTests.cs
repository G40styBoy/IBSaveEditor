using System.Text.Json;
using System.Text.RegularExpressions;
using IBSaveEditor.Manifest;
using IBSaveEditor.Package;

namespace IBSaveEditor.Tests
{
    /// <summary>
    /// Answers "is {game}'s manifest good enough to ship" as a number instead of an
    /// impression. Reads the corpus census <see cref="SaveCensusHarness"/> already wrote
    /// (<c>docs/schema/{game}.census.json</c>, checked into git) and reports what fraction
    /// of its editable leaf paths a field in that game's manifest can actually reach,
    /// listing exactly what's missing - ordered by how often it shows up in real saves -
    /// so a thin manifest is visible in a number instead of hiding behind how many tabs
    /// happen to render.
    /// <para>
    /// Unlike <see cref="SaveCensusHarness"/> / <see cref="CatalogCoverageTests"/>, this
    /// doesn't touch the private save corpus (<c>SAVE STORAGE LOCATION/</c>) at all - it
    /// only reads the census files those harnesses already generated and committed, so it
    /// runs identically for every contributor regardless of whether they have that corpus.
    /// </para>
    /// <para>
    /// "Editable leaf path" = a census path that isn't a container (Object/Array) itself.
    /// One shape needs a special case: an <c>EnumNode</c> serializes to JSON as an object
    /// with "Enum"/"Enum Value" keys, so the census records it as an Object-typed path with
    /// two String-typed children. That whole shape counts as ONE editable leaf (an
    /// EnumChoice field edits the pair together as its EnumType/EnumValue), not as an
    /// excluded container plus two orphaned string leaves nothing could ever cover.
    /// </para>
    /// <para>
    /// This is a report, not a gate, same as <see cref="CatalogCoverageTests"/> - the
    /// census itself over- and under-counts in known ways (mixed-shape corpora across
    /// format versions, see CLAUDE.md), so the number here is a prioritization signal to
    /// set a bar against deliberately, not a hard threshold to enforce in CI.
    /// </para>
    /// </summary>
    public class ManifestCoverageTests
    {
        private static readonly string SchemaDir = TestPathways.GetDocsSchemaDir();
        private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };
        private static readonly Regex ArrayIndexPattern = new(@"\[\d+\]", RegexOptions.Compiled);

        [Fact]
        public void ReportManifestCoveragePerGame()
        {
            var reports = new List<CoverageReportDto>();

            foreach (var game in Enum.GetValues<Game>())
            {
                var censusPath = Path.Combine(SchemaDir, $"{game}.census.json");
                if (!File.Exists(censusPath))
                    continue; // No census generated for this game yet.

                var census = JsonSerializer.Deserialize<CensusFileDto>(File.ReadAllText(censusPath))!;
                var leaves = ExtractEditableLeafPaths(census);
                if (leaves.Count == 0)
                    continue;

                var reachable = CollectReachablePaths(ManifestRegistry.Get(game));

                var missing = leaves.Keys
                    .Where(p => !reachable.Contains(p))
                    .OrderByDescending(p => leaves[p])
                    .ThenBy(p => p, StringComparer.Ordinal)
                    .Select(p => new MissingPathDto { Path = p, PresenceRate = leaves[p] })
                    .ToList();

                var coveredCount = leaves.Count - missing.Count;

                reports.Add(new CoverageReportDto
                {
                    Game                  = game.ToString(),
                    GeneratedUtc          = DateTime.UtcNow,
                    EditableLeafPathCount = leaves.Count,
                    CoveredCount          = coveredCount,
                    CoveragePercent       = Percent(coveredCount, leaves.Count),
                    Missing               = missing,
                });
            }

            if (reports.Count == 0)
                return; // No census files exist yet - nothing to report against.

            Directory.CreateDirectory(SchemaDir);
            foreach (var report in reports)
            {
                var outPath = Path.Combine(SchemaDir, $"{report.Game}.manifest-coverage.json");
                File.WriteAllText(outPath, JsonSerializer.Serialize(report, SerializerOptions));
            }

            Assert.NotEmpty(reports);
        }

        #region Leaf Path Extraction

        /// <summary>
        /// Every census path that's directly editable, mapped to its presence rate. A path
        /// is a leaf when its own Types set contains neither "Object" nor "Array" (a plain
        /// scalar), or when it's the Object-shaped root of an Enum pair (see class summary) -
        /// in which case the pair's own "{path}.Enum" / "{path}.Enum Value" children are
        /// excluded so they aren't double-counted as separate, uncoverable leaves.
        /// </summary>
        private static Dictionary<string, double> ExtractEditableLeafPaths(CensusFileDto census)
        {
            var byPath = census.Paths.ToDictionary(p => p.Path, p => p, StringComparer.Ordinal);

            bool IsPureLeaf(PathEntryDto entry) =>
                !entry.Types.Contains("Object") && !entry.Types.Contains("Array");

            bool IsEnumShape(PathEntryDto entry) =>
                entry.Types.Count == 1 && entry.Types[0] == "Object"
                && byPath.TryGetValue(entry.Path + ".Enum", out var enumType) && IsPureLeaf(enumType)
                && byPath.TryGetValue(entry.Path + ".Enum Value", out var enumValue) && IsPureLeaf(enumValue);

            var enumShapePaths = census.Paths.Where(IsEnumShape).Select(p => p.Path).ToHashSet(StringComparer.Ordinal);
            var excludedChildren = enumShapePaths
                .SelectMany(p => new[] { p + ".Enum", p + ".Enum Value" })
                .ToHashSet(StringComparer.Ordinal);

            var leaves = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var entry in census.Paths)
            {
                if (excludedChildren.Contains(entry.Path))
                    continue;
                if (enumShapePaths.Contains(entry.Path) || IsPureLeaf(entry))
                    leaves[entry.Path] = entry.PresenceRate;
            }
            return leaves;
        }

        #endregion

        #region Manifest Path Collection

        /// <summary>
        /// Every save path a field or collection column in <paramref name="manifest"/> can
        /// reach, normalized into census-path shape (rooted at "data.", every array index
        /// collapsed to "[]" the same way <see cref="SaveCensusHarness"/> collapses them).
        /// </summary>
        private static HashSet<string> CollectReachablePaths(GameManifest manifest)
        {
            var reachable = new HashSet<string>(StringComparer.Ordinal);

            foreach (var field in manifest.AllFields)
                reachable.Add(ToCensusPath(field.Path));

            foreach (var collection in manifest.AllCollections)
                foreach (var column in collection.Columns)
                    reachable.Add(ToCensusColumnPath(collection.Path, column.RelativePath));

            return reachable;
        }

        private static string ToCensusPath(string rootRelativePath) =>
            "data." + ArrayIndexPattern.Replace(rootRelativePath, "[]");

        private static string ToCensusColumnPath(string collectionPath, string columnRelativePath) =>
            ToCensusPath(collectionPath) + "[]." + ArrayIndexPattern.Replace(columnRelativePath, "[]");

        #endregion

        private static double Percent(int part, int total) =>
            total == 0 ? 0 : Math.Round(100.0 * part / total, 2);

        #region DTOs

        // Matches the shape SaveCensusHarness writes - only the fields this report needs.
        private sealed class CensusFileDto
        {
            public List<PathEntryDto> Paths { get; set; } = new();
        }

        private sealed class PathEntryDto
        {
            public string Path { get; set; } = "";
            public List<string> Types { get; set; } = new();
            public double PresenceRate { get; set; }
        }

        private sealed class CoverageReportDto
        {
            public string   Game                  { get; set; } = "";
            public DateTime GeneratedUtc           { get; set; }
            public int      EditableLeafPathCount  { get; set; }
            public int      CoveredCount           { get; set; }
            public double   CoveragePercent        { get; set; }
            public List<MissingPathDto> Missing    { get; set; } = new();
        }

        private sealed class MissingPathDto
        {
            public string Path { get; set; } = "";
            public double PresenceRate { get; set; }
        }

        #endregion
    }
}
