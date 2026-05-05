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
}
