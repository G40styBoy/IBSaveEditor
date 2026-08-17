using IBSaveEditor.Package;

namespace IBSaveEditor.Manifest;

/// <summary>
/// One editable field: a save path, the widget it renders as, and its display
/// metadata. Built with <see cref="ManifestBuilder.Field"/> and the fluent
/// <c>.Describe()</c> / <c>.Range()</c> / <c>.Optional()</c> modifiers, e.g.
/// <code>
/// Field("Currency[0].Current", FieldKind.Money, "Gold")
///     .Describe("Spendable gold.")
///     .Range(0, 999_999_999)
/// </code>
/// </summary>
public sealed record FieldSpec
{
    public required string    Path  { get; init; }
    public required FieldKind Kind  { get; init; }
    public required string    Label { get; init; }

    public string? Description { get; init; }
    public double? Min         { get; init; }
    public double? Max         { get; init; }

    /// <summary>
    /// When true, the corpus honesty test tolerates this path not resolving on
    /// some saves (e.g. a field that only exists in late-game saves) instead
    /// of failing. Unset fields are expected to resolve on every save.
    /// </summary>
    public bool IsOptional { get; init; }

    /// <summary>
    /// Restricts an <see cref="FieldKind.ItemRef"/>/<see cref="FieldKind.GemRef"/> picker's
    /// candidate list to catalog entries of this <see cref="IBSaveEditor.Catalog.CatalogEntry.Category"/>
    /// (e.g. "weapon"). Unset shows every entry in the game's catalog.
    /// </summary>
    public string? CatalogCategory { get; init; }

    public FieldSpec Describe(string description) => this with { Description = description };
    public FieldSpec Range(double min, double max) => this with { Min = min, Max = max };
    public FieldSpec Optional()                    => this with { IsOptional = true };
    public FieldSpec Category(string catalogCategory) => this with { CatalogCategory = catalogCategory };
}

/// <summary>A labeled group of fields shown together within a <see cref="TabSpec"/>.</summary>
public sealed record SectionSpec
{
    public required string                     Title  { get; init; }
    public required IReadOnlyList<FieldSpec>   Fields { get; init; }
}

/// <summary>
/// One column of a <see cref="CollectionSpec"/> row: a <see cref="FieldKind"/>-driven
/// control, same as <see cref="FieldSpec"/>, but resolved against a single array
/// element instead of the save root - see <see cref="CollectionSpec"/>.
/// </summary>
public sealed record CollectionColumnSpec
{
    public required string    RelativePath { get; init; }
    public required FieldKind Kind         { get; init; }
    public required string    Label        { get; init; }

    public string? CatalogCategory { get; init; }

    public CollectionColumnSpec Category(string catalogCategory) => this with { CatalogCategory = catalogCategory };
}

/// <summary>
/// A variable-length list of save-array elements, rendered as one repeating row per
/// element. Each <see cref="CollectionColumnSpec"/> reuses the exact same
/// Number/Toggle/Text/ItemRef/GemRef/EnumChoice controls scalar fields do - a column
/// is just a field whose path is relative to one array element rather than the save
/// root, so nothing about the controls themselves needed to change to support this.
/// <para>
/// Deliberately one level deep: a column's path can't reach into a nested array
/// (e.g. an inventory item's own socketed gems) - that's a real gap, not an oversight,
/// left for whenever a second level actually earns its own design pass.
/// </para>
/// </summary>
public sealed record CollectionSpec
{
    public required string Path    { get; init; }
    public required string Label   { get; init; }
    public required IReadOnlyList<CollectionColumnSpec> Columns { get; init; }

    /// <summary>Which column's display value becomes each row's title. Falls back to a positional "#N" when unset.</summary>
    public string? TitleColumnPath { get; init; }

    /// <summary>Same meaning as <see cref="FieldSpec.IsOptional"/>, for the collection's own array path.</summary>
    public bool IsOptional { get; init; }

    /// <summary>
    /// When set, only rows whose named column resolves to a number greater than zero
    /// are shown - e.g. an inventory tracked per-catalog-item filtered down to what's
    /// actually owned. Kept as a plain column-name string rather than an embedded
    /// predicate so <see cref="CollectionSpec"/> stays a pure data record.
    /// <para>
    /// Also selects the UI's add/remove model: a collection with this set gets the
    /// "owned-toggle" model (picking a hidden entry sets its own column above zero;
    /// nothing is ever inserted or deleted, since the array already has one element per
    /// possible entry) - see <see cref="IsStructurallyEditable"/> for the other model.
    /// </para>
    /// </summary>
    public string? OwnedOnlyColumnPath { get; init; }

    /// <summary>
    /// Opts a genuine variable-length array into structural add/remove (insert a new
    /// element cloned from the last one; delete an element outright). Defaults false
    /// deliberately: most collections without <see cref="OwnedOnlyColumnPath"/> set (e.g.
    /// IB3's "All Quests") are a fixed, complete roster the game expects to always be
    /// there in full, not something a player should be able to insert into or delete
    /// from. Whether a given array is actually safe to grow or shrink is a per-collection
    /// judgment call, not something the shape of the array alone can answer - see
    /// <see cref="AddRemovable"/>.
    /// </summary>
    public bool IsStructurallyEditable { get; init; }

    public CollectionSpec Title(string relativeColumnPath) => this with { TitleColumnPath = relativeColumnPath };
    public CollectionSpec Optional() => this with { IsOptional = true };
    public CollectionSpec WhereOwned(string relativeColumnPath) => this with { OwnedOnlyColumnPath = relativeColumnPath };
    public CollectionSpec AddRemovable() => this with { IsStructurallyEditable = true };
}

/// <summary>One tab in the editor shell, made up of one or more sections and, optionally, collections.</summary>
public sealed record TabSpec
{
    public required string                       Title    { get; init; }
    public required IReadOnlyList<SectionSpec>   Sections { get; init; }

    public IReadOnlyList<CollectionSpec> Collections { get; init; } = Array.Empty<CollectionSpec>();

    public TabSpec WithCollections(params CollectionSpec[] collections) => this with { Collections = collections };
}

/// <summary>
/// The full set of editable tabs for one game. Manifests are hand-written C#
/// (see <see cref="ManifestBuilder"/>), not a data file: field paths are
/// checked against real save shapes at <c>dotnet test</c> time, not at
/// runtime, and there's no schema to keep in sync with the field list.
/// </summary>
public sealed record GameManifest
{
    public required Game                     Game { get; init; }
    public required IReadOnlyList<TabSpec>   Tabs { get; init; }

    public IEnumerable<FieldSpec> AllFields =>
        Tabs.SelectMany(t => t.Sections).SelectMany(s => s.Fields);

    public IEnumerable<CollectionSpec> AllCollections =>
        Tabs.SelectMany(t => t.Collections);
}

/// <summary>
/// Declarative factory functions for building a <see cref="GameManifest"/>.
/// Intended to be brought in with <c>using static IBSaveEditor.Manifest.ManifestBuilder;</c>
/// so manifest files read as plain <c>Tab(...) { Section(...) { Field(...) } }</c> trees.
/// </summary>
public static class ManifestBuilder
{
    public static FieldSpec Field(string path, FieldKind kind, string label) =>
        new() { Path = path, Kind = kind, Label = label };

    public static SectionSpec Section(string title, params FieldSpec[] fields) =>
        new() { Title = title, Fields = fields };

    public static CollectionColumnSpec Column(string relativePath, FieldKind kind, string label) =>
        new() { RelativePath = relativePath, Kind = kind, Label = label };

    public static CollectionSpec Collection(string path, string label, params CollectionColumnSpec[] columns) =>
        new() { Path = path, Label = label, Columns = columns };

    public static TabSpec Tab(string title, params SectionSpec[] sections) =>
        new() { Title = title, Sections = sections };

    // No GameManifest-building helper here: every per-game manifest file lives inside
    // this same "Manifest" namespace, and a factory method named "Manifest(...)" collides
    // with the enclosing namespace segment. Per-game files build GameManifest directly
    // with the record initializer instead - see IB3Manifest.cs.
}
