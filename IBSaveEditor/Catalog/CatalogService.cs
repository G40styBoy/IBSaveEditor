using System.Collections.Concurrent;
using System.Reflection;
using Newtonsoft.Json.Linq;
using IBSaveEditor.Package;

namespace IBSaveEditor.Catalog;

/// <summary>
/// Loads and caches each game's item catalog from the JSONs embedded in this assembly
/// (see the <c>EmbeddedResource</c> group in IBSaveEditor.csproj) and resolves
/// internalNames against them.
/// <para>
/// Catalogs ship inside the exe rather than as loose files on disk : the whole point
/// is that a single-file publish still has item data to resolve against, with no
/// install-time asset extraction step.
/// </para>
/// <para>
/// Gem token files (<c>{game}.gems.json</c>) are intentionally NOT loaded here. They
/// aren't a name-keyed entry list - they're display-string templates that a gem's
/// display name gets built from at read time - so they don't fit the
/// internalName-&gt;<see cref="CatalogEntry"/> shape this service provides.
/// </para>
/// </summary>
public static class CatalogService
{
    private static readonly ConcurrentDictionary<Game, IReadOnlyDictionary<string, CatalogEntry>> Cache = new();

    /// <summary>All entries for a game, keyed by internalName. Loaded once per game, then cached.</summary>
    /// <exception cref="InvalidOperationException">The embedded catalog is missing or malformed.</exception>
    public static IReadOnlyDictionary<string, CatalogEntry> GetEntries(Game game) =>
        Cache.GetOrAdd(game, LoadEntries);

    public static bool TryResolve(Game game, string internalName, out CatalogEntry? entry) =>
        GetEntries(game).TryGetValue(internalName, out entry);

    private static IReadOnlyDictionary<string, CatalogEntry> LoadEntries(Game game)
    {
        var resourceName = $"Catalog/{game}.json";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded catalog resource '{resourceName}' not found. " +
                "Is it registered as an EmbeddedResource in IBSaveEditor.csproj?");

        using var reader = new StreamReader(stream);
        var root = JObject.Parse(reader.ReadToEnd());

        var items = root["items"] as JArray
            ?? throw new InvalidOperationException($"Catalog '{resourceName}' has no 'items' array.");

        var entries = new Dictionary<string, CatalogEntry>(StringComparer.Ordinal);
        foreach (var item in items.OfType<JObject>())
        {
            var entry = ParseEntry(item, resourceName);
            entries[entry.InternalName] = entry;
        }

        return entries;
    }

    private static CatalogEntry ParseEntry(JObject item, string resourceName)
    {
        var internalName = item.Value<string>("internalName");
        if (string.IsNullOrEmpty(internalName))
            throw new InvalidOperationException($"Catalog '{resourceName}' has an entry with no 'internalName'.");

        return new CatalogEntry
        {
            InternalName = internalName,
            Category     = item.Value<string>("category") ?? "",
            DisplayName  = item.Value<string?>("displayName"),
            Class        = item.Value<string?>("class"),
        };
    }
}
