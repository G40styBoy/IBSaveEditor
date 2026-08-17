using IBSaveEditor.Manifest;
using IBSaveEditor.Util;
using IBSaveEditor.ViewModels;
using IBSaveEditor.ViewModels.Fields;

namespace IBSaveEditor.Tests
{
    /// <summary>
    /// Export always writes to the same fixed <c>OUTPUT/{packageName}.bin</c> path and
    /// overwrites unconditionally - fine when the audience was one person iterating on the
    /// tool, not fine for someone who opens their only save, makes a mistake, and exports
    /// over the previous export with no way back. <see cref="ToolPaths.BackupIfExists"/>
    /// closes that gap; this proves the backup actually preserves the old export's real
    /// bytes, not just that a file matching the naming pattern happens to exist.
    /// </summary>
    [Collection("Save fixture files")]
    public class ExportBackupTests
    {
        private static readonly string FILES = TestPathways.GetFileLocation();

        [Fact]
        public void ExportingOverAnExistingFile_BacksUpThePreviousExportContentIntact()
        {
            var sourcePath   = Path.Combine(FILES, "Unencrypted IB3 Save.bin");
            var packageName  = Path.GetFileNameWithoutExtension(sourcePath);
            var exportedPath = Path.Combine(ToolPaths.OutputDir, $"{packageName}.bin");

            var vm = new MainWindowViewModel();
            vm.LoadFromBin(sourcePath);

            vm.ExportToBin();
            Assert.True(File.Exists(exportedPath));
            var firstExportBytes = File.ReadAllBytes(exportedPath);

            // Change something so the second export's bytes actually differ, then export
            // again over the exact same OUTPUT path.
            var goldSpec = IB3Manifest.Instance.AllFields.Single(f => f.Label == "Gold");
            var field    = new FieldViewModel(goldSpec, vm.CurrentGame, vm.RootNodes);
            field.Value  = Convert.ToInt64(field.Value) + 1;

            vm.ExportToBin();
            var secondExportBytes = File.ReadAllBytes(exportedPath);
            Assert.NotEqual(firstExportBytes, secondExportBytes);

            var backups = Directory.GetFiles(ToolPaths.OutputDir, $"{packageName}.*.bak.bin");
            Assert.NotEmpty(backups);

            var latestBackup = backups.OrderByDescending(File.GetLastWriteTimeUtc).First();
            Assert.Equal(firstExportBytes, File.ReadAllBytes(latestBackup));
        }
    }
}
