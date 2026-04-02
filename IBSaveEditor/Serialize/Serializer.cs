using IBSaveEditor.UProperties;
using IBSaveEditor.Wrappers;
using IBSaveEditor.Util;
using IBSaveEditor.Package;

namespace IBSaveEditor.Serialize;
/// <summary>
/// Takes crunched Json data and converts it into serialized data readable for BasicLoadObject
/// This gets written out into a binary file
/// </summary>
class Serializer : IDisposable
{
    private const string DEFAULT_NAME = "UnencryptedSave0";
    private const string EXTENSION = ".bin";

    private readonly List<UProperty> crunchedData;
    private readonly string outputPath;

    private UnrealBinaryWriter _writer;
    private Stream _stream;
    private PackageInfo info;

    public Serializer(PackageInfo info, List<UProperty> crunchedData)
    {
        string fileName = $@"{info.packageName}{EXTENSION}";
        outputPath = Path.Combine(ToolPaths.OutputDir, fileName);

        _stream = new FileStream(outputPath, FileMode.Create, FileAccess.ReadWrite);
        _writer = new UnrealBinaryWriter(_stream);
        this.info = info;
        this.crunchedData = crunchedData;
    }

    public bool SerializeAndOutputData()
    {
        try
        {
            SerializePackageHeader();
            foreach (var uProperty in crunchedData)
            {
                _writer.WritePropertyMetadata(uProperty);
                uProperty.SerializeValue(_writer);
            }
            _writer.WriteUnrealString("None");
            _writer.Flush();

            if (info.isEncrypted)
                PackageCrypto.EncryptPackage(ref _stream, info);

            return true;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(ex.Message);
        }
    }

    private void SerializePackageHeader()
    {
        // even encrypted files have header data stored before encryption
        // add here so the file can be read correctly when loading a save
        if (info.isEncrypted)
        {
            switch (info.game)
            {
                case Game.IB1:
                    _writer.Write(PackageConstants.NO_MAGIC);
                    break;
                case Game.IB2 or Game.VOTE:
                    _writer.Write(0);
                    _writer.Write(PackageConstants.NO_MAGIC);
                    break;
                default:
                    throw new InvalidDataException("Package is encrypted but its game type for header population isnt supported.");
            }
            return;
        }

        // write out unencrypted header info
        _writer.Write(info.saveVersion);
        _writer.Write(info.saveMagic);        
    }

    public void Dispose()
    {
        _writer.Dispose();
        _stream.Dispose();
    }
}
