using System.Text.Json;

/// <summary>
/// Accepts Deserialized Data from an UnrealPackage and writes it to a .json file.
/// </summary>
public class JsonDataParser : IDisposable
{
    private readonly string filePath = $@"{FilePaths.OutputDir}\Deserialized Save Data.json";
    private readonly List<UProperty> saveData;
    private readonly FileStream _stream;
    private readonly Utf8JsonWriter _writer;

    public JsonDataParser(List<UProperty> saveData)
    {
        this.saveData = saveData;

        _stream = File.Create(filePath);
        _writer = new Utf8JsonWriter(_stream, new JsonWriterOptions {Indented = true});
    }

    /// <summary>
    /// Writes out all save data neatly into a json file
    /// </summary>
    public void WriteDataToFile(Game game)
    {
        // set the package type for our enumerator class so our program knows what game's enum pool to pull from
        IBEnum.game = game;
        try
        {
            _writer.WriteStartObject();
            foreach (var uProperty in saveData)
                uProperty.WriteValueData(_writer, uProperty.name);
            _writer.WriteEndObject();
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(exception.Message);
        }
    }

    public void Dispose()
    {
        _writer?.Dispose();
        _stream?.Dispose();
    }
}
