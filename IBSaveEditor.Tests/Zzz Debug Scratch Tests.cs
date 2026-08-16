using IBSaveEditor.Package;
using IBSaveEditor.Json;

namespace IBSaveEditor.Tests
{
    public class ZzzDebugScratchTests
    {
        [Theory]
        [InlineData("Unencrypted IB1 Save.bin")]
        [InlineData("Unencrypted VOTE Save.bin")]
        public void DumpJsonForDesignResearch(string fileName)
        {
            var path = Path.Combine(TestPathways.GetFileLocation(), fileName);
            using var package = new UnrealPackage(path);
            var properties = package.ReadProperties();
            var json = new JsonDataParser(properties, package.info).ReturnDataAsString();

            var outPath = Path.Combine(Path.GetTempPath(), $"dump_{Path.GetFileNameWithoutExtension(fileName)}.json");
            File.WriteAllText(outPath, json);
        }

        [Fact]
        public void ScanSaveStorage()
        {
            var root = Path.GetFullPath(Path.Combine(TestPathways.GetFileLocation(), "..", "..", "..", "SAVE STORAGE LOCATION"));
            var lines = new List<string>();
            lines.Add($"root={root} exists={Directory.Exists(root)}");

            if (!Directory.Exists(root))
            {
                File.WriteAllLines(Path.Combine(Path.GetTempPath(), "scan_saves.txt"), lines);
                return;
            }

            foreach (var file in Directory.EnumerateFiles(root, "*.bin", SearchOption.AllDirectories).OrderBy(f => f))
            {
                try
                {
                    using var pkg = new UnrealPackage(file);
                    var rel = Path.GetRelativePath(root, file);
                    lines.Add($"OK\t{rel}\tgame={pkg.info.game}\tencrypted={pkg.info.isEncrypted}\tsize={new FileInfo(file).Length}");
                }
                catch (Exception ex)
                {
                    var rel = Path.GetRelativePath(root, file);
                    lines.Add($"FAIL\t{rel}\t{ex.Message.Replace('\n', ' ').Replace('\r', ' ')}");
                }
            }

            File.WriteAllLines(Path.Combine(Path.GetTempPath(), "scan_saves.txt"), lines);
        }
    }
}
