using IBSaveEditor.Catalog;

namespace IBSaveEditor.ViewModels.Fields;

/// <summary>
/// One selectable row in an ItemRef/GemRef picker: the raw internalName a
/// <see cref="FieldViewModel"/> writes back, paired with a display-safe label
/// that's never null even when the catalog has no <see cref="CatalogEntry.DisplayName"/>
/// for it (about a third of entries don't, per the Phase 2 catalog coverage numbers).
/// </summary>
public sealed record CatalogPickerItem(string InternalName, string Label)
{
    public static CatalogPickerItem From(CatalogEntry entry) => new(
        entry.InternalName,
        string.IsNullOrEmpty(entry.DisplayName) ? entry.InternalName : $"{entry.DisplayName} ({entry.InternalName})");
}
