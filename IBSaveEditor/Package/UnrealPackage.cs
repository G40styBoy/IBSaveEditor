using IBSaveEditor.UProperties;
using IBSaveEditor.Serialize;
using IBSaveEditor.Wrappers;
using System.Text;

namespace IBSaveEditor.Package;
/// <summary>
/// Class managing all unreal packages encounterable. 
/// </summary>
public class UnrealPackage : IDisposable
{
    private UnrealStream _stream;
    private UnrealBinaryReader _reader;
    
    public UnrealStream Stream => _stream;
    public UnrealBinaryReader Reader => _reader;

    public PackageInfo info { get; private set; } = null!;

    public UnrealPackage(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("The specified file does not exist.", filePath);

        info = new PackageInfo();
        info.SetPackageName(Path.GetFileNameWithoutExtension(filePath));

        try
        {
            _stream = new UnrealStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
            _reader = new UnrealBinaryReader(_stream, Encoding.UTF8, leaveOpen: true);
            GetHeaderinfo();
            GetPackageType();
        }
        catch (Exception ex)
        {
            Dispose();
            throw new InvalidOperationException($"Failed to construct UnrealPackage for '{filePath}\n{ex.Message}'.", ex);
        }
    }

    /// <summary>
    /// Gets the header info for a package.
    /// </summary>
    private void GetHeaderinfo()
    {
        _stream.Position = 0;
        info.SetSaveVersion(_reader.DeserializeUInt());
        info.SetSaveMagic(_reader.DeserializeUInt());
        _stream.Position = 0;
    }

    /// <summary>
    /// Attempts to resolve a package's game type (IB1, IB2, IB3, VOTE) based on its header metadata.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the package type cannot be determined.</exception>
    private void GetPackageType()
    {
        try
        {
            Game game;

            if (IsPackageEncrypted())
            {
                info.SetIsEncrypted(true);
                game = ResolveEncryptedGame();
            }
            else
            {
                info.SetIsEncrypted(false);
                game = ResolveDecryptedGame();
            }

            info.SetGame(game);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Package type resolution failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Resolves the game type for an encrypted package using save magic and version values.
    /// IB2 and VOTE share the same save magic, so a half-block decryption attempt is used to distinguish them.
    /// </summary>
    private Game ResolveEncryptedGame()
    {
        if (info.saveMagic == PackageConstants.IB1_SAVE_MAGIC)
            return Game.IB1;

        if (info.saveVersion == PackageConstants.IB2_SAVE_MAGIC)
        {
            _stream.SetPosition(sizeof(int));
            return PackageCrypto.TryDecryptHalfBlock(Game.IB2, _stream) ? Game.IB2 : Game.VOTE;
        }

        if (info.saveVersion == PackageConstants.IB3_SAVE_MAGIC)
            return Game.IB3;

        throw new InvalidDataException("Could not determine encrypted package type from header metadata.");
    }

    /// <summary>
    /// Resolves the game type for an unencrypted package.
    /// IB3 and unencrypted IB2 share the same save version, so a content check is used to distinguish them.
    /// </summary>
    private Game ResolveDecryptedGame()
    {
        if (info.saveVersion == PackageConstants.SAVE_FILE_VERSION_IB3)
            return IsPackageIB3() ? Game.IB3 : Game.IB2;

        if (info.saveVersion == PackageConstants.SAVE_FILE_VERSION_PC)
            return Game.IB1;

        throw new InvalidDataException("Could not determine decrypted package type from header metadata.");
    }

    /// <summary>
    /// Decrypts and unreal package and replaces all of the necessary data.
    /// </summary>
    public void DecryptPackage()
    {
        try
        {
            byte[] decryptedData = PackageCrypto.DecryptPackage(this);
    
            _stream.Dispose();
            _stream = new UnrealStream(decryptedData);
            _reader = new UnrealBinaryReader(_stream, Encoding.UTF8, leaveOpen: true);   
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to decrypt Unreal Package: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Checks the first 8 bytes to determine the package's encryption state
    /// </summary>
    /// <returns>Returns if the package is encrypted or not</returns>
    public bool IsPackageEncrypted() =>
         !(info.saveVersion == PackageConstants.SAVE_FILE_VERSION_IB3 || info.saveVersion == PackageConstants.SAVE_FILE_VERSION_PC)
         || info.saveMagic != PackageConstants.NO_MAGIC;

    /// <summary>
    /// Verifies if an unencrypted package is a IB3 save.
    /// </summary>
    private bool IsPackageIB3()
    {
        if (info.isEncrypted) return false;

        const string STRING_TO_CHECK = "CurrentEngineVersion";
        const int ENGINE_VERSION_LOCATION = 62;
        //checking here to see if "CurrentEngineVersion" is present
        _stream.SetPosition(_stream.Length - ENGINE_VERSION_LOCATION);
        return _reader.DeserializeString() is STRING_TO_CHECK;
    }

    /// <summary>
    /// Deserializes a package's contents into a list of UProperty's
    /// </summary>
    /// <returns>A list of all UProperties inside of a <see cref="UnrealPackage"/></returns>
    public List<UProperty> ReadProperties()
    {
        const int ENCRYPTED_HEADER = 4;
        const int HEADER_SIZE = 8;

        if (info.isEncrypted)
            DecryptPackage();

        _stream.SetPosition(info.isEncrypted && info.game is Game.IB1 or Game.IB3
            ? ENCRYPTED_HEADER
            : HEADER_SIZE);

        return new Deserializer(this).DeserializePackage();
    }

    public void Dispose()
    {
        _reader?.Dispose();
        _stream?.Dispose();
    }
}