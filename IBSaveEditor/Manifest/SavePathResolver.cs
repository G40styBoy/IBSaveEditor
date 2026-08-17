using IBSaveEditor.Models;

namespace IBSaveEditor.Manifest;

/// <summary>
/// Walks a parsed <see cref="SavePath"/> against a loaded save's root node
/// list (the same <c>List&lt;SaveNode&gt;</c> produced by
/// <c>JsonToNodeTree.Convert</c>) and returns the node it points to, or
/// <c>null</c> if any segment along the way doesn't exist.
/// <para>
/// Deliberately tolerant rather than throwing: manifest paths are hand-written
/// against a specific game's save shape, and a field that's simply absent from
/// a given save (early-game, DLC-gated, etc.) is an expected outcome the
/// caller decides how to handle - see the corpus honesty test that walks every
/// manifest path against every real save on disk.
/// </para>
/// </summary>
public static class SavePathResolver
{
    public static SaveNode? Resolve(string path, IReadOnlyList<SaveNode> root) =>
        Resolve(SavePath.Parse(path), root);

    public static SaveNode? Resolve(IReadOnlyList<SavePathSegment> path, IReadOnlyList<SaveNode> root)
    {
        if (path.Count == 0)
            return null;

        SaveNode? current = null;
        IReadOnlyList<SaveNode>? searchSpace = root;

        foreach (var segment in path)
        {
            if (segment.IsIndex)
            {
                if (current is not ArrayNode array)
                    return null;

                var index = segment.Index!.Value;
                if (index < 0 || index >= array.Items.Count)
                    return null;

                current = array.Items[index];
            }
            else
            {
                if (searchSpace is null)
                    return null;

                current = searchSpace.FirstOrDefault(n => n.Name == segment.Name);
                if (current is null)
                    return null;
            }

            searchSpace = current switch
            {
                StructNode s => s.Children,
                _            => null,
            };
        }

        return current;
    }
}
