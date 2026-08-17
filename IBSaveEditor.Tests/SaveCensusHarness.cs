using System.Text.Json;
using IBSaveEditor.Json;
using IBSaveEditor.Package;

namespace IBSaveEditor.Tests
{
    /// <summary>
    /// Walks a local corpus of real save files, runs each one through the full read pipeline
    /// (<see cref="UnrealPackage"/> -&gt; <see cref="UnrealPackage.ReadProperties"/> -&gt;
    /// <see cref="JsonDataParser"/>), and aggregates the resulting JSON shape per game into
    /// <c>docs/schema/{game}.census.json</c>.
    /// <para>
    /// This is a generator, not an assertion: it documents the *observed* save schema across
    /// real player data, which is useful for spotting fields the hand-written UProperty/UArray
    /// registries don't otherwise surface. The source corpus (<see cref="SaveStorageRoot"/>) is
    /// a personal save archive that lives outside version control (see "SAVE STORAGE LOCATION/"
    /// in .gitignore), so this harness quietly no-ops when it isn't present rather than failing
    /// the suite for every other contributor.
    /// </para>
    /// <para>
    /// In the "Save fixture files" collection (same as <see cref="PipelineRoundTripTests"/> et
    /// al.) not for file sharing this time, but because <c>JsonDataParser</c> calls
    /// <c>IBEnum.SetGame</c>, which mutates a static, unsynchronized "current game" field. Walking
    /// many games' worth of saves here while another test's export is mid-pipeline for a
    /// different game intermittently corrupts that export - this collection is what serializes
    /// them against each other.
    /// </para>
    /// </summary>
    [Collection("Save fixture files")]
    public class SaveCensusHarness
    {
        private static readonly string SaveStorageRoot  = TestPathways.GetSaveStorageLocation();
        private static readonly string SchemaOutputDir  = TestPathways.GetDocsSchemaDir();

        private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

        [Fact]
        public void GenerateSchemaCensus()
        {
            if (!Directory.Exists(SaveStorageRoot))
                return;

            var files = Directory.EnumerateFiles(SaveStorageRoot, "*.bin", SearchOption.AllDirectories)
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var censusByGame = new Dictionary<Game, GameCensus>();
            var unresolvedFailures = new List<string>();

            foreach (var file in files)
            {
                var rel = Path.GetRelativePath(SaveStorageRoot, file);

                UnrealPackage package;
                try
                {
                    package = new UnrealPackage(file);
                }
                catch (Exception ex)
                {
                    // Game type couldn't even be resolved, so there's no bucket to attribute this to.
                    unresolvedFailures.Add($"{rel}: {Flatten(ex.Message)}");
                    continue;
                }

                using (package)
                {
                    if (!censusByGame.TryGetValue(package.info.game, out var census))
                        censusByGame[package.info.game] = census = new GameCensus();

                    census.SourceFileCount++;

                    try
                    {
                        var properties = package.ReadProperties();
                        var json = new JsonDataParser(properties, package.info).ReturnDataAsString();

                        using var doc = JsonDocument.Parse(json);
                        var data = doc.RootElement.GetProperty("data");

                        var visited = new HashSet<string>(StringComparer.Ordinal);
                        WalkElement(data, "data", visited, census);

                        foreach (var path in visited)
                            census.GetOrAdd(path).PresentInFiles++;

                        census.SuccessfulFileCount++;
                    }
                    catch (Exception ex)
                    {
                        census.FailedFiles.Add($"{rel}: {Flatten(ex.Message)}");
                    }
                }
            }

            Directory.CreateDirectory(SchemaOutputDir);

            foreach (var (game, census) in censusByGame.OrderBy(kv => kv.Key.ToString(), StringComparer.Ordinal))
            {
                var dto = new CensusFileDto
                {
                    Game                = game.ToString(),
                    GeneratedUtc        = DateTime.UtcNow,
                    SourceFileCount     = census.SourceFileCount,
                    SuccessfulFileCount = census.SuccessfulFileCount,
                    FailedFiles         = census.FailedFiles.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList(),
                    Paths = census.PathsByKey
                        .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                        .Select(kv => ToDto(kv.Key, kv.Value, census.SuccessfulFileCount))
                        .ToList(),
                };

                var outPath = Path.Combine(SchemaOutputDir, $"{game}.census.json");
                File.WriteAllText(outPath, JsonSerializer.Serialize(dto, SerializerOptions));
            }

            if (unresolvedFailures.Count > 0)
                File.WriteAllLines(Path.Combine(SchemaOutputDir, "_unresolved.log"), unresolvedFailures);

            Assert.NotEmpty(files);
            Assert.NotEmpty(censusByGame);
        }

        #region JSON Walking

        /// <summary>
        /// Recursively records every path reached from <paramref name="element"/> into
        /// <paramref name="census"/>, and marks each one visited in <paramref name="visitedThisFile"/>
        /// so presence is counted at most once per file no matter how many times (or how deep inside
        /// an array) a path actually occurs.
        /// <para>
        /// Array indices are collapsed to a single "[]" segment: individual indices carry no schema
        /// meaning here (they vary with inventory size, item count, etc.), so per-index paths would
        /// just fragment the census instead of describing the shape of one array element.
        /// </para>
        /// </summary>
        private static void WalkElement(JsonElement element, string path, HashSet<string> visitedThisFile, GameCensus census)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    RecordVisit(path, "Object", census, visitedThisFile);
                    foreach (var prop in element.EnumerateObject())
                        WalkElement(prop.Value, $"{path}.{prop.Name}", visitedThisFile, census);
                    break;

                case JsonValueKind.Array:
                    RecordVisit(path, "Array", census, visitedThisFile);
                    var elementPath = $"{path}[]";
                    foreach (var item in element.EnumerateArray())
                        WalkElement(item, elementPath, visitedThisFile, census);
                    break;

                case JsonValueKind.String:
                    RecordVisit(path, "String", census, visitedThisFile);
                    census.GetOrAdd(path).RecordSample(element.GetString() ?? "");
                    break;

                case JsonValueKind.Number:
                    RecordVisit(path, "Number", census, visitedThisFile);
                    var stats = census.GetOrAdd(path);
                    if (element.TryGetDouble(out var number))
                        stats.RecordNumber(number);
                    stats.RecordSample(element.GetRawText());
                    break;

                case JsonValueKind.True:
                case JsonValueKind.False:
                    RecordVisit(path, "Boolean", census, visitedThisFile);
                    census.GetOrAdd(path).RecordSample(element.GetBoolean() ? "true" : "false");
                    break;

                case JsonValueKind.Null:
                    RecordVisit(path, "Null", census, visitedThisFile);
                    break;
            }
        }

        private static void RecordVisit(string path, string type, GameCensus census, HashSet<string> visitedThisFile)
        {
            census.GetOrAdd(path).Types.Add(type);
            visitedThisFile.Add(path);
        }

        private static string Flatten(string message) => message.Replace('\n', ' ').Replace('\r', ' ');

        #endregion

        #region Aggregation Model

        private sealed class GameCensus
        {
            public int SourceFileCount;
            public int SuccessfulFileCount;
            public readonly List<string> FailedFiles = new();
            public readonly Dictionary<string, PathStats> PathsByKey = new(StringComparer.Ordinal);

            public PathStats GetOrAdd(string path)
            {
                if (!PathsByKey.TryGetValue(path, out var stats))
                    PathsByKey[path] = stats = new PathStats();
                return stats;
            }
        }

        private sealed class PathStats
        {
            private const int SampleCap = 20;
            private const int SampleMaxLength = 200;
            private readonly HashSet<string> _seenSamples = new();

            public readonly SortedSet<string> Types = new(StringComparer.Ordinal);
            public int PresentInFiles;
            public double? Min;
            public double? Max;
            public readonly List<string> DistinctSamples = new();
            public bool SamplesTruncated;

            public void RecordNumber(double value)
            {
                Min = Min is null ? value : Math.Min(Min.Value, value);
                Max = Max is null ? value : Math.Max(Max.Value, value);
            }

            public void RecordSample(string sample)
            {
                if (sample.Length > SampleMaxLength)
                    sample = sample[..SampleMaxLength] + "…";

                if (!_seenSamples.Add(sample))
                    return;

                if (DistinctSamples.Count >= SampleCap)
                {
                    SamplesTruncated = true;
                    return;
                }

                DistinctSamples.Add(sample);
            }
        }

        #endregion

        #region Output DTOs

        private sealed class CensusFileDto
        {
            public string Game { get; set; } = "";
            public DateTime GeneratedUtc { get; set; }
            public int SourceFileCount { get; set; }
            public int SuccessfulFileCount { get; set; }
            public List<string> FailedFiles { get; set; } = new();
            public List<PathEntryDto> Paths { get; set; } = new();
        }

        private sealed class PathEntryDto
        {
            public string Path { get; set; } = "";
            public List<string> Types { get; set; } = new();
            public int PresentInFiles { get; set; }
            public double PresenceRate { get; set; }
            public double? Min { get; set; }
            public double? Max { get; set; }
            public List<string> DistinctSamples { get; set; } = new();
            public bool SamplesTruncated { get; set; }
        }

        private static PathEntryDto ToDto(string path, PathStats stats, int totalFiles) => new()
        {
            Path             = path,
            Types            = stats.Types.ToList(),
            PresentInFiles   = stats.PresentInFiles,
            PresenceRate     = totalFiles == 0 ? 0 : Math.Round((double)stats.PresentInFiles / totalFiles, 4),
            Min              = stats.Min,
            Max              = stats.Max,
            DistinctSamples  = stats.DistinctSamples,
            SamplesTruncated = stats.SamplesTruncated,
        };

        #endregion
    }
}
