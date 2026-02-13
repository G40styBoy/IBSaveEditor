namespace SaveDumper;

internal class Program
{
    private static string? inputPath;
    private static bool debug = false;

    public static void Main()
    {
        if (debug)
        {
            DebugMain();
            return;
        }

        Console.Title = "IBSaveDumper";

        while (true)
        {
            Console.Clear();
            PrintBanner();

            inputPath = PromptForFile(
                "Drag and drop a .bin save file: ",
                ".bin",
                "Invalid file. Please drag a valid .bin file: ",
                "Only .bin files are accepted. Please try again: "
            );

            Console.Clear();
            PrintBanner();
            Console.WriteLine("Processing save package...\n");

            FilePaths.ValidateOutputDirectory();
            UnrealPackage upk;
            try
            {
                upk = new UnrealPackage(inputPath);
                RunDeserialization(upk);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                WaitAndRestart();
                continue;
            }

            string jsonPath = PromptForFile(
                "Now drag and drop the modified .json file to repackage: ",
                ".json",
                "Invalid file. Please drag a valid .json file: ",
                "Only .json files are accepted. Please try again: "
            );

            Console.Clear();
            PrintBanner();
            Console.WriteLine("Processing save data...\n");

            try
            {
                RunSerialization(upk.info, jsonPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            WaitAndRestart();
        }
    }

    private static void RunSerialization(PackageInfo info, string jsonPath)
    {
        ProgressBar.Run("Serializing", () =>
        {
            var cruncher = new JsonDataCruncher(jsonPath, info.game);
            var crunchedData = cruncher.ReadJsonFile();
            if (crunchedData is null)
                throw new InvalidOperationException("Serialization process failed!");

            using (var serializer = new Serializer(info, crunchedData))
                serializer.SerializeAndOutputData();
        });
    }

    private static void RunDeserialization(UnrealPackage upk)
    {
        ProgressBar.Run("Deserializing", () =>
        {
            List<UProperty> propertyList = upk.DeserializeUPK();
            if (propertyList is null)
                throw new InvalidOperationException("Deserialization process failed!");

            using (var JsonDataParser = new JsonDataParser(propertyList))
                JsonDataParser.WriteDataToFile(upk.info.game);

            upk.Dispose();
        });
    }

    /// <summary>
    /// For development testing
    /// </summary>
    private static void DebugMain()
    {
        Console.ReadKey();
    }


    private static string PromptForFile(string prompt, string requiredExtension, string invalidMessage, string wrongExtensionMessage)
    {
        string? path;
        bool showingError = false;

        while (true)
        {
            int promptLine = Console.CursorTop;

            if (!showingError)
                Console.Write(prompt);

            path = Console.ReadLine()?.Trim('"');

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                ClearAndShowError(promptLine, invalidMessage);
                showingError = true;
                continue;
            }

            if (Path.GetExtension(path).ToLowerInvariant() != requiredExtension)
            {
                ClearAndShowError(promptLine, wrongExtensionMessage);
                showingError = true;
                continue;
            }

            break;
        }
        return path!;
    }

    private static void ClearAndShowError(int startLine, string message)
    {
        // clear from the start line downward (3 lines should be enough)
        for (int i = 0; i < 3; i++)
        {
            Console.SetCursorPosition(0, startLine + i);
            Console.Write(new string(' ', Console.WindowWidth - 1));
        }

        Console.SetCursorPosition(0, startLine);
        Console.Write(message);
    }

    private static void PrintBanner()
    {
        Util.PrintColoredLine("========================================", ConsoleColor.Cyan, true);
        Util.PrintColoredLine("         SAVE DUMPER TOOL ALPHA          ", ConsoleColor.Cyan, true);
        Util.PrintColoredLine("========================================", ConsoleColor.Cyan, true);
        Util.PrintColoredLine(" © 2026 G40sty. All rights reserved.\n", ConsoleColor.DarkGray, true);
    }

    private static void WaitAndRestart()
    {
        Console.WriteLine("\nPress any key to continue...");
        Console.ReadKey(true);
        Console.Clear();
    }
}
