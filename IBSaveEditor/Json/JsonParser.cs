using System.Text.Json;
using IBSaveEditor.UProperties; 
using IBSaveEditor.Package;
using IBSaveEditor.Util;
using IBSaveEditor.Enums;
using System.Text;


namespace IBSaveEditor.Json;
/// <summary>
/// Accepts deserialized data from an UnrealPackage and writes it to a .json file
/// in an envelope format: { "meta": { ... }, "data": { ... } }.
/// </summary>
public sealed class JsonDataParser
{
    private readonly List<UProperty> saveData;
    private readonly PackageInfo info;

    public JsonDataParser(List<UProperty> saveData, PackageInfo info)
    {
        this.saveData = saveData;
        this.info = info;
        IBEnum.SetGame(info.game);
    }

    public void WriteDataToFile()
    {
        if (info is null)
            throw new ArgumentNullException(nameof(info));

        try
        {
            using var stream = File.Create($"{ToolPaths.OutputDir}/{info.packageName}.json");
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
            WriteAll(writer);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to write deserialized save JSON.", ex);
        }
    }

    public string ReturnDataAsString()
    {
        if (info is null)
            throw new ArgumentNullException(nameof(info));

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

    private void WriteAll(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        WriteMeta(writer);
        WriteData(writer);
        writer.WriteEndObject();
        writer.Flush();
    }

    private void WriteMeta(Utf8JsonWriter writer)
    {
        writer.WritePropertyName("metadata");
        writer.WriteStartObject();

        writer.WriteString("packageName", info.packageName);
        writer.WriteString("game", info.game.ToString());
        writer.WriteBoolean("isEncrypted", info.isEncrypted);
        writer.WriteNumber("saveVersion", info.saveVersion);
        writer.WriteNumber("saveMagic", info.saveMagic);

        writer.WriteEndObject();
    }

    private void WriteData(Utf8JsonWriter writer)
    {
        writer.WritePropertyName("data");
        writer.WriteStartObject();

        foreach (var uProperty in saveData)
            uProperty.WriteValueData(writer, uProperty.name);

        writer.WriteEndObject();
    }
}