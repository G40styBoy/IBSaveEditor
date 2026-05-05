using System.Text.Json;
using IBSaveEditor.UProperties;
using IBSaveEditor.Package;
using IBSaveEditor.Util;
using IBSaveEditor.Enums;
using System.Text;

namespace IBSaveEditor.Json;

/// <summary>
/// Converts a deserialized <see cref="UProperty"/> list into a structured JSON envelope
/// and writes it either to a file in OUTPUT or to an in-memory string.
/// <para>
/// The output format is:
/// <code>
/// {
///   "metadata": { packageName, game, isEncrypted, saveVersion, saveMagic },
///   "data":     { ... all save properties ... }
/// }
/// </code>
/// The metadata envelope is what allows the save to be re-serialized back to .bin
/// without losing package identity information.
/// </para>
/// </summary>
public sealed class JsonDataParser
{
    private readonly List<UProperty> _saveData;
    private readonly PackageInfo     _info;

    /// <param name="saveData">The deserialized properties to write.</param>
    /// <param name="info">Package metadata written into the envelope header.</param>
    public JsonDataParser(List<UProperty> saveData, PackageInfo info)
    {
        ArgumentNullException.ThrowIfNull(saveData);
        ArgumentNullException.ThrowIfNull(info);

        _saveData = saveData;
        _info     = info;

        // The IBEnum registry needs to know the game before any property writes
        // because enum index lookups are game-specific.
        IBEnum.SetGame(info.game);
    }

    //  Public output methods ─────────────────────────────────────────────────

    /// <summary>
    /// Writes the full JSON envelope to a file in the OUTPUT directory.
    /// The filename is derived from the package name in <see cref="PackageInfo"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the file cannot be written.</exception>
    public void WriteDataToFile()
    {
        try
        {
            var path = Path.Combine(ToolPaths.OutputDir, $"{_info.packageName}.json");
            using var stream = File.Create(path);
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
            WriteAll(writer);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to write deserialized save JSON.", ex);
        }
    }

    /// <summary>
    /// Serializes the full JSON envelope to a UTF-8 string in memory without touching disk.
    /// Used by the UI to load save data directly without an intermediate file.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if serialization fails.</exception>
    public string ReturnDataAsString()
    {
        try
        {
            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
            WriteAll(writer);
            return Encoding.UTF8.GetString(stream.ToArray());
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to serialize save JSON.", ex);
        }
    }

    //  Private write steps ───────────────────────────────────────────────────

    /// <summary>
    /// Writes the complete envelope: opens the root object, writes metadata and data
    /// sections, then closes and flushes.
    /// </summary>
    private void WriteAll(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        WriteMeta(writer);
        WriteData(writer);
        writer.WriteEndObject();
        writer.Flush();
    }

    /// <summary>
    /// Writes the "metadata" section of the envelope. This includes everything needed
    /// to reconstruct a <see cref="PackageInfo"/> when deserializing the JSON back to .bin.
    /// </summary>
    private void WriteMeta(Utf8JsonWriter writer)
    {
        writer.WritePropertyName("metadata");
        writer.WriteStartObject();

        writer.WriteString("packageName",  _info.packageName);
        writer.WriteString("game",         _info.game.ToString());
        writer.WriteBoolean("isEncrypted", _info.isEncrypted);
        writer.WriteNumber("saveVersion",  _info.saveVersion);
        writer.WriteNumber("saveMagic",    _info.saveMagic);

        writer.WriteEndObject();
    }

    /// <summary>
    /// Writes the "data" section of the envelope. Each <see cref="UProperty"/> in the
    /// save data list writes itself under its own property name.
    /// </summary>
    private void WriteData(Utf8JsonWriter writer)
    {
        writer.WritePropertyName("data");
        writer.WriteStartObject();

        foreach (var property in _saveData)
            property.WriteValueData(writer, property.name);

        writer.WriteEndObject();
    }
}
