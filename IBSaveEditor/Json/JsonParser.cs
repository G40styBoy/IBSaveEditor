using System.Text.Json;
using IBSaveEditor.UProperties; 
using IBSaveEditor.Package;
using IBSaveEditor.Util;

namespace IBSaveEditor.Json;
/// <summary>
/// Accepts deserialized data from an UnrealPackage and writes it to a .json file
/// in an envelope format: { "meta": { ... }, "data": { ... } }.
/// </summary>
public sealed class JsonDataParser : IDisposable
{
    private readonly List<UProperty> saveData;
    private readonly FileStream _stream;
    private readonly Utf8JsonWriter _writer;
    private readonly PackageInfo info;

    public JsonDataParser(List<UProperty> saveData, PackageInfo info)
    {
        this.saveData = saveData;
        this.info = info;

        _stream = File.Create($"{ToolPaths.OutputDir}/{info.packageName}.json");
        _writer = new Utf8JsonWriter(_stream, new JsonWriterOptions { Indented = true });
    }

    /// <summary>
    /// Writes out all save data into a json envelope that includes required metadata for repackaging.
    /// </summary>
    public void WriteDataToFile()
    {
        if (info is null)
            throw new ArgumentNullException(nameof(info));

        // set enum pool for the correct game
        IBEnum.SetGame(info.game);

        try
        {
            _writer.WriteStartObject();

            WriteMeta(info);
            WriteData();

            _writer.WriteEndObject();
            _writer.Flush();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to write deserialized save JSON.", ex);
        }
    }

    private void WriteMeta(PackageInfo info)
    {
        _writer.WritePropertyName("metadata");
        _writer.WriteStartObject();

        _writer.WriteString("packageName", info.packageName);
        _writer.WriteString("game", info.game.ToString());
        _writer.WriteBoolean("isEncrypted", info.isEncrypted);
        _writer.WriteNumber("saveVersion", info.saveVersion);
        _writer.WriteNumber("saveMagic", info.saveMagic);

        _writer.WriteEndObject();
    }

    private void WriteData()
    {
        _writer.WritePropertyName("data");
        _writer.WriteStartObject();

        foreach (var uProperty in saveData)
            uProperty.WriteValueData(_writer, uProperty.name);

        _writer.WriteEndObject();
    }

    public void Dispose()
    {
        _writer.Dispose();
        _stream.Dispose();
    }
}