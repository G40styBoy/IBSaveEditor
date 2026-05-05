#if DEBUG
using IBSaveEditor.Wrappers;

namespace IBSaveEditor.Util;
/// <summary>
/// Provides debug-only utilities for dumping binary stream data to disk during failure states.
/// Only compiled in <c>DEBUG</c> builds.
/// </summary>
internal static class DebugDumper
{
    /// <summary>
    /// Dumps the full contents of a stream to the OUTPUT directory.
    /// The filename includes the failure position so the relevant bytes
    /// can be located immediately in HxD.
    /// </summary>
    /// <param name="stream">The stream to dump.</param>
    /// <param name="failurePosition">The stream position where the failure occurred.</param>
    public static void DumpStream(UnrealStream stream, long failurePosition)
    {
        try
        {
            ToolPaths.ValidateOutputDirectory();

            var fileName   = $"dump_pos{failurePosition}_{DateTime.Now:yyyyMMdd_HHmmss}.bin";
            var outputPath = Path.Combine(ToolPaths.OutputDir, fileName);

            var originalPosition = stream.Position;
            stream.Position      = 0;

            using var file = File.Create(outputPath);
            stream.BaseStream.CopyTo(file);

            stream.Position = originalPosition;

            // Util.DebugBreak();
        }
        catch
        {
        }
    }
}
#endif
