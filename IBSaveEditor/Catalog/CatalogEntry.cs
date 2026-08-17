namespace IBSaveEditor.Catalog;

/// <summary>
/// One row of a game's item catalog (<c>Catalog/{game}.json</c>, extracted from the
/// game's own .ini data by <c>Scripts/extract_catalog.py</c>).
/// <para>
/// Deliberately minimal: only the fields every entry across all four catalogs actually
/// has are modeled here. The source JSON carries a lot more per-category data (stats,
/// visuals, gem composition, etc.) that an ItemRef/GemRef control can read directly off
/// the catalog file later if it turns out to be needed - this is just enough to resolve
/// an internalName and show something to a human picking from a list.
/// </para>
/// </summary>
public sealed record CatalogEntry
{
    public required string InternalName { get; init; }
    public required string Category     { get; init; }

    public string? DisplayName { get; init; }
    public string? Class       { get; init; }
}
