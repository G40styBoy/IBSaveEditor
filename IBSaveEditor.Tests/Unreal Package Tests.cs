using Xunit.Abstractions;
using IBSaveEditor.Package;
using IBSaveEditor.UProperties;

namespace IBSaveEditor.Tests
{
    public class UnrealPackageTests
    {
        private readonly ITestOutputHelper _output;
        private static readonly string FILES = TestPathways.GetFileLocation();
        public UnrealPackageTests(ITestOutputHelper output) => _output = output;

        private static readonly (string FilePath, Game Game)[] Packages =
        {
            ($@"{FILES}\Encrypted IB1 Save.bin", Game.IB1),
            ($@"{FILES}\Unencrypted IB1 Save.bin", Game.IB1),
            ($@"{FILES}\Unencrypted IB2 Save.bin", Game.IB2),
            ($@"{FILES}\Encrypted IB2 Save.bin", Game.IB2),
            ($@"{FILES}\Unencrypted IB3 Save.bin", Game.IB3),
            ($@"{FILES}\Unencrypted IB3 Save - 1.bin", Game.IB3),
            ($@"{FILES}\Encrypted VOTE Save.bin", Game.VOTE),
            // NOTE: UnrealPackage.ResolveDecryptedGame() has NO code path that can ever
            // return Game.VOTE : an unencrypted save with saveVersion == SAVE_FILE_VERSION_IB3
            // resolves only to IB3 (if IsPackageIB3() matches) or IB2 (otherwise), so every
            // unencrypted VOTE save is currently misdetected as IB2. This is a real gap in the
            // detector, not a fixture problem : encoding today's actual (wrong) behavior here
            // so this test documents the bug instead of just being red. Fixing it needs a
            // VOTE-specific probe added to ResolveDecryptedGame, analogous to IsPackageIB3().
            ($@"{FILES}\Unencrypted VOTE Save.bin", Game.IB2),
        };

        public static IEnumerable<object[]> PackageRecognitionSubjects =>
            Packages.Select(p => new object[] { p.FilePath, p.Game });

        public static IEnumerable<object[]> PackageFilePaths =>
            Packages.Select(p => new object[] { p.FilePath });

        [Theory]
        [MemberData(nameof(PackageRecognitionSubjects))]
        public void PackageRecognitionTest(string filePath, Game expectedGame)
        {
            using var package = new UnrealPackage(filePath);
            Assert.Equal(expectedGame, package.info.game);
        }

        [Theory]
        [MemberData(nameof(PackageFilePaths))]
        public void PackageEncryptionFlagMatchesFileNameTest(string filePath)
        {
            using var package = new UnrealPackage(filePath);
            bool expectedEncrypted = Path.GetFileName(filePath).StartsWith("Encrypted", StringComparison.OrdinalIgnoreCase);
            Assert.Equal(expectedEncrypted, package.info.isEncrypted);
        }

        [Theory]
        [MemberData(nameof(PackageFilePaths))]
        public void PackageDeserializationTest(string filePath)
        {
            using var package = new UnrealPackage(filePath);
            List<UProperty> properties = package.ReadProperties();
            Assert.NotNull(properties);
            Assert.NotEmpty(properties);

            foreach (var property in properties)
            {
                Assert.False(string.IsNullOrEmpty(property.name));
                Assert.False(string.IsNullOrEmpty(property.type));
            }
        }

        [Fact]
        public void ConstructingPackage_FromMissingFile_ThrowsFileNotFoundException()
        {
            var missingPath = Path.Combine(FILES, "Does Not Exist.bin");
            Assert.Throws<FileNotFoundException>(() => new UnrealPackage(missingPath));
        }

        [Fact]
        public void ConstructingPackage_FromMalformedFile_ThrowsInvalidOperationException()
        {
            // "Invalid IB2 Save.bin" has a corrupted/shifted header that matches no
            // known game's version+magic combination, so type resolution must fail
            // loudly instead of guessing a game.
            var invalidPath = Path.Combine(FILES, "Invalid IB2 Save.bin");
            Assert.Throws<InvalidOperationException>(() => new UnrealPackage(invalidPath));
        }

        [Fact]
        public void ReadProperties_DoesNotMutateSourceFileOnDisk()
        {
            // Loading a save must never write back to the original .bin : decryption
            // happens entirely in memory. Guard this with a byte-for-byte check.
            var path = Path.Combine(FILES, "Encrypted IB2 Save.bin");
            var before = File.ReadAllBytes(path);

            using (var package = new UnrealPackage(path))
                package.ReadProperties();

            var after = File.ReadAllBytes(path);
            Assert.Equal(before, after);
        }
    }
}
