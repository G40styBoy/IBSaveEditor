using Xunit.Abstractions;

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
        };

        public static IEnumerable<object[]> PackageRecognitionSubjects =>
            Packages.Select(p => new object[] { p.FilePath, p.Game });

        public static IEnumerable<object[]> PackageFilePaths =>
            Packages.Select(p => new object[] { p.FilePath });

        [Theory]
        [MemberData(nameof(PackageRecognitionSubjects))]
        public void PackageRecognitionTest(string filePath, Game expectedGame)
        {
            var package = new UnrealPackage(filePath);
            Assert.Equal(expectedGame, package.game);
        }

        [Theory]
        [MemberData(nameof(PackageFilePaths))]
        public void PackageDeserializationTest(string filePath)
        {
            var package = new UnrealPackage(filePath);
            var properties = package.DeserializeUPK();
            Assert.NotNull(properties);
            Assert.NotEmpty(properties);
        }
    }
}
