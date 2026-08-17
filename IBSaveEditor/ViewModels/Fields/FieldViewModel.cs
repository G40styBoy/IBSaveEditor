using System.Globalization;
using ReactiveUI;
using IBSaveEditor.Catalog;
using IBSaveEditor.Enums;
using IBSaveEditor.Manifest;
using IBSaveEditor.Models;
using IBSaveEditor.Package;

namespace IBSaveEditor.ViewModels.Fields;

/// <summary>
/// Which editing widget a resolved <see cref="FieldSpec"/> actually renders as.
/// Several <see cref="FieldKind"/> values share a widget until they earn a
/// dedicated one : Money/StatPoint/Counter are all int-backed edits and reuse
/// the Number control, and ItemRef/GemRef both resolve to
/// <see cref="CatalogPicker"/> - a search-and-select widget over
/// <c>CatalogService</c>, filtered to a different category per field
/// (<see cref="FieldSpec.CatalogCategory"/>), not two different controls.
/// <para>
/// <see cref="EnumChoice"/> edits through a dropdown sourced two ways: an
/// authoritative C# enum when <see cref="EnumChoiceSource"/> has one for the
/// field's EnumType, otherwise the corpus-observed values in
/// <see cref="ObservedEnumValues"/> (explicitly not guaranteed complete - see its
/// own doc comment). Either way the field's actual current value is always in
/// the candidate list even if neither source already had it, so nothing is ever
/// silently unselectable. A path that resolves to something other than an
/// EnumNode has no candidates at all and falls back to plain display.
/// </para>
/// </summary>
public enum FieldControlKind { Number, Toggle, Text, CatalogPicker, EnumChoice, Unsupported }

/// <summary>
/// Binds one manifest <see cref="FieldSpec"/> to the <see cref="PrimitiveNode"/>
/// it resolves to inside an already-loaded save's node tree.
/// <para>
/// Resolution happens once, at construction, via <see cref="SavePathResolver"/>
/// against the same root node list the raw tree editor (<c>NodeViewModel</c>)
/// operates on - edits made here and edits made in the tree view mutate the
/// same backing objects, so both stay in sync automatically.
/// </para>
/// <para>
/// A path that doesn't resolve (missing field, wrong game shape, or resolves
/// to something other than a primitive) leaves this field in a read-only
/// "not present" state rather than throwing : whether that's expected is a
/// property of the manifest (<see cref="FieldSpec.IsOptional"/>), checked
/// separately by the corpus honesty test, not by the UI at load time.
/// </para>
/// </summary>
public sealed class FieldViewModel : ReactiveObject
{
    private const int MaxVisibleCandidates = 40;

    private readonly Game _game;
    private readonly SaveNode? _resolvedNode;
    private readonly PrimitiveNode? _primitiveNode;
    private readonly EnumNode? _enumNode;
    private readonly Action? _onEdited;
    private object? _value;

    public FieldViewModel(FieldSpec spec, Game game, IReadOnlyList<SaveNode> root, Action? onEdited = null)
    {
        Spec  = spec;
        _game = game;
        _resolvedNode  = SavePathResolver.Resolve(spec.Path, root);
        _primitiveNode = _resolvedNode as PrimitiveNode;
        _enumNode      = _resolvedNode as EnumNode;
        _onEdited      = onEdited;
        _value = _primitiveNode?.Value;

        if (ControlKind == FieldControlKind.CatalogPicker)
            InitializeCatalogPickerCandidates();
        else if (ControlKind == FieldControlKind.EnumChoice)
            InitializeEnumChoiceCandidates();
    }

    public FieldSpec Spec { get; }

    public string  Label           => Spec.Label;
    public string? Description     => Spec.Description;
    public bool    HasDescription  => !string.IsNullOrEmpty(Spec.Description);

    /// <summary>
    /// False when the path didn't resolve to anything in this save. A resolved
    /// path that isn't the node type its <see cref="FieldKind"/> expects (e.g. an
    /// EnumChoice field whose path actually points at a primitive) still counts
    /// as resolved here - see <see cref="IsEnumChoiceControl"/>'s own display
    /// fallback for that mismatch.
    /// </summary>
    public bool IsResolved => _resolvedNode != null;

    /// <summary>True when a resolved Number field's backing value is a float rather than an int.</summary>
    public bool IsFloatNumber => _primitiveNode?.TypeHint == "float";

    public FieldControlKind ControlKind => Spec.Kind switch
    {
        FieldKind.Number or FieldKind.Money or FieldKind.StatPoint or FieldKind.Counter => FieldControlKind.Number,
        FieldKind.Toggle                      => FieldControlKind.Toggle,
        FieldKind.Text                        => FieldControlKind.Text,
        FieldKind.ItemRef or FieldKind.GemRef => FieldControlKind.CatalogPicker,
        FieldKind.EnumChoice                  => FieldControlKind.EnumChoice,
        _                                      => FieldControlKind.Unsupported,
    };

    public bool IsNumberControl        => ControlKind == FieldControlKind.Number;
    public bool IsToggleControl        => ControlKind == FieldControlKind.Toggle;
    public bool IsTextControl          => ControlKind == FieldControlKind.Text;
    public bool IsCatalogPickerControl => ControlKind == FieldControlKind.CatalogPicker;
    public bool IsEnumChoiceControl    => ControlKind == FieldControlKind.EnumChoice;
    public bool IsUnsupportedControl   => ControlKind == FieldControlKind.Unsupported;

    /// <summary>Number control, int-backed value : the two split so the view doesn't need a MultiBinding.</summary>
    public bool IsIntegerNumberControl => IsNumberControl && !IsFloatNumber;

    /// <summary>Number control, float-backed value.</summary>
    public bool IsFloatNumberControl => IsNumberControl && IsFloatNumber;

    /// <summary>
    /// A human-readable summary of this field's current value, regardless of kind.
    /// Used for collection row titles (see <c>CollectionRowViewModel</c>) - not bound
    /// to directly by <c>FieldRowView</c> itself, which uses the kind-specific
    /// properties so each control keeps its own display logic.
    /// </summary>
    public string DisplayText => ControlKind switch
    {
        FieldControlKind.CatalogPicker => CatalogPickerDisplayLabel,
        FieldControlKind.EnumChoice    => SelectedEnumChoice ?? EnumChoiceDisplayValue,
        FieldControlKind.Toggle        => ToggleEditValue ? "on" : "off",
        FieldControlKind.Number        => IsFloatNumber ? FloatEditValue : IntEditValue,
        _                               => TextEditValue,
    };

    /// <summary>
    /// The edited value. Writes straight through to the backing
    /// <see cref="PrimitiveNode"/> on every set, exactly like
    /// <c>NodeViewModel.EditValue</c> - there's no separate commit step.
    /// Not used by EnumChoice, which writes through <see cref="SelectedEnumChoice"/>
    /// instead (a different backing node type, <see cref="EnumNode"/>).
    /// <para>
    /// Views don't bind to this directly : bind to <see cref="TextEditValue"/>,
    /// <see cref="IntEditValue"/>, <see cref="FloatEditValue"/>, or
    /// <see cref="ToggleEditValue"/> instead. Those exist specifically because an
    /// Avalonia compiled `{Binding Value, Converter=...}` on this <c>object?</c>-typed
    /// property rendered wrong at runtime (values that were provably correct in every
    /// C# check - "not present in this save" was never the state, the wrong value was)
    /// while a plain same-typed string property (<see cref="CatalogPickerDisplayLabel"/>)
    /// rendered correctly in the same window. Moving the conversion into C# instead of
    /// an XAML value converter sidesteps whatever that was.
    /// </para>
    /// </summary>
    public object? Value
    {
        get => _value;
        set
        {
            this.RaiseAndSetIfChanged(ref _value, value);
            if (_primitiveNode != null)
                _primitiveNode.Value = value;
            this.RaisePropertyChanged(nameof(TextEditValue));
            this.RaisePropertyChanged(nameof(IntEditValue));
            this.RaisePropertyChanged(nameof(FloatEditValue));
            this.RaisePropertyChanged(nameof(ToggleEditValue));
            _onEdited?.Invoke();
        }
    }

    /// <summary>Text control's bound value - a properly string-typed wrapper over <see cref="Value"/>.</summary>
    public string TextEditValue
    {
        get => Value as string ?? string.Empty;
        set => Value = value;
    }

    /// <summary>
    /// Int-backed Number control's bound value, formatted/parsed as a plain integer string.
    /// <para>
    /// Every field's <c>FieldRowView</c> instantiates all four value controls
    /// (Toggle/Text/Int/Float) regardless of which one is actually visible for that
    /// field's kind - Avalonia still evaluates the bindings on the hidden ones. Without
    /// the <see cref="IsIntegerNumberControl"/> guard, every non-numeric field (the
    /// large majority - Text, Toggle, ItemRef, EnumChoice, an owned-items collection's
    /// non-numeric columns) would attempt <c>Convert.ToInt64</c> on a string/bool value
    /// on every binding refresh, throwing and catching a real .NET exception each time.
    /// Harmless to correctness (the catch already returned empty either way) but not
    /// free, and it was loud enough in a debugger to look like a bug. Bailing out before
    /// the conversion is attempted at all - not just catching its failure - is the fix.
    /// </para>
    /// </summary>
    public string IntEditValue
    {
        get
        {
            if (!IsIntegerNumberControl || Value is null) return string.Empty;
            try { return Convert.ToInt64(Value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture); }
            catch { return string.Empty; }
        }
        set
        {
            if (IsIntegerNumberControl && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                Value = parsed;
        }
    }

    /// <summary>Float-backed Number control's bound value, formatted/parsed as a decimal string. Guarded the same way and for the same reason as <see cref="IntEditValue"/>.</summary>
    public string FloatEditValue
    {
        get
        {
            if (!IsFloatNumberControl || Value is null) return string.Empty;
            try { return Convert.ToDouble(Value, CultureInfo.InvariantCulture).ToString("G", CultureInfo.InvariantCulture); }
            catch { return string.Empty; }
        }
        set
        {
            if (IsFloatNumberControl && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                Value = parsed;
        }
    }

    /// <summary>Toggle control's bound value.</summary>
    public bool ToggleEditValue
    {
        get => Value is true;
        set => Value = value;
    }

    #region Catalog picker (ItemRef / GemRef)

    private bool _isCatalogPickerOpen;
    private string _catalogPickerSearchText = string.Empty;

    /// <summary>Every catalog entry this field's picker can choose from, filtered by <see cref="FieldSpec.CatalogCategory"/> if set.</summary>
    public IReadOnlyList<CatalogPickerItem> CatalogPickerCandidates { get; private set; } = Array.Empty<CatalogPickerItem>();

    /// <summary>The catalog entry matching the field's current value, or null if unresolved / not in the catalog.</summary>
    public CatalogPickerItem? SelectedCatalogPickerCandidate { get; private set; }

    /// <summary>
    /// True when this field holds a value that isn't in the catalog - a real gap
    /// (see <c>CatalogCoverageTests</c>), not a bug : the raw value still displays,
    /// just without a friendly label, so nothing looks silently blank.
    /// </summary>
    public bool HasUnresolvedCatalogValue =>
        IsCatalogPickerControl && IsResolved && SelectedCatalogPickerCandidate is null && !string.IsNullOrEmpty(Value as string);

    public string CatalogPickerDisplayLabel => SelectedCatalogPickerCandidate?.Label ?? (Value as string) ?? "(none)";

    public bool IsCatalogPickerOpen
    {
        get => _isCatalogPickerOpen;
        set => this.RaiseAndSetIfChanged(ref _isCatalogPickerOpen, value);
    }

    public string CatalogPickerSearchText
    {
        get => _catalogPickerSearchText;
        set
        {
            this.RaiseAndSetIfChanged(ref _catalogPickerSearchText, value);
            this.RaisePropertyChanged(nameof(FilteredCatalogPickerCandidates));
        }
    }

    /// <summary>Candidates matching the current search text, capped so the picker never renders a catalog's full 1000+ rows at once.</summary>
    public IReadOnlyList<CatalogPickerItem> FilteredCatalogPickerCandidates =>
        (string.IsNullOrWhiteSpace(CatalogPickerSearchText)
            ? CatalogPickerCandidates
            : CatalogPickerCandidates.Where(c => c.Label.Contains(CatalogPickerSearchText, StringComparison.OrdinalIgnoreCase)))
        .Take(MaxVisibleCandidates)
        .ToList();

    public void OpenCatalogPicker()
    {
        CatalogPickerSearchText = string.Empty;
        IsCatalogPickerOpen = true;
    }

    public void CancelCatalogPicker() => IsCatalogPickerOpen = false;

    public void SelectCatalogPickerCandidate(CatalogPickerItem item)
    {
        SelectedCatalogPickerCandidate = item;
        Value = item.InternalName;
        IsCatalogPickerOpen = false;
        this.RaisePropertyChanged(nameof(SelectedCatalogPickerCandidate));
        this.RaisePropertyChanged(nameof(CatalogPickerDisplayLabel));
        this.RaisePropertyChanged(nameof(HasUnresolvedCatalogValue));
    }

    private void InitializeCatalogPickerCandidates()
    {
        var entries = CatalogService.GetEntries(_game).Values.AsEnumerable();
        if (!string.IsNullOrEmpty(Spec.CatalogCategory))
            entries = entries.Where(e => e.Category.Equals(Spec.CatalogCategory, StringComparison.OrdinalIgnoreCase));

        CatalogPickerCandidates = entries
            .Select(CatalogPickerItem.From)
            .OrderBy(c => c.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var currentName = Value as string;
        SelectedCatalogPickerCandidate = currentName is null
            ? null
            : CatalogPickerCandidates.FirstOrDefault(c => c.InternalName == currentName);
    }

    #endregion

    #region EnumChoice

    /// <summary>
    /// Selectable values for this EnumChoice field. Empty when the path didn't resolve
    /// to an <see cref="EnumNode"/> - the view falls back to <see cref="EnumChoiceDisplayValue"/>.
    /// </summary>
    public IReadOnlyList<string> EnumChoiceCandidates { get; private set; } = Array.Empty<string>();

    public bool HasEnumChoiceCandidates => EnumChoiceCandidates.Count > 0;

    /// <summary>
    /// False when <see cref="EnumChoiceCandidates"/> came from <see cref="ObservedEnumValues"/>
    /// rather than an authoritative C# enum - the view shows a caption warning the list
    /// might not be exhaustive.
    /// </summary>
    public bool IsEnumChoiceAuthoritative { get; private set; }

    /// <summary>True when the dropdown is showing and its candidate list isn't guaranteed complete.</summary>
    public bool ShowsUnverifiedEnumChoiceWarning => HasEnumChoiceCandidates && !IsEnumChoiceAuthoritative;

    /// <summary>True when this is an EnumChoice field but the path didn't actually resolve to an EnumNode - falls back to plain text instead of a dropdown.</summary>
    public bool IsEnumChoiceMismatch => IsEnumChoiceControl && !HasEnumChoiceCandidates;

    /// <summary>Read-only display for an EnumChoice field with no candidate source, or a mismatch notice if the path resolved to something other than an enum.</summary>
    public string EnumChoiceDisplayValue => _enumNode?.EnumValue ?? "(not an enum in this save)";

    /// <summary>The dropdown's selection. Writes straight through to the backing <see cref="EnumNode"/>'s EnumValue on every set.</summary>
    public string? SelectedEnumChoice
    {
        get => _enumNode?.EnumValue;
        set
        {
            if (_enumNode is null || value is null)
                return;

            _enumNode.EnumValue = value;
            this.RaisePropertyChanged(nameof(SelectedEnumChoice));
            this.RaisePropertyChanged(nameof(EnumChoiceDisplayValue));
            _onEdited?.Invoke();
        }
    }

    private void InitializeEnumChoiceCandidates()
    {
        if (_enumNode is null)
            return;

        var authoritative = EnumChoiceSource.TryGetAuthoritativeType(_game, _enumNode.EnumType);

        List<string> candidates;
        if (authoritative != null)
        {
            candidates = Enum.GetNames(authoritative)
                // UnrealScript's own "_MAX" sentinel marks the end of the enum for array
                // sizing purposes - it's never a real serialized value.
                .Where(n => !n.EndsWith("_MAX", StringComparison.Ordinal))
                .ToList();
            IsEnumChoiceAuthoritative = true;
        }
        else
        {
            candidates = (ObservedEnumValues.TryGet(_game, _enumNode.EnumType) ?? Array.Empty<string>()).ToList();
            IsEnumChoiceAuthoritative = false;
        }

        // Never make the field's own current value unselectable, even if neither
        // source already listed it.
        if (!candidates.Contains(_enumNode.EnumValue, StringComparer.Ordinal))
            candidates.Insert(0, _enumNode.EnumValue);

        EnumChoiceCandidates = candidates;
    }

    #endregion
}
