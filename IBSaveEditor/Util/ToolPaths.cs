namespace IBSaveEditor.Util;

/// <summary>
/// Centralizes all file path constants and output directory management for the tool.
/// </summary>
public static class ToolPaths
{
    private static readonly DirectoryInfo ParentDirectory =
        Directory.GetParent(Directory.GetCurrentDirectory())!;

    /// <summary>
    /// The output directory for all generated files.
    /// In DEBUG builds this sits next to the project root for easy access.
    /// In Release builds it sits beside the executable.
    /// </summary>
#if DEBUG
    public static readonly string OutputDir = Path.Combine(ParentDirectory.FullName, "OUTPUT");
#else
    public static readonly string OutputDir = Path.Combine(AppContext.BaseDirectory, "OUTPUT");
#endif

    /// <summary>Creates the OUTPUT directory if it does not already exist.</summary>
    public static void ValidateOutputDirectory()
    {
        if (!Directory.Exists(OutputDir))
            Directory.CreateDirectory(OutputDir);
    }

    /// <summary>Returns whether the OUTPUT directory currently exists on disk.</summary>
    public static bool DoesOutputExist() => Directory.Exists(OutputDir);

    /// <summary>
    /// If a file already exists at <paramref name="path"/>, copies it aside to a
    /// timestamped "*.bak" file in the same directory before anything overwrites it.
    /// <para>
    /// Export always writes to the same fixed <c>OUTPUT/{packageName}.bin</c> path, so
    /// without this, exporting twice - or exporting after a mistake - silently destroys
    /// the previous export with no way back. Timestamping rather than a single ".bak"
    /// keeps every prior export recoverable, not just the first one.
    /// </para>
    /// </summary>
    public static void BackupIfExists(string path)
    {
        if (!File.Exists(path))
            return;

        var dir      = Path.GetDirectoryName(path)!;
        var name     = Path.GetFileNameWithoutExtension(path);
        var ext      = Path.GetExtension(path);
        var backupPath = Path.Combine(dir, $"{name}.{DateTime.Now:yyyyMMdd_HHmmss}.bak{ext}");

        File.Copy(path, backupPath, overwrite: true);
    }
}
