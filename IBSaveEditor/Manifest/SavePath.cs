namespace IBSaveEditor.Manifest;

/// <summary>
/// One step of a parsed <see cref="SavePath"/>: either a named lookup into the
/// current node's children (<see cref="Name"/>) or a positional lookup into
/// the current array's items (<see cref="Index"/>). Exactly one is set.
/// </summary>
public readonly record struct SavePathSegment(string? Name, int? Index)
{
    public bool IsIndex => Index.HasValue;

    public override string ToString() => IsIndex ? $"[{Index}]" : Name ?? "";
}

/// <summary>
/// Parses manifest path strings like <c>"Currency[0].Current"</c> into a flat
/// list of <see cref="SavePathSegment"/>s that <see cref="SavePathResolver"/>
/// walks against a loaded save's node tree.
/// </summary>
public static class SavePath
{
    /// <exception cref="ArgumentException">The path is null, empty, or whitespace.</exception>
    /// <exception cref="FormatException">A bracket group is malformed or non-numeric.</exception>
    public static IReadOnlyList<SavePathSegment> Parse(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Save path cannot be empty.", nameof(path));

        var segments = new List<SavePathSegment>();

        foreach (var part in path.Split('.'))
        {
            var bracketStart = part.IndexOf('[');

            if (bracketStart < 0)
            {
                segments.Add(new SavePathSegment(part, null));
                continue;
            }

            var name = part[..bracketStart];
            if (name.Length > 0)
                segments.Add(new SavePathSegment(name, null));

            var rest = part[bracketStart..];
            while (rest.Length > 0)
            {
                if (rest[0] != '[')
                    throw new FormatException($"Malformed segment '{part}' in save path '{path}'.");

                var close = rest.IndexOf(']');
                if (close < 0)
                    throw new FormatException($"Unclosed '[' in segment '{part}' of save path '{path}'.");

                var indexText = rest[1..close];
                if (!int.TryParse(indexText, out var index))
                    throw new FormatException($"Non-numeric index '{indexText}' in segment '{part}' of save path '{path}'.");

                segments.Add(new SavePathSegment(null, index));
                rest = rest[(close + 1)..];
            }
        }

        return segments;
    }
}
