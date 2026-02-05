using System.Diagnostics;

public static class Util
{
        public static void PrintColored(string message, ConsoleColor color, bool resetColor = true)
    {
        Console.ForegroundColor = color;
        Console.Write(message);
        if (resetColor)
            Console.ResetColor();
    }

    // Overload for writing a line (adds newline)
    public static void PrintColoredLine(string message, ConsoleColor color, bool resetColor = true) =>
        PrintColored(message + Environment.NewLine, color, resetColor);

    [DebuggerHidden]
    [Conditional("DEBUG")]
    public static void DebugBreak()
    {
        if (Debugger.IsAttached)
            Debugger.Break();
    }
}

/// <summary>
/// utility class for the tool's GUI to make the interface cleaner
/// </summary>
public static class ProgressBar
{
    public static void Run(string label, Action work)
    {
        int barWidth = 50;
        bool running = true;
        Exception? caughtException = null;

        Thread progressThread = new Thread(() =>
        {
            int progress = 0;
            int currentTop = Console.CursorTop;

            while (running)
            {
                Console.CursorVisible = false;
                Console.SetCursorPosition(0, currentTop);
                Util.PrintColored($"{label}: [", ConsoleColor.White, false);
                Util.PrintColored(new string('=', progress), ConsoleColor.Yellow, false);
                Console.Write(new string(' ', barWidth - progress));
                Util.PrintColored("]", ConsoleColor.White, false);
                progress = (progress + 1) % (barWidth + 1);
                Thread.Sleep(50);
            }
        });

        progressThread.Start();

        try
        {
            work();
        }
        catch (Exception ex)
        {
            caughtException = ex;
        }

        running = false;
        progressThread.Join();

        Console.SetCursorPosition(0, Console.CursorTop);
        Util.PrintColored($"{label}: [", ConsoleColor.White, false);

        if (caughtException == null)
        {
            Util.PrintColored(new string('=', barWidth), ConsoleColor.Green, false);
            Util.PrintColored("] 100%\n", ConsoleColor.White);
        }
        else
        {
            Util.PrintColored(new string('=', barWidth), ConsoleColor.Red, false);
            Util.PrintColored("] FAILED\n", ConsoleColor.White);
            Util.PrintColored($"Error: {caughtException.Message}\n", ConsoleColor.Red);
        }

        Console.CursorVisible = true;
    }
}


/// <summary>
/// Helps keep file locations, names, etc. organized.
/// All file path data needed for the program is stored here
/// </summary>
public static class FilePaths
{
    public static DirectoryInfo parentDirectory = Directory.GetParent(Directory.GetCurrentDirectory())!;
    public static string OutputDir = $@"{parentDirectory}\OUTPUT";
    public static string Localization = $@"{parentDirectory}\IBSaveEditor\Localization";

    public static string baseLocation = $@"{parentDirectory}\SAVE STORAGE LOCATION";

    public static string IB3SAVES = Path.Combine(baseLocation, @"IB3 Backup");
    public static string IB2SAVES = Path.Combine(baseLocation, @"IB2 Backup");
    public static string IB1SAVES = Path.Combine(baseLocation, @"IB1 Backup");
    public static string VOTESAVES = Path.Combine(baseLocation, @"VOTE!!! Backup");

    public static void ValidateOutputDirectory()
    {
        if (!File.Exists(OutputDir))
            Directory.CreateDirectory(OutputDir);
    }

    public static bool DoesOutputExist() => File.Exists(OutputDir);
};

