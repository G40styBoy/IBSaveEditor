namespace IBSaveEditor;

public sealed class Program
{
    private const string TITLE = "IBSaveDumper";
    private const string BANNER_NAME = "SAVE DUMPER TOOL ALPHA";
    private const string BIN_FILE_EXT = ".bin";
    private const string JSON_FILE_EXT = ".json";

    public static void Main()
    {
        Console.Title = TITLE;

        while (true)
        {
            Util.RefreshConsole(BANNER_NAME);
            try
            {
                FilePaths.ValidateOutputDirectory();

                string path = PromptForExistingFile(
                    "Drop a .bin (deserialize) or .json (repackage), then press Enter: ",
                    "Invalid file. Drop a real file path: ",
                    "Only .bin or .json are accepted. Try again: "
                );

                Util.RefreshConsole(BANNER_NAME);
                string ext = Path.GetExtension(path).ToLowerInvariant();

                if (ext == BIN_FILE_EXT)
                {
                    RunBinToJson(path);
                    Util.PrintSuccess("Done. JSON written to OUTPUT.");
                }
                else if (ext == JSON_FILE_EXT)
                {
                    RunJsonToBin(path);
                    Util.PrintSuccess("Done. Package written to OUTPUT.");
                }
                else
                    throw new InvalidOperationException("Only .bin or .json are accepted.");
                
            }
            catch (Exception ex)
            {
                Util.PrintFailure(ex);
            }

            Util.Restart();
        }
    }

    private static void RunBinToJson(string binPath)
    {
        Console.WriteLine("Processing .bin package...\n");

        using var upk = new UnrealPackage(binPath);
        Util.PrintStep("Loading Package", "OK");

        List<UProperty> properties = upk.DeserializeUPK();
        if (properties is null || properties.Count == 0)
            throw new InvalidOperationException("Deserialization failed: no properties were produced.");
        Util.PrintStep("Deserializing Package", "OK");


        using var parser = new JsonDataParser(properties, upk.info);
        parser.WriteDataToFile();
        Util.PrintStep("Writing JSON", "OK");
    }

    private static void RunJsonToBin(string jsonPath)
    {
        Util.PrintColoredLine("Processing .json data...", ConsoleColor.Yellow);

        EnvelopeMeta meta = JsonUtils.ReadMeta(jsonPath);
        var info = new PackageInfo();
        info.SetPackageName(meta.PackageName);
        info.SetGame(meta.Game);
        info.SetIsEncrypted(meta.IsEncrypted);
        info.SetSaveVersion(meta.SaveVersion);
        info.SetSaveMagic(meta.SaveMagic);
        Util.PrintStep("Loading Metadata", "OK");

        string dataJson = JsonUtils.ExtractDataObjectJson(jsonPath, "data");

        var cruncher = new JsonDataCruncher(dataJson, info.game); 
        var crunchedData = cruncher.ReadJsonFile();               
        if (crunchedData is null)
            throw new InvalidOperationException("Serialization failed: JSON could not be parsed into save data.");
        Util.PrintStep("Crunching JSON Data", "OK");

        using var serializer = new Serializer(info, crunchedData);
        serializer.SerializeAndOutputData();
        Util.PrintStep("Serializing Package", "OK");
    }

    private static string PromptForExistingFile(string prompt, string invalidMessage, string wrongExtensionMessage)
    {
        while (true)
        {
            Console.Write(prompt);

            string? input = Console.ReadLine();
            string? path = input?.Trim().Trim('"');

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                Util.PrintInlineError(invalidMessage); 
                continue;
            }

            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext != BIN_FILE_EXT && ext != JSON_FILE_EXT)
            {
                Util.PrintInlineError(wrongExtensionMessage);
                continue;
            }

            return path;
        }
    }
}