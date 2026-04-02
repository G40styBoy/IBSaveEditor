using System.Diagnostics;

namespace IBSaveEditor.Util;
/// <summary>
/// Utility class for methods in which do not need an isolated file. These are used everywhere in the codebase.
/// </summary>
public static class Util
{
    public static void PrintColored(string message, ConsoleColor color, bool resetColor = true)
    {
        Console.ForegroundColor = color;
        Console.Write(message);
        if (resetColor)
            Console.ResetColor();
    }

    public static void PrintColoredLine(string message, ConsoleColor color, bool resetColor = true) =>
        PrintColored(message + Environment.NewLine, color, resetColor);

    public static void PrintBanner(string BANNER_NAME)
    {
        PrintColoredLine("========================================", ConsoleColor.Cyan, true);
        PrintColoredLine($"            {BANNER_NAME}            ", ConsoleColor.Cyan, true);
        PrintColoredLine("========================================", ConsoleColor.Cyan, true);
        PrintColoredLine("© 2026 G40sty. All rights reserved.\n", ConsoleColor.DarkGray, true);
    }

    public static void PrintStep(string label, string status)
    {
        PrintColored(label, ConsoleColor.White, false);
        PrintColored(": ", ConsoleColor.White, false);
        PrintColoredLine(status, ConsoleColor.Green, true);
    }

    public static void PrintStep(string label, string status, ConsoleColor color)
    {
        PrintColored(label, ConsoleColor.White, false);
        PrintColored(": ", ConsoleColor.White, false);
        PrintColoredLine(status, color, true);
    }

    public static void PrintSuccess(string message)
    {
        Console.WriteLine();
        PrintColoredLine(message, ConsoleColor.Green, true);
    }

    public static void PrintFailure(Exception ex)
    {
        Console.WriteLine();
        PrintColoredLine("FAILED", ConsoleColor.Red, true);
        Console.WriteLine();

        PrintColoredLine(ex.GetType().FullName ?? "Exception", ConsoleColor.Red, true);
        PrintColoredLine(ex.Message, ConsoleColor.Red, true);

        Console.WriteLine();
        Console.WriteLine(ex.ToString());
    }

    public static void PrintInlineError(string message) => PrintColoredLine(message, ConsoleColor.Red, true);
    
    public static void Restart()
    {
        Console.WriteLine();
        PrintColoredLine("Press any key to restart...", ConsoleColor.DarkGray, true);
        Console.ReadKey(true);
    }

    public static void RefreshConsole(string BANNER_NAME)
    {
        Console.Clear();
        PrintBanner(BANNER_NAME);
    }

    [DebuggerHidden]
    [Conditional("DEBUG")]
    public static void DebugBreak()
    {
        if (Debugger.IsAttached)
            Debugger.Break();
    }
}