using System;
using Newtonsoft.Json.Linq;
using IBSaveEditor.Package;

namespace IBSaveEditor.Services;

/// <summary>
/// Extracts the <see cref="Game"/> enum value from a save file's JSON metadata envelope.
/// <para>
/// Save files have a top-level <c>metadata</c> object containing a <c>game</c>
/// string field. This service parses that field into the strongly-typed enum
/// the rest of the codebase uses.
/// </para>
/// </summary>
public static class GameMetadataExtractor
{
    private const string MetadataKey = "metadata";
    private const string GameKey     = "game";

    /// <summary>
    /// Reads the game type from the metadata envelope of a parsed JSON root.
    /// </summary>
    /// <param name="root">The full parsed JSON object.</param>
    /// <returns>The game enum value if found and parseable.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when metadata is missing, the game key is absent, or the value
    /// cannot be parsed into the <see cref="Game"/> enum.
    /// </exception>
    public static Game Extract(JObject root)
    {
        var metaProp = FindCaseInsensitive(root, MetadataKey);
        if (metaProp?.Value is not JObject metaObj)
            throw new InvalidOperationException(
                "Save file has no metadata envelope. Cannot determine game type.");

        var gameProp = FindCaseInsensitive(metaObj, GameKey);
        var gameStr  = gameProp?.Value?.ToString();
        if (string.IsNullOrWhiteSpace(gameStr))
            throw new InvalidOperationException(
                "Save file metadata is missing the 'game' field.");

        if (!Enum.TryParse<Game>(gameStr, ignoreCase: true, out var game))
            throw new InvalidOperationException(
                $"Save file metadata 'game' value '{gameStr}' is not a recognized game.");

        return game;
    }

    /// <summary>Finds a property by name with case-insensitive matching.</summary>
    private static JProperty? FindCaseInsensitive(JObject obj, string name)
    {
        foreach (var prop in obj.Properties())
            if (prop.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                return prop;
        return null;
    }
}
