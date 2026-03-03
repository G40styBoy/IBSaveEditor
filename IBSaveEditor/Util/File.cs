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
        if (!Directory.Exists(OutputDir))
            Directory.CreateDirectory(OutputDir);
    }

    public static bool DoesOutputExist() => Directory.Exists(OutputDir);
};
