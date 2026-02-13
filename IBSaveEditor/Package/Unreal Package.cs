public class UnrealPackage : IDisposable
{
    private Stream _stream;
    private BinaryReader _reader;
    private BinaryWriter _writer;
    public PackageInfo info { get; private set; } = null!;

    public UnrealPackage(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("The specified file does not exist.", filePath);

        info = new PackageInfo();
        info.SetPackageName(Path.GetFileNameWithoutExtension(filePath));

        try
        {
            _stream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
            InitializeReaders();
            GetPackageHeaderInfo();
            ResolvePackageType();
        }
        catch (Exception ex)
        {
            Dispose();
            throw new InvalidOperationException($"Failed to construct UnrealPackage for '{filePath}'.", ex);
        }
    }

    /// <summary>
    /// Initializes a new <see cref="MemoryStream"/> for the <see cref="UnrealPackage"/> from the provided byte array.
    /// </summary>
    /// <param name="data">The raw package data used to populate the stream.</param>
    private void InitializeStreamFromBytes(byte[] data)
    {
        if (_stream is not null)
            Dispose();
        _stream = new MemoryStream(data, writable: true);
        InitializeReaders();
    }

    private void InitializeReaders()
    {
        _reader = new BinaryReader(_stream, Encoding.UTF8, leaveOpen: true);
        _writer = new BinaryWriter(_stream, Encoding.UTF8, leaveOpen: true);
    }

    /// <summary>
    /// Attempts to guess a package's type (I.E => IB1, IB2, IB3, VOTE) based on its metadata.
    /// </summary>
    private void ResolvePackageType()
    {
        Game game;
        try
        {
            if (PackageCrypto.IsPackageEncrypted(this))
            {
                info.SetIsEncrypted(true);

                if (info.saveMagic == PackageConstants.IB1_SAVE_MAGIC)
                    game = Game.IB1;

                else if (info.saveVersion == PackageConstants.IB2_SAVE_MAGIC)
                {
                    SetStreamPosition(sizeof(int));
                    game = PackageCrypto.TryDecryptHalfBlock(Game.IB2, _stream) ? Game.IB2 : Game.VOTE;
                }

                else
                    throw new InvalidDataException("Could not decipher encrypted package type when attempting to resolve package info.");
            }
            else
            {
                info.SetIsEncrypted(false);

                // can either be an IB3 or unencrypted IB2 package
                if (info.saveVersion is PackageConstants.SAVE_FILE_VERSION_IB3)
                {
                    if (IsPackageIB3()) game = Game.IB3;
                    else game = Game.IB2;
                }       
                else if (info.saveVersion is PackageConstants.SAVE_FILE_VERSION_PC)
                    game = Game.IB1; 

                else
                    throw new InvalidDataException("Could not decipher decrypted package type when attempting to resolve package info.");
            }

            info.SetGame(game);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"Package Data population failed. Reason: {exception.Message}", exception);
        }
    }

    /// <summary>
    /// Verifies if an unencrypted package is a IB3 save.
    /// </summary>
    private bool IsPackageIB3()
    {
        if (info.isEncrypted) return false;

        const string STRING_TO_CHECK = "CurrentEngineVersion";
        const int ENGINE_VERSION_LOCATION = 62;
        //checking here to see if "CurrentEngineVersion" is present
        SetStreamPosition(_stream.Length - ENGINE_VERSION_LOCATION);
        return DeserializeString() is STRING_TO_CHECK;
    }

    private void GetPackageHeaderInfo()
    {
        if (_stream.Position != 0)
            _stream.Position = 0;
        info.SetSaveVersion(DeserializeUInt());
        info.SetSaveMagic(DeserializeUInt());
        ResetStreamPosition();
    }

    /// <summary>
    /// Reverts the stream position during deserialization given a value and its type.
    /// </summary>
    public void RevertStreamPosition(string value)
    {
        // Right now this only supports strings, but for now we don't need to revert any other type
        _stream.Position -= sizeof(int) + sizeof(byte); // size + nt
        _stream.Position -= value.Length;
    }

    /// <returns>the next string in the stream</returns>
    public string PeekString()
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

    public string DeserializeString()
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

    public object DeserializeByteProperty()
    {
        try
        {
            string enumName = DeserializeString();

            if (enumName == UType.NONE)
                return DeserializeByte();

            string enumValue = DeserializeString();
            return new KeyValuePair<string, string>(enumName, enumValue);
        }
        catch (Exception ex) when (
            ex is EndOfStreamException
            or IOException
            or ObjectDisposedException)
        {
            throw new InvalidDataException(
                "Failed to deserialize ByteProperty from package stream.", ex);
        }
    }

    public int DeserializeInt()
    {
        try
        {
            int value = _reader.ReadInt32();

            if (value < 0 && value != -1)
                return int.MaxValue;

            return value;
        }
        catch (Exception ex) when (
            ex is EndOfStreamException
            or IOException
            or ObjectDisposedException)
        {
            throw new InvalidDataException(
                "Failed to read Int32 from package stream.", ex);
        }
    }

    public uint DeserializeUInt()
    {
        try
        {
            return _reader.ReadUInt32();
        }
        catch (Exception ex) when (
            ex is EndOfStreamException
            or IOException
            or ObjectDisposedException)
        {
            throw new InvalidDataException(
                "Failed to read UInt32 from package stream.", ex);
        }
    }

    public float DeserializeFloat()
    {
        try
        {
            float value = _reader.ReadSingle();

            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new InvalidDataException($"Invalid float value read from stream: {value}");

            return value;
        }
        catch (Exception ex) when (
            ex is EndOfStreamException
            or IOException
            or ObjectDisposedException)
        {
            throw new InvalidDataException(
                "Failed to read Single (float) from package stream.", ex);
        }
    }

    public bool DeserializeBool()
    {
        try
        {
            return _reader.ReadBoolean();
        }
        catch (Exception ex) when (
            ex is EndOfStreamException
            or IOException
            or ObjectDisposedException)
        {
            throw new InvalidDataException(
                "Failed to read Boolean from package stream.", ex);
        }
    }

    public byte DeserializeByte()
    {
        try
        {
            return _reader.ReadByte();
        }
        catch (Exception ex) when (
            ex is EndOfStreamException
            or IOException
            or ObjectDisposedException)
        {
            throw new InvalidDataException(
                "Failed to read Byte from package stream.", ex);
        }
    }

    /// <summary>
    /// Deserializes a package's contents into a list of UProperty's
    /// </summary>
    /// <returns>A list of all UProperties inside of a <see cref="UnrealPackage"/></returns>
    public List<UProperty> DeserializeUPK()
    {
        const int ENCRYPTED_IB1_HEADER = 4;
        const int HEADER_SIZE = 8;

        // replaces existing stream data with the newly decrypted data
        if (info.isEncrypted)
        {
            byte[] decryptedData = PackageCrypto.DecryptPackage(this);
            InitializeStreamFromBytes(decryptedData);
        }
            
        try
        {
            if (info.game is Game.IB1 && info.isEncrypted is true)
                SetStreamPosition(ENCRYPTED_IB1_HEADER);
            else
                SetStreamPosition(HEADER_SIZE);

            Deserializer deserializer = new Deserializer();
            var uProperties = deserializer.DeserializePackage(this);
            return uProperties;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to deserialize package contents.", ex);
        }
    }

    /// <summary>
    /// Returns a copy of the underlying stream's bytes without altering its final position.
    /// </summary>
    public byte[] GetStreamBytes()
    {
        if (_stream is null)
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

    public bool IsEndFile() => _stream.Position >= _stream.Length;

    public void SetStreamPosition(long position)
    {
        if (!_stream.CanSeek) throw new NotSupportedException("Stream is not seekable.");
        if (position < 0 || position > _stream.Length) throw new ArgumentOutOfRangeException(nameof(position));
        _stream.Position = position;
    }

    public long GetStreamPosition() => _stream.Position;

    public void ResetStreamPosition() => _stream.Position = 0;

    public void Dispose()
    {
        _reader?.Dispose();
        _writer?.Dispose();
        _stream?.Dispose();
    }
}