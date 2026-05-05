using IBSaveEditor.UProperties;
using System.Text;

namespace IBSaveEditor.Wrappers;
/// <summary>
/// Wrapper for <see cref="BinaryWriter"/> that includes methods tailoring to Unreal Packages.
/// </summary>
public class UnrealBinaryReader : BinaryReader
{
    public UnrealBinaryReader(UnrealStream stream) : base(stream.BaseStream) { }

    public UnrealBinaryReader(UnrealStream stream, Encoding encoding, bool leaveOpen) : base(stream.BaseStream, encoding, leaveOpen) { }

    /// <returns>the next string in the stream</returns>
    public string PeekString()
    {
        string str;
        long originalPosition = BaseStream.Position;

        try
        {
            str = DeserializeString();
        }
        finally
        {
            BaseStream.Position = originalPosition;
        }

        return str;
    }

    public string DeserializeString()
    {
        try
        {
            var strLength = ReadInt32();
            if (strLength <= 0)
                return string.Empty;

            var bytes = ReadBytes(strLength);
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
            int value = ReadInt32();

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
            return ReadUInt32();
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
            float value = ReadSingle();

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
            return ReadBoolean();
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
            return ReadByte();
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
}