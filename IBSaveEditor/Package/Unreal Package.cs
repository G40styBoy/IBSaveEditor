using SaveDumper.Deserializer;
using SaveDumper.UnrealPackageManager.Crypto;

public class UnrealPackage : IDisposable
{
    private string filePath { get; init; }
    private Stream _stream;
    private BinaryReader _reader;
    private BinaryWriter _writer;

    public string packageName { get; private set; }
    public bool isEncrypted { get; private set; }
    public uint saveVersion { get; private set; }
    public uint saveMagic { get; private set; }
    public Game game { get; private set; }

    /// <summary>
    /// Default Unreal Package constructor
    /// </summary>
    public UnrealPackage(string filePath)
    {
        this.filePath = filePath;
        if (!File.Exists(filePath))
            throw new FileNotFoundException("The specified file does not exist.", filePath);

        packageName = Path.GetFileNameWithoutExtension(filePath);
        try
        {
            _stream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
            InitializeReaders();
            GetPackageHeaderInfo();
            ResolvePackageInfo();
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(exception.Message);
        }
    }

    private void InitializeStreamFromBytes(byte[] data)
    {
        if (_stream is not null)
            DisposeStream();
        _stream = new MemoryStream(data, writable: true);
        InitializeReaders();
    }

    private void InitializeReaders()
    {
        _reader = new BinaryReader(_stream, Encoding.UTF8, leaveOpen: true);
        _writer = new BinaryWriter(_stream, Encoding.UTF8, leaveOpen: true);
    }


    /// <summary>
    /// Returns a copy of the underlying stream's bytes without altering its final position.
    /// </summary>
    public byte[] GetStreamBytes()
    {
        if (_stream == null)
            throw new InvalidOperationException("Stream is not initialized.");

        if (_stream is MemoryStream ms)
            return ms.ToArray(); 

        long originalPosition = 0;

        if (_stream.CanSeek)
        {
            originalPosition = _stream.Position;
            _stream.Position = 0;
        }

        using var copyStream = new MemoryStream();
        _stream.CopyTo(copyStream);

        if (_stream.CanSeek)
            _stream.Position = originalPosition;

        return copyStream.ToArray();
    }

    /// <returns>A boolean value that indicates whether the stream has reached the end of the file</returns>
    public bool IsEndFile() => _stream.Position >= _stream.Length;

    /// <summary>
    /// Sets the stream position to the specified amount
    /// </summary>
    public void SetStreamPosition(long position) => _stream.Position = position;
    /// <summary>
    /// Sets the stream position to 0
    /// </summary>
    public void ResetStreamPosition() => _stream.Position = 0;

    /// <summary>
    /// Attempts to collect as much data as possible from the header data of the file.
    /// Also invokes decryption of certian packages if needed
    /// </summary>
    private void ResolvePackageInfo()
    {
        try
        {
            if (PackageCrypto.IsPackageEncrypted(this))
            {
                isEncrypted = true;

                if (saveMagic == PackageCrypto.IB1_SAVE_MAGIC)
                    game = Game.IB1;

                else if (saveVersion == PackageCrypto.IB2_SAVE_MAGIC)
                {
                    SetStreamPosition(sizeof(int));
                    game = PackageCrypto.TryDecryptHalfBlock(Game.IB2, _stream)
                        ? Game.IB2
                        : Game.VOTE;
                }
            }
            else
            {
                // can either be an IB3 or unencrypted IB2 package
                if (saveVersion is PackageCrypto.SAVE_FILE_VERSION_IB3)
                {
                    if (IsPackageIB3()) game = Game.IB3;
                    else game = Game.IB2;
                    return;
                }       

                game = Game.IB1;  // defaults to IB1
            }
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"Package Data population failed. Reason: {exception.Message}", exception);
        }
    }

    private bool IsPackageIB3()
    {
        const string STRING_TO_CHECK = "CurrentEngineVersion";
        //checking here to see if "CurrentEngineVersion" is present
        SetStreamPosition(_stream.Length - 62);
        return DeserializeString() is STRING_TO_CHECK;
    }

    /// <summary>
    /// Populates the info for our package header so we can use it to determine save type.
    /// </summary>
    private void GetPackageHeaderInfo()
    {
        if (_stream.Position != 0)
            _stream.Position = 0;
        saveVersion = DeserializeUInt();
        saveMagic = DeserializeUInt();
        ResetStreamPosition();
    }

    /// <summary>
    /// Reverts the stream position during deserialization given a value and its type.
    /// Right now this only supports strings, but for now we don't need to revert any other type

    /// </summary>
    public void RevertStreamPosition(string value)
    {
        _stream.Position -= sizeof(int) + sizeof(byte); // size + nt
        _stream.Position -= value.Length;
    }

    /// <returns>the next string in the stream</returns>
    internal string PeekString()
    {
        string str;
        long originalPosition = _reader.BaseStream.Position;

        try
        {
            str = DeserializeString();
        }
        finally
        {
            _reader.BaseStream.Position = originalPosition;
        }

        return str;
    }

    internal string DeserializeString()
    {
        try
        {
            var strLength = _reader.ReadInt32();
            if (strLength <= 0)
                return string.Empty;

            var bytes = _reader.ReadBytes(strLength);
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
            int _buffer = _reader.ReadInt32();

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
            return _reader.ReadUInt32();
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
            float _buffer = _reader.ReadSingle();

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
            return _reader.ReadBoolean();
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
            return _reader.ReadByte();
        }
        catch (Exception)
        {
            throw new InvalidOperationException($"Could not read byte inside of Unreal Package.");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ReadOnlySpan<byte> Deserialize(int count) => _reader.ReadBytes(count);

    /// <summary>
    /// Deserializes a package's contents into a list of UProperties
    /// </summary>
    /// <returns>A list of all UProperties inside of the package</returns>
    public List<UProperty> DeserializeUPK()
    {
        const int ENCRYPTED_IB1_HEADER = 4;
        const int HEADER_SIZE = 8;

        // replaces existing stream data with the newly decrypted data
        if (isEncrypted)
        {
            byte[] decryptedData = PackageCrypto.DecryptPackage(this);
            InitializeStreamFromBytes(decryptedData);
        }
            
        try
        {
            if (game is Game.IB1 && isEncrypted is true)
                SetStreamPosition(ENCRYPTED_IB1_HEADER);
            else
                SetStreamPosition(HEADER_SIZE);

            var deserializer = new UPKDeserializer(game);
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
        _reader?.Close();
        _writer?.Close();
        _stream?.Close();
    }

    private void DisposeStream()
    {
        _reader?.Dispose();
        _writer?.Dispose();
        _stream?.Dispose();
    }

    public void Dispose()
    {
        DisposeStream();
        GC.SuppressFinalize(this);
    }

    ~UnrealPackage() => Dispose();
}