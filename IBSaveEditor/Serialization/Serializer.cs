using IBSaveEditor.UProperties;
using IBSaveEditor.Wrappers;
using IBSaveEditor.Util;
using IBSaveEditor.Package;

namespace IBSaveEditor.Serialize;

/// <summary>
/// Converts crunched JSON data back into the binary format expected by the game's
/// <c>BasicLoadObject</c> pipeline, then writes it to a <c>.bin</c> file in OUTPUT.
/// Handles encryption for all supported games.
/// </summary>
internal sealed class Serializer : IDisposable
{
    private const string EXTENSION = ".bin";

    private readonly PackageInfo        _info;
    private readonly List<UProperty>    _crunchedData;
    private readonly string             _outputPath;

    private UnrealStream       _stream;
    private UnrealBinaryWriter _writer;

    public Serializer(PackageInfo info, List<UProperty> crunchedData)
    {
        _info         = info;
        _crunchedData = crunchedData;
        _outputPath   = Path.Combine(ToolPaths.OutputDir, $"{info.packageName}{EXTENSION}");

        _stream = new UnrealStream(_outputPath, FileMode.Create, FileAccess.ReadWrite);
        _writer = new UnrealBinaryWriter(_stream);
    }

    /// <summary>
    /// Serializes all crunched data to disk, then encrypts the output if the
    /// original package was encrypted.
    /// </summary>
    /// <returns>True on success.</returns>
    /// <exception cref="InvalidOperationException">Thrown if any step of the pipeline fails.</exception>
    public bool SerializeAndOutputData()
    {
        try
        {
            WriteHeader();
            WriteProperties();

            _writer.Flush();

            if (_info.isEncrypted)
                PackageCrypto.EncryptPackage(ref _stream, _info);

            return true;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(ex.Message);
        }
    }

    // Write steps ───────────────────────────────────────────────────────────

    /// <summary>
    /// Writes the package header to the stream.
    /// <para>
    /// Encrypted packages need a placeholder header written before the property
    /// data so the game can identify the file type and version when loading.
    /// <c>EncryptPackage</c> later replaces the placeholder with the real header.
    /// Unencrypted packages write their version and magic values directly.
    /// </para>
    /// </summary>
    /// <exception cref="InvalidDataException">
    /// Thrown when the package is encrypted but its game type has no supported header layout.
    /// </exception>
    private void WriteHeader()
    {
        if (_info.isEncrypted)
        {
            WriteEncryptedHeader();
            return;
        }

        // Unencrypted: write version and magic directly
        _writer.Write(_info.saveVersion);
        _writer.Write(_info.saveMagic);
    }

    /// <summary>
    /// Writes the pre-encryption header placeholder.
    /// <para>
    /// IB1 and IB3 write only a single NO_MAGIC field.
    /// IB2 and VOTE write a zero field followed by NO_MAGIC.
    /// </para>
    /// </summary>
    private void WriteEncryptedHeader()
    {
        switch (_info.game)
        {
            case Game.IB1 or Game.IB3:
                _writer.Write(PackageConstants.NO_MAGIC);
                break;

            case Game.IB2 or Game.VOTE:
                _writer.Write(0);
                _writer.Write(PackageConstants.NO_MAGIC);
                break;

            default:
                throw new InvalidDataException(
                    $"Encrypted game type '{_info.game}' has no supported header layout.");
        }
    }

    /// <summary>
    /// Writes all crunched properties to the stream, followed by the
    /// "None" terminator required by the UE3 property list format.
    /// </summary>
    private void WriteProperties()
    {
        foreach (var property in _crunchedData)
        {
            _writer.WritePropertyMetadata(property);
            property.SerializeValue(_writer);
        }

        // Every UE3 property list ends with a "None".
        _writer.WriteUnrealString(UType.NONE);
    }

    public void Dispose()
    {
        _writer.Dispose();
        _stream.Dispose();
    }
}