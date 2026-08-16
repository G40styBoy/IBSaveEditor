namespace IBSaveEditor.Util;

/// <summary>
/// Creates a temporary JSON file on disk, exposes its path, and deletes it on dispose.
/// Used to bridge in-memory JSON strings to APIs that require a file path.
/// </summary>
internal sealed class TempJsonFile : IDisposable
{
    /// <summary>The full path of the temporary file on disk.</summary>
    public string Path { get; }

    public TempJsonFile(string json)
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"ibse_{Guid.NewGuid():N}.json");

        File.WriteAllText(Path, json);
    }

    public void Dispose()
    {
        if (File.Exists(Path))
            File.Delete(Path);
    }
}
