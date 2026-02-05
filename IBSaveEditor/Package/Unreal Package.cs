using SaveDumper.Deserializer;
using SaveDumper.UnrealPackageManager.Crypto;

public class UnrealPackage : IDisposable
{
    internal string filePath;
    private FileStream fileStream;
    private BinaryReader binaryReader;
    private BinaryWriter binaryWriter;
    internal PackageData packageData;

    public record struct PackageData
    {
        public string packageName;
        public bool bisEncrypted;
        public uint saveVersion;
        public uint saveMagic;
        /// <summary>
        /// Houses our stream bytes for encrypted streams, otherwise null.
        /// </summary>
        public byte[]? streamBytes;
        public Game game;
    }

    /// <summary>
    /// Default Unreal Package constructor
    /// </summary>
    /// <param name="filePath">File to read the package from</param>
    public UnrealPackage(string filePath)
    {
        this.filePath = filePath;
        if (!File.Exists(filePath))
            throw new FileNotFoundException("The specified file does not exist.", filePath);

        packageData = new PackageData();
        packageData.packageName = Path.GetFileNameWithoutExtension(filePath);
        try
        {

            fileStream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
            InitializeBinaryReadWrite();
            GetPackageHeaderInfo();
            ResolvePackageInfo();
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(exception.Message);
        }
    }

    /// <summary>
    /// Constructor implemented for encrypted packages.
    /// </summary>
    public UnrealPackage(PackageData packageData)
    {
        this.packageData = packageData;
        filePath = $@"{FilePaths.OutputDir}/{Path.GetRandomFileName()}";

        try
        {
            fileStream = File.Create(filePath, 0, FileOptions.DeleteOnClose);
            InitializeBinaryReadWrite();
            TransformEncryptedPackage();
        }
        catch (Exception exception)
        {
            Dispose();
            throw new Exception(exception.Message);
        }
    }

    /// <returns>A boolean value that indicates whether the stream has reached the end of the file</returns>
    internal bool IsEndFile() => fileStream.Position >= fileStream.Length;

    /// <summary>
    /// Sets the stream position to the specified amount
    /// </summary>
    /// <param name="position">position to set</param>
    public void SetStreamPosition(long position) => fileStream.Position = position;
    /// <summary>
    /// Sets the stream position to 0
    /// </summary>
    public void ResetStreamPosition() => fileStream.Position = 0;

    /// <summary>
    /// Attempts to collect as much data as possible from the header data of the file.
    /// Also invokes decryption of certian packages if needed
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    private void ResolvePackageInfo()
    {
        try
        {
            if (PackageCrypto.IsPackageEncrypted(packageData))
            {
                packageData.bisEncrypted = true;

                if (packageData.saveMagic == PackageCrypto.IB1_SAVE_MAGIC)
                    packageData.game = Game.IB1;

                else if (packageData.saveVersion == PackageCrypto.IB2_SAVE_MAGIC)
                {
                    SetStreamPosition(sizeof(int));
                    packageData.game = PackageCrypto.TryDecryptHalfBlock(Game.IB2, fileStream)
                        ? Game.IB2
                        : Game.VOTE;
                }
                packageData.streamBytes = GetFileBytes();
            }
            else
            {
                packageData.game = packageData.saveVersion switch
                {
                    PackageCrypto.SAVE_FILE_VERSION_IB3 => Game.IB3,
                    PackageCrypto.SAVE_FILE_VERSION_PC => Game.IB1,
                    _ => throw new InvalidOperationException("Unknown save version.")
                };
            }
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"Package Data population failed. Reason: {exception.Message}", exception);
        }
    }


    /// <summary>
    /// Populates the info for our package header so we can use it to determine save type.
    /// </summary>
    private void GetPackageHeaderInfo()
    {
        packageData.saveVersion = DeserializeUInt();
        packageData.saveMagic = DeserializeUInt();
        ResetStreamPosition();
    }

    private void InitializeBinaryReadWrite()
    {
        try
        {
            binaryReader = new BinaryReader(fileStream, Encoding.UTF8);
            binaryWriter = new BinaryWriter(fileStream, Encoding.UTF8);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(exception.Message);
        }
    }

    private byte[] GetFileBytes()
    {
        if (fileStream is null)
            return new byte[0];

        fileStream.Position = 0;
        using (var memoryStream = new MemoryStream())
        {
            fileStream.CopyTo(memoryStream);
            return memoryStream.ToArray();
        }
    }

    /// <summary>
    /// Reverts the stream position during deserialization given a value and its type
    /// </summary>
    public void RevertStreamPosition(string value)
    {
        // Right now this only supports strings, but for now we don't need to revert any other type
        fileStream.Position -= sizeof(int) + sizeof(byte); // size + nt
        fileStream.Position -= value.Length;
    }

    internal void TransformEncryptedPackage(bool outputDecryptedFile = false)
    {
        try
        {
            byte[] decryptedData = PackageCrypto.DecryptPackage(this);
            fileStream.Write(decryptedData, 0, decryptedData.Length);
            if (outputDecryptedFile)
                File.WriteAllBytes($@"{FilePaths.OutputDir}/Decrypted.bin", decryptedData);    
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException($"Failed to decrypt package. {exception.Message}");
        }
    }

    /// <returns>the next string in the stream</returns>
    internal string PeekString()
    {
        string str;
        long originalPosition = binaryReader.BaseStream.Position;

        try
        {
            str = DeserializeString();
        }
        finally
        {
            binaryReader.BaseStream.Position = originalPosition;
        }

        return str;
    }

    internal string DeserializeString()
    {
        try
        {
            var strLength = binaryReader.ReadInt32();
            if (strLength <= 0)
                return string.Empty;

            var bytes = binaryReader.ReadBytes(strLength);
            return Encoding.UTF8.GetString(bytes).TrimEnd('\0');
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException($"Failure deserializing string inside an Unreal Package. {exception}");
        }
    }

    internal object DeserializeByteProperty()
    {
        string enumName = DeserializeString();

        if (enumName == UType.NONE)
            return DeserializeByte();
        else
        {
            string enumValue = DeserializeString();
            return new KeyValuePair<string, string>(enumName, enumValue);
        }
    }

    internal int DeserializeInt()
    {
        try
        {
            int _buffer = binaryReader.ReadInt32();

            if (int.IsNegative(_buffer) && _buffer != -1)
                return int.MaxValue;

            return _buffer;
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new ArgumentOutOfRangeException($"Could not convert {sizeof(int)} bytes to an integer.");
        }
    }

    internal uint DeserializeUInt()
    {
        try
        {
            return binaryReader.ReadUInt32();
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new ArgumentOutOfRangeException($"Could not convert {sizeof(int)} bytes to an integer.");
        }
    }


    internal float DeserializeFloat()
    {
        try
        {
            float _buffer = binaryReader.ReadSingle();

            if (float.IsNaN(_buffer) || float.IsInfinity(_buffer))
                throw new InvalidDataException($"Invalid float value: {_buffer}");

            return _buffer;
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new ArgumentOutOfRangeException($"Could not convert {sizeof(float)} byte to a float.");
        }
    }

    internal bool DeserializeBool()
    {
        try
        {
            return binaryReader.ReadBoolean();
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new ArgumentOutOfRangeException($"Could not convert {sizeof(bool)} to a bool.");
        }
    }
    internal byte DeserializeByte()
    {
        try
        {
            return binaryReader.ReadByte();
        }
        catch (Exception)
        {
            throw new InvalidOperationException($"Could not read byte inside of Unreal Package.");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ReadOnlySpan<byte> Deserialize(int count) => binaryReader.ReadBytes(count);

    /// <summary>
    /// Deserializes a package's contents into a list of UProperties
    /// </summary>
    /// <returns>A list of all UProperties inside of the package</returns>
    /// <exception cref="InvalidOperationException">Deserialization was a failure</exception>
    public List<UProperty> DeserializeUPK()
    {
        const int ENCRYPTED_IB1_HEADER = 4;
        const int HEADER_SIZE = 8;
        try
        {
            if (packageData.game is Game.IB1 && packageData.bisEncrypted is true)
                SetStreamPosition(ENCRYPTED_IB1_HEADER);
            else
                SetStreamPosition(HEADER_SIZE);

            var deserializer = new UPKDeserializer(packageData.game);
            var uProperties = deserializer.DeserializePackage(this);
            return uProperties;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(exception.Message);
        }
    }

    public void Close()
    {
        binaryReader?.Close();
        binaryWriter?.Close();
        fileStream?.Close();
    }

    public void Dispose()
    {
        binaryReader?.Dispose();
        binaryWriter?.Dispose();
        fileStream?.Dispose();
        GC.SuppressFinalize(this);
    }

    ~UnrealPackage() => Dispose();
}