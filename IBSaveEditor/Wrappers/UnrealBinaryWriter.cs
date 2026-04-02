using IBSaveEditor.UProperties;
namespace IBSaveEditor.Wrappers;
/// <summary>
/// Wrapper for <see cref="BinaryWriter"/> that includes methods tailoring to Unreal Packages.
/// </summary>
public class UnrealBinaryWriter : BinaryWriter
{
    public UnrealBinaryWriter(Stream stream) : base(stream) { }

    /// <summary>
    /// Overwrite method for BinaryWriter.write(string) so it does not append the size to the beginning of the string
    /// </summary>
    public void WriteUnrealString(string str)
    {
        // write the size of the 0 string
        if (str == string.Empty)
        {
            Write(0);
            return;
        }

        // Onstead of using binWriter.Write directly for strings, use this work-around 
        // avoids appending the string size as a byte to the beginning of the string in hex
        byte[] strBytes = Encoding.UTF8.GetBytes(str);
        Write(str.Length + sizeof(byte)); // string size + null terminator
        Write(strBytes);
        Write((byte)0);  // null terminator
    }

    /// <summary>
    /// Writes out the metadata for a UProperty.
    /// </summary>
    /// <param name="property"></param>
    public void WritePropertyMetadata(UProperty property)
    {
        WriteUnrealString(property.name);
        WriteUnrealString(property.type);
        Write(property.size);
        Write(property.arrayIndex);
    }
}