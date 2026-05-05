using System.Diagnostics;

namespace IBSaveEditor.Util;
/// <summary>
/// General utility methods used across the codebase.
/// Console methods are retained for test/debug use.
/// </summary>
public static class Util
{
    // Console ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Writes a message to the console in the specified color.
    /// </summary>
    /// <param name="resetColor">If true, resets the console color after writing.</param>
    public static void PrintColored(string message, ConsoleColor color, bool resetColor = true)
    {
        Console.ForegroundColor = color;
        Console.Write(message);
        if (resetColor)
            Console.ResetColor();
    }

    /// <summary>Writes a message to the console in the specified color, followed by a newline.</summary>
    public static void PrintColoredLine(string message, ConsoleColor color, bool resetColor = true)
        => PrintColored(message + Environment.NewLine, color, resetColor);

    /// <summary>Waits for a keypress then returns : used to pause between test runs.</summary>
    public static void Restart()
    {
        Console.WriteLine();
        PrintColoredLine("Press any key to restart...", ConsoleColor.DarkGray);
        Console.ReadKey(true);
    }

    // Debug ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Triggers a debugger breakpoint if one is attached.
    /// Only executes in <c>DEBUG</c> builds.
    /// </summary>
    [DebuggerHidden]
    [Conditional("DEBUG")]
    public static void DebugBreak()
    {
        if (Debugger.IsAttached)
            Debugger.Break();
    }
}
