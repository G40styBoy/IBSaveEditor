using System.Text.Json;
using IBSaveEditor.Package;

namespace IBSaveEditor.Json;

/// <summary>
/// Utility methods for reading and validating values from a parsed JSON save envelope.
/// <para>
/// All read methods throw <see cref="InvalidOperationException"/> with a clear message
/// when the expected field is missing, the wrong type, or out of range : so callers
/// don't need to handle null checks themselves.
/// </para>
/// </summary>
public static class JsonUtils
{
    //  Envelope-level reads ──────────────────────────────────────────────────

    /// <summary>
    /// Reads the "metadata" section of a save JSON file and returns a populated
    /// <see cref="EnvelopeMeta"/> record. Used before serialization to reconstruct
    /// <see cref="PackageInfo"/> without a binary package being present.
    /// </summary>
    /// <param name="jsonPath">Path to the JSON save file on disk.</param>
    /// <exception cref="InvalidOperationException">Thrown if any required metadata field is missing or invalid.</exception>
    public static EnvelopeMeta ReadMeta(string jsonPath)
    {
        try
        {
            using var fs  = File.OpenRead(jsonPath);
            using var doc = JsonDocument.Parse(fs);

            var root     = doc.RootElement;
            var metadata = ReadProperty(root, "metadata");

            var game        = ReadEnum<Game>(metadata, "game");
            var isEncrypted = ReadBoolean(metadata, "isEncrypted");
            var packageName = ReadString(metadata, "packageName");
            var saveVersion = ReadUInt32(metadata, "saveVersion");
            var saveMagic   = ReadUInt32(metadata, "saveMagic");

            return new EnvelopeMeta(game, isEncrypted, packageName, saveVersion, saveMagic);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException("Failed to read JSON metadata.", ex);
        }
    }

    /// <summary>
    /// Extracts a named top-level JSON object from a file and returns its raw JSON text.
    /// Used to isolate the "data" section of the envelope so it can be passed to
    /// <see cref="JsonDataCruncher"/> without parsing the rest of the envelope.
    /// </summary>
    /// <param name="jsonPath">Path to the JSON save file.</param>
    /// <param name="objectName">Name of the top-level property to extract (e.g. "data").</param>
    /// <exception cref="InvalidOperationException">Thrown if the property is not found.</exception>
    public static string ExtractDataObjectJson(string jsonPath, string objectName)
    {
        try
        {
            using var fs  = File.OpenRead(jsonPath);
            using var doc = JsonDocument.Parse(fs);

            if (!doc.RootElement.TryGetProperty(objectName, out var dataEl))
                throw new InvalidOperationException($"Invalid JSON: missing '{objectName}' object.");

            return dataEl.GetRawText();
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException($"Failed to extract '{objectName}' object from JSON.", ex);
        }
    }

    //  Element-level reads ───────────────────────────────────────────────────

    /// <summary>
    /// Reads a child property from a <see cref="JsonElement"/> by name.
    /// Throws if the property doesn't exist.
    /// </summary>
    public static JsonElement ReadProperty(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var property))
            throw new InvalidOperationException($"Invalid JSON: missing '{propertyName}'.");

        return property;
    }

    /// <summary>
    /// Reads a non-empty string value from a <see cref="JsonElement"/> by property name.
    /// Throws if the property is missing, not a string, or empty.
    /// </summary>
    public static string ReadString(JsonElement parent, string propertyName)
    {
        var el = ReadProperty(parent, propertyName);

        if (el.ValueKind != JsonValueKind.String)
            throw new InvalidOperationException($"Invalid JSON: '{propertyName}' must be a string.");

        var value = el.GetString();
        if (string.IsNullOrEmpty(value))
            throw new InvalidOperationException($"Invalid JSON: '{propertyName}' is empty.");

        return value;
    }

    /// <summary>
    /// Reads a boolean value from a <see cref="JsonElement"/> by property name.
    /// Throws if the property is missing or not a boolean.
    /// </summary>
    public static bool ReadBoolean(JsonElement parent, string propertyName)
    {
        var el = ReadProperty(parent, propertyName);

        if (el.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new InvalidOperationException($"Invalid JSON: '{propertyName}' must be a boolean.");

        return el.GetBoolean();
    }

    /// <summary>
    /// Reads an unsigned 32-bit integer from a <see cref="JsonElement"/> by property name.
    /// Throws if the property is missing, not a number, or out of UInt32 range.
    /// </summary>
    public static uint ReadUInt32(JsonElement parent, string propertyName)
    {
        var el = ReadProperty(parent, propertyName);

        if (el.ValueKind != JsonValueKind.Number)
            throw new InvalidOperationException($"Invalid JSON: '{propertyName}' must be a number.");

        if (!el.TryGetUInt32(out var value))
            throw new InvalidOperationException(
                $"Invalid JSON: '{propertyName}' is out of range for UInt32.");

        return value;
    }

    /// <summary>
    /// Reads a string value and parses it into the specified enum type.
    /// Throws if the property is missing, not a string, or the value is not a valid enum member.
    /// </summary>
    public static TEnum ReadEnum<TEnum>(JsonElement parent, string propertyName)
        where TEnum : struct, Enum
    {
        var text = ReadString(parent, propertyName);

        if (!Enum.TryParse<TEnum>(text, ignoreCase: true, out var value))
            throw new InvalidOperationException(
                $"Invalid JSON: '{propertyName}' value '{text}' is not a valid {typeof(TEnum).Name}.");

        return value;
    }

    //  Guard helpers ─────────────────────────────────────────────────────────

    /// <summary>
    /// Asserts that a string value is non-null and non-empty.
    /// Used in cruncher contexts where an empty name or value would produce a corrupt save.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <param name="context">A description of where the value came from, for the error message.</param>
    public static string RequireString(string? value, string context)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException(
                $"Unexpected null/empty string while reading {context}.");
        return value;
    }

    /// <summary>
    /// Asserts that a file exists at the given path before any downstream read attempts it.
    /// Throws <see cref="ArgumentNullException"/> if the path is null, or
    /// <see cref="FileNotFoundException"/> if the file does not exist.
    /// </summary>
    public static void RequireFileExists(string path, string? paramName = null)
    {
        ArgumentNullException.ThrowIfNull(path, paramName ?? nameof(path));

        if (!File.Exists(path))
            throw new FileNotFoundException("The specified file does not exist.", path);
    }
}
