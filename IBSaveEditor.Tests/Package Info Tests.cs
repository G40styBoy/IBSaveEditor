using IBSaveEditor.Package;

namespace IBSaveEditor.Tests
{
    /// <summary>
    /// PackageInfo fields are write-once by design (see CLAUDE.md). These tests
    /// pin that behavior down so it can't be "relaxed" by accident.
    /// </summary>
    public class PackageInfoTests
    {
        [Fact]
        public void UnsetFields_ThrowOnRead()
        {
            var info = new PackageInfo();
            Assert.Throws<InvalidOperationException>(() => info.packageName);
            Assert.Throws<InvalidOperationException>(() => info.isEncrypted);
            Assert.Throws<InvalidOperationException>(() => info.saveVersion);
            Assert.Throws<InvalidOperationException>(() => info.saveMagic);
            Assert.Throws<InvalidOperationException>(() => info.game);
        }

        [Fact]
        public void SetPackageName_Twice_Throws()
        {
            var info = new PackageInfo();
            info.SetPackageName("Save");
            Assert.Throws<InvalidOperationException>(() => info.SetPackageName("Other"));
        }

        [Fact]
        public void SetIsEncrypted_Twice_Throws()
        {
            var info = new PackageInfo();
            info.SetIsEncrypted(true);
            Assert.Throws<InvalidOperationException>(() => info.SetIsEncrypted(false));
        }

        [Fact]
        public void SetSaveVersion_Twice_Throws()
        {
            var info = new PackageInfo();
            info.SetSaveVersion(5);
            Assert.Throws<InvalidOperationException>(() => info.SetSaveVersion(4));
        }

        [Fact]
        public void SetSaveMagic_Twice_Throws()
        {
            var info = new PackageInfo();
            info.SetSaveMagic(123u);
            Assert.Throws<InvalidOperationException>(() => info.SetSaveMagic(456u));
        }

        [Fact]
        public void SetGame_Twice_Throws()
        {
            var info = new PackageInfo();
            info.SetGame(Game.IB3);
            Assert.Throws<InvalidOperationException>(() => info.SetGame(Game.IB2));
        }

        [Fact]
        public void SetThenGet_ReturnsExactValueForEveryField()
        {
            var info = new PackageInfo();
            info.SetPackageName("MySave");
            info.SetIsEncrypted(true);
            info.SetSaveVersion(5);
            info.SetSaveMagic(541812089u);
            info.SetGame(Game.IB3);

            Assert.Equal("MySave", info.packageName);
            Assert.True(info.isEncrypted);
            Assert.Equal(5u, info.saveVersion);
            Assert.Equal(541812089u, info.saveMagic);
            Assert.Equal(Game.IB3, info.game);
        }
    }
}
