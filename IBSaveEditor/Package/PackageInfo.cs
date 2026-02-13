/// <summary>
/// Represents the immutable header and identity metadata of a parsed Unreal package.
/// 
/// This type encapsulates the core information extracted from a save file,
/// including its name, encryption state, version, magic identifier, and
/// resolved game type. It serves as a stable snapshot of package-level
/// metadata that can be shared across deserialization, serialization, and
/// cryptographic operations without exposing the full <see cref="UnrealPackage"/>.
/// </summary>
public sealed class PackageInfo
{
    private string? _packageName;
    public string packageName => _packageName ?? throw new InvalidOperationException("PackageName not set.");
    public void SetPackageName(string value)
    {
        if (_packageName is not null) throw new InvalidOperationException("PackageName already set.");
        _packageName = value;
    }

    private bool? _isEncrypted;
    public bool isEncrypted => _isEncrypted ?? throw new InvalidOperationException("IsEncrypted not set.");
    public void SetIsEncrypted(bool value)
    {
        if (_isEncrypted is not null) throw new InvalidOperationException("IsEncrypted already set.");
        _isEncrypted = value;
    }

    private uint? _saveVersion;
    public uint saveVersion => _saveVersion ?? throw new InvalidOperationException("SaveVersion not set.");
    public void SetSaveVersion(uint value)
    {
        if (_saveVersion is not null) throw new InvalidOperationException("SaveVersion already set.");
        _saveVersion = value;
    }

    private uint? _saveMagic;
    public uint saveMagic => _saveMagic ?? throw new InvalidOperationException("SaveMagic not set.");
    public void SetSaveMagic(uint value)
    {
        if (_saveMagic is not null) throw new InvalidOperationException("SaveMagic already set.");
        _saveMagic = value;
    }

    private Game? _game;
    public Game game => _game ?? throw new InvalidOperationException("Game not set.");
    public void SetGame(Game value)
    {
        if (_game is not null) throw new InvalidOperationException("Game already set.");
        _game = value;
    }
}
