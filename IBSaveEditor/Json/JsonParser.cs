using System.Diagnostics;
using System.Text.Json;

namespace SaveDumper.JsonParser;

/// <summary>
/// Accepts Deserialized Data from an UnrealPackage and writes it to a .json file
/// *generated data gets sent to \OUTPUT* 
/// </summary>
public class JsonDataParser : IDisposable
{
    private readonly string filePath = $@"{FilePaths.OutputDir}\Deserialized Save Data.json";
    private readonly List<UProperty> saveData;
    private readonly FileStream fs;
    private readonly Utf8JsonWriter writer;

    public JsonDataParser(List<UProperty> saveData)
    {
        this.saveData = saveData;

        fs = File.Create(filePath);
        writer = new Utf8JsonWriter(fs, new JsonWriterOptions {Indented = true});
    }

    /// <summary>
    /// Writes out all save data neatly into a json file
    /// </summary>
    internal void WriteDataToFile(UnrealPackage UPK)
    {
        // set the package type for our enumerator class so our program knows what game's enum pool to pull from
        IBEnum.game = UPK.game;
        try
        {
            writer.WriteStartObject();
            foreach (var uProperty in saveData)
                uProperty.WriteValueData(writer, uProperty.name);
            writer.WriteEndObject();
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(exception.Message);
        }
    }

    public void Dispose()
    {
        writer?.Dispose();
        fs?.Dispose();
    }
}
