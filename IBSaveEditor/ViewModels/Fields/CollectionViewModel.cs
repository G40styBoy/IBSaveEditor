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
    public CollectionRowViewModel(CollectionSpec spec, Game game, IReadOnlyList<SaveNode> elementChildren, int arrayIndex)
    {
        ArrayIndex = arrayIndex;
        Columns = spec.Columns
            .Select(c => new FieldViewModel(
                new FieldSpec { Path = c.RelativePath, Kind = c.Kind, Label = c.Label, CatalogCategory = c.CatalogCategory },
                game, elementChildren))
            .ToList();

        Title = spec.TitleColumnPath is { } titlePath
            ? Columns.FirstOrDefault(c => c.Spec.Path == titlePath)?.DisplayText is { Length: > 0 } text
                ? text
                : $"#{arrayIndex + 1}"
            : $"#{arrayIndex + 1}";
    }

    /// <summary>This row's position in the underlying save array - stable across filtering, unlike its position in <see cref="CollectionViewModel.Rows"/>.</summary>
    public int ArrayIndex { get; }

    public string Title { get; }
    public IReadOnlyList<FieldViewModel> Columns { get; }
}

/// <summary>
/// Renders one <see cref="CollectionSpec"/> against an already-loaded save: resolves
/// the array, builds one <see cref="CollectionRowViewModel"/> per element (applying
/// <see cref="CollectionSpec.OwnedOnlyColumnPath"/> if set), and exposes the result for
/// <c>CollectionView</c> to render as repeating rows of reused <c>FieldRowView</c>s.
/// </summary>
public sealed class CollectionViewModel
{
    public CollectionViewModel(CollectionSpec spec, Game game, IReadOnlyList<SaveNode> root)
    {
        Spec = spec;
        Rows = BuildRows(spec, game, root);
    }

    public CollectionSpec Spec { get; }
    public string Label => Spec.Label;
    public IReadOnlyList<CollectionRowViewModel> Rows { get; }
    public bool HasRows => Rows.Count > 0;

    private static List<CollectionRowViewModel> BuildRows(CollectionSpec spec, Game game, IReadOnlyList<SaveNode> root)
    {
        var rows = new List<CollectionRowViewModel>();

        if (SavePathResolver.Resolve(spec.Path, root) is not ArrayNode array)
            return rows;

        var index = 0;
        foreach (var item in array.Items)
        {
            var elementChildren = item is StructNode s ? (IReadOnlyList<SaveNode>)s.Children : Array.Empty<SaveNode>();
            var thisIndex = index++;

            if (spec.OwnedOnlyColumnPath is { } ownedPath && !IsOwned(ownedPath, elementChildren))
                continue;

            rows.Add(new CollectionRowViewModel(spec, game, elementChildren, thisIndex));
        }

        return rows;
    }

    private static bool IsOwned(string relativePath, IReadOnlyList<SaveNode> elementChildren) =>
        SavePathResolver.Resolve(relativePath, elementChildren) is PrimitiveNode { Value: not null } p
        && TryToDouble(p.Value, out var n) && n > 0;

    private static bool TryToDouble(object value, out double result)
    {
        try { result = Convert.ToDouble(value); return true; }
        catch { result = 0; return false; }
    }
}
