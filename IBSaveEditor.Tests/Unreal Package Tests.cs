using System.Diagnostics;
using System.Threading.Tasks.Dataflow;
using Xunit;
using Xunit.Abstractions;

namespace IBSaveEditor.Tests;

public class UnrealPackageTests
{

    const string FILES = @"C:\Users\G40sty\Documents\VS Code\Infinity Blade\IBSaveEditor\IBSaveEditor.Tests\Files";

    public static IEnumerable<object[]> packageRecognitionSubjects =>
        new List<object[]>
        {
            new object[] { $@"{FILES}\Encrypted IB1 Save.bin", Game.IB1 },
            new object[] { $@"{FILES}\Unencrypted IB1 Save.bin", Game.IB1 },
            new object[] { $@"{FILES}\Unencrypted IB2 Save.bin", Game.IB2 },
            new object[] { $@"{FILES}\Encrypted IB2 Save", Game.IB2 },
            new object[] { $@"{FILES}\Unencrypted IB3 Save.bin", Game.IB3 },
            new object[] { $@"{FILES}\Unencrypted VOTE Save.bin", Game.VOTE }
        };

    /// <summary>
    /// Takes a list of packages and tests their recognition.
    /// </summary>
    /// <param name="filePath">File path to package</param>
    /// <param name="expectedGame">Game expected from package recognition</param>
    [Theory]
    [MemberData(nameof(packageRecognitionSubjects))]
    public void PackageRecognitionTest(string filePath, Game expectedGame)
    {
        var package = new UnrealPackage(filePath);
        Assert.Equal(expectedGame, package.packageData.game); 
    }
}
