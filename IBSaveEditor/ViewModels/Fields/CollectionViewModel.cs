using System.Collections.ObjectModel;
using ReactiveUI;
using IBSaveEditor.Catalog;
using IBSaveEditor.Manifest;
using IBSaveEditor.Models;
using IBSaveEditor.Package;

namespace IBSaveEditor.ViewModels.Fields;

/// <summary>
/// One row of a <see cref="CollectionViewModel"/>: one save-array element, rendered as
/// its <see cref="CollectionSpec"/>'s columns, each a full <see cref="FieldViewModel"/>
/// resolved against this element's own children rather than the save root.
/// </summary>
public sealed class CollectionRowViewModel
{
    public CollectionRowViewModel(CollectionSpec spec, Game game, SaveNode backingItem, int arrayIndex, Action? onEdited = null)
    {
        BackingItem = backingItem;
        ArrayIndex  = arrayIndex;

        // Computed once here, from the spec, rather than bound cross-scope back to the
        // owning CollectionViewModel from inside CollectionView's Rows DataTemplate - the
        // row already knows everything needed to answer "can I be removed" for itself.
        CanBeRemoved = spec.OwnedOnlyColumnPath != null || spec.IsStructurallyEditable;

        var elementChildren = backingItem is StructNode s ? (IReadOnlyList<SaveNode>)s.Children : Array.Empty<SaveNode>();
        Columns = spec.Columns
            .Select(c => new FieldViewModel(
                new FieldSpec { Path = c.RelativePath, Kind = c.Kind, Label = c.Label, CatalogCategory = c.CatalogCategory },
                game, elementChildren, onEdited))
            .ToList();

        Title = spec.TitleColumnPath is { } titlePath
            ? Columns.FirstOrDefault(c => c.Spec.Path == titlePath)?.DisplayText is { Length: > 0 } text
                ? text
                : $"#{arrayIndex + 1}"
            : $"#{arrayIndex + 1}";
    }

    /// <summary>
    /// True when this row can be removed - either mode (owned-toggle "deactivate" or
    /// structural "delete") applies. False for a collection that's neither (e.g. a fixed
    /// roster with no owned filter and not opted into <see cref="CollectionSpec.IsStructurallyEditable"/>),
    /// where a remove button would do nothing.
    /// </summary>
    public bool CanBeRemoved { get; }

    /// <summary>The actual save-array element this row edits - needed to remove it outright (see <see cref="CollectionViewModel.RemoveRow"/>).</summary>
    internal SaveNode BackingItem { get; }

    /// <summary>This row's position in the underlying save array - stable across filtering, unlike its position in <see cref="CollectionViewModel.Rows"/>.</summary>
    public int ArrayIndex { get; }

    public string Title { get; }
    public IReadOnlyList<FieldViewModel> Columns { get; }
}

/// <summary>
/// A row hidden by <see cref="CollectionSpec.OwnedOnlyColumnPath"/>, pickable as an "add"
/// candidate. Deliberately lighter than <see cref="CollectionRowViewModel"/> - a full row
/// builds a <see cref="FieldViewModel"/> per column, including an ItemRef/GemRef column's
/// full catalog candidate list, which is fine for the ~20 rows actually shown but not for
/// the ~400 that could be hidden behind an owned filter. A candidate only needs a label to
/// pick by and the element it activates.
/// </summary>
public sealed class CollectionAddableRowViewModel
{
    internal CollectionAddableRowViewModel(SaveNode backingItem, string title)
    {
        BackingItem = backingItem;
        Title       = title;
    }

    internal SaveNode BackingItem { get; }
    public string Title { get; }
}

/// <summary>
/// Renders one <see cref="CollectionSpec"/> against an already-loaded save: resolves the
/// array, splits its elements into shown <see cref="Rows"/> and, when
/// <see cref="CollectionSpec.OwnedOnlyColumnPath"/> is set, hidden <see cref="AddableCandidates"/>.
/// <para>
/// Two genuinely different add/remove models exist depending on the array's shape:
/// </para>
/// <para>
/// <b>Owned-toggle</b> (<see cref="SupportsOwnedToggle"/>, e.g. IB3's "Owned Items"): the
/// array has one element per possible catalog entry, whether owned or not - PlayerInventory
/// never grows or shrinks. "Adding" an item means picking a hidden element and setting its
/// owned column above zero; "removing" means zeroing it back out. Nothing is ever inserted
/// or deleted. Selected purely by <see cref="CollectionSpec.OwnedOnlyColumnPath"/> being set
/// - no separate opt-in needed, since a filtered collection can only ever mean this.
/// </para>
/// <para>
/// <b>Structural</b> (<see cref="SupportsStructuralAddRemove"/>, e.g. "Spare Gems"): a
/// genuine variable-length array. Adding clones the last existing element (the only way to
/// seed a structurally correct new one without knowing the full UProperty schema - see
/// <see cref="NodeViewModel.CloneNode"/>) and appends it; removing deletes the element
/// outright. Requires <see cref="CollectionSpec.IsStructurallyEditable"/> to be explicitly
/// opted into - unlike the owned-toggle case, the mere absence of an owned filter doesn't
/// mean an array is safe to insert into or delete from (IB3's "All Quests" has no owned
/// filter either, but it's a fixed roster, not a collection to grow or shrink). <see cref="CanAddRow"/>
/// is false on a currently-empty array : there's nothing to clone from, a real gap rather
/// than a bug, same spirit as the manifest's documented one-level-deep limitation.
/// </para>
/// <para>
/// Structural add/remove mutates the underlying <see cref="SaveNode"/> array directly, so
/// export/save reflect it correctly - but the raw tree's <c>NodeViewModel</c> wrapper for
/// this same array was built once at load time and won't show the change until a full
/// Reload. A known, accepted gap: this guarantees correct data, not live cross-surface sync
/// for structural edits specifically (scalar edits already stay in sync with no extra code,
/// since both surfaces write through the same backing nodes).
/// </para>
/// </summary>
public sealed class CollectionViewModel : ReactiveObject
{
    private const int MaxVisibleAddCandidates = 40;

    private readonly Game _game;
    private readonly IReadOnlyList<SaveNode> _root;
    private readonly Action? _onEdited;

    private bool   _isAddPickerOpen;
    private string _addPickerSearchText = string.Empty;

    public CollectionViewModel(CollectionSpec spec, Game game, IReadOnlyList<SaveNode> root, Action? onEdited = null)
    {
        Spec     = spec;
        _game    = game;
        _root    = root;
        _onEdited = onEdited;

        Rows              = new ObservableCollection<CollectionRowViewModel>();
        AddableCandidates = new ObservableCollection<CollectionAddableRowViewModel>();
        Rebuild();
    }

    public CollectionSpec Spec { get; }
    public string Label => Spec.Label;
    public ObservableCollection<CollectionRowViewModel> Rows { get; }
    public bool HasRows => Rows.Count > 0;

    /// <summary>See the class summary's "Owned-toggle" case.</summary>
    public bool SupportsOwnedToggle => Spec.OwnedOnlyColumnPath != null;

    /// <summary>
    /// See the class summary's "Structural" case. Requires <see cref="CollectionSpec.IsStructurallyEditable"/>
    /// to be explicitly opted into - most non-owned-toggle collections (e.g. a fixed quest
    /// roster) aren't safe to freely insert into or delete from just because they aren't
    /// filtered by ownership.
    /// </summary>
    public bool SupportsStructuralAddRemove => Spec.IsStructurallyEditable && !SupportsOwnedToggle;

    /// <summary>Rows hidden by the owned-only filter, pickable as "add" candidates. Always empty unless <see cref="SupportsOwnedToggle"/>.</summary>
    public ObservableCollection<CollectionAddableRowViewModel> AddableCandidates { get; }

    public bool HasAddableCandidates => AddableCandidates.Count > 0;

    /// <summary>Whether the owned-toggle "add item" picker panel is open. Mirrors <c>FieldViewModel.IsCatalogPickerOpen</c>.</summary>
    public bool IsAddPickerOpen
    {
        get => _isAddPickerOpen;
        set => this.RaiseAndSetIfChanged(ref _isAddPickerOpen, value);
    }

    public string AddPickerSearchText
    {
        get => _addPickerSearchText;
        set
        {
            this.RaiseAndSetIfChanged(ref _addPickerSearchText, value);
            this.RaisePropertyChanged(nameof(FilteredAddableCandidates));
        }
    }

    /// <summary>Candidates matching the current search text, capped the same way <c>FieldViewModel.FilteredCatalogPickerCandidates</c> is.</summary>
    public IReadOnlyList<CollectionAddableRowViewModel> FilteredAddableCandidates =>
        (string.IsNullOrWhiteSpace(AddPickerSearchText)
            ? AddableCandidates
            : AddableCandidates.Where(c => c.Title.Contains(AddPickerSearchText, StringComparison.OrdinalIgnoreCase)))
        .Take(MaxVisibleAddCandidates)
        .ToList();

    public void OpenAddPicker()
    {
        AddPickerSearchText = string.Empty;
        IsAddPickerOpen = true;
    }

    public void CancelAddPicker() => IsAddPickerOpen = false;

    /// <summary>False on a currently-empty structural array - <see cref="AddRow"/> has nothing to clone a new element's shape from.</summary>
    public bool CanAddRow => SupportsStructuralAddRemove && Rows.Count > 0;

    /// <summary>Promotes a hidden row into view by setting its owned column to a default count of 1. The array element already existed - nothing is inserted.</summary>
    public void ActivateRow(CollectionAddableRowViewModel candidate)
    {
        if (!SupportsOwnedToggle) return;
        if (!TrySetOwnedValue(candidate.BackingItem, 1L)) return;
        _onEdited?.Invoke();
        IsAddPickerOpen = false;
        Rebuild();
    }

    /// <summary>Demotes a shown row out of view by zeroing its owned column. The array element itself is left alone, not deleted.</summary>
    public void DeactivateRow(CollectionRowViewModel row)
    {
        if (!SupportsOwnedToggle) return;
        if (!TrySetOwnedValue(row.BackingItem, 0L)) return;
        _onEdited?.Invoke();
        Rebuild();
    }

    /// <summary>Appends a new element cloned from the last existing row. No-op when <see cref="CanAddRow"/> is false.</summary>
    public void AddRow()
    {
        if (!CanAddRow) return;
        if (SavePathResolver.Resolve(Spec.Path, _root) is not ArrayNode array || array.Items.Count == 0)
            return;

        var cloned = NodeViewModel.CloneNode(array.Items[^1], $"[{array.Items.Count}]");
        array.Items.Add(cloned);
        _onEdited?.Invoke();
        Rebuild();
    }

    /// <summary>Deletes the row's underlying array element outright.</summary>
    public void RemoveRow(CollectionRowViewModel row)
    {
        if (!SupportsStructuralAddRemove) return;
        if (SavePathResolver.Resolve(Spec.Path, _root) is not ArrayNode array) return;

        array.Items.Remove(row.BackingItem);
        _onEdited?.Invoke();
        Rebuild();
    }

    /// <summary>
    /// Rebuilds <see cref="Rows"/> and <see cref="AddableCandidates"/> from the current
    /// save state. Called after every mutation instead of patching indices in place -
    /// simpler and matches how the raw tree already refreshes wholesale on structural
    /// change, and these collections are the same "not virtualized, a known cost" size
    /// class the manifest already accepts elsewhere.
    /// </summary>
    private void Rebuild()
    {
        Rows.Clear();
        AddableCandidates.Clear();

        if (SavePathResolver.Resolve(Spec.Path, _root) is ArrayNode array)
        {
            var index = 0;
            foreach (var item in array.Items)
            {
                var thisIndex = index++;

                if (Spec.OwnedOnlyColumnPath is { } ownedPath)
                {
                    if (IsOwned(ownedPath, item))
                        Rows.Add(new CollectionRowViewModel(Spec, _game, item, thisIndex, _onEdited));
                    else
                        AddableCandidates.Add(new CollectionAddableRowViewModel(item, ResolveCandidateTitle(item)));
                }
                else
                {
                    Rows.Add(new CollectionRowViewModel(Spec, _game, item, thisIndex, _onEdited));
                }
            }
        }

        this.RaisePropertyChanged(nameof(HasRows));
        this.RaisePropertyChanged(nameof(HasAddableCandidates));
        this.RaisePropertyChanged(nameof(CanAddRow));
        this.RaisePropertyChanged(nameof(FilteredAddableCandidates));
    }

    /// <summary>
    /// A label for a hidden row's picker entry, resolved directly off the raw tree rather
    /// than through a <see cref="FieldViewModel"/> (see <see cref="CollectionAddableRowViewModel"/>'s
    /// summary for why). Reuses the catalog for a friendly label the same way
    /// <see cref="CatalogPickerItem.From"/> does when the title column is a real catalog
    /// reference; falls back to the raw value otherwise.
    /// </summary>
    private string ResolveCandidateTitle(SaveNode item)
    {
        if (Spec.TitleColumnPath is not { } titlePath)
            return "(unnamed)";

        var children = item is StructNode s ? (IReadOnlyList<SaveNode>)s.Children : Array.Empty<SaveNode>();
        if (SavePathResolver.Resolve(titlePath, children) is not PrimitiveNode { Value: string raw } || raw.Length == 0)
            return "(unnamed)";

        return CatalogService.TryResolve(_game, raw, out var entry)
            ? CatalogPickerItem.From(entry!).Label
            : raw;
    }

    private bool TrySetOwnedValue(SaveNode item, long value)
    {
        var children = item is StructNode s ? (IReadOnlyList<SaveNode>)s.Children : Array.Empty<SaveNode>();
        if (SavePathResolver.Resolve(Spec.OwnedOnlyColumnPath!, children) is not PrimitiveNode p)
            return false;

        p.Value = value;
        return true;
    }

    private static bool IsOwned(string relativePath, SaveNode item)
    {
        var children = item is StructNode s ? (IReadOnlyList<SaveNode>)s.Children : Array.Empty<SaveNode>();
        return SavePathResolver.Resolve(relativePath, children) is PrimitiveNode { Value: not null } p
            && TryToDouble(p.Value, out var n) && n > 0;
    }

    private static bool TryToDouble(object value, out double result)
    {
        try { result = Convert.ToDouble(value); return true; }
        catch { result = 0; return false; }
    }
}
