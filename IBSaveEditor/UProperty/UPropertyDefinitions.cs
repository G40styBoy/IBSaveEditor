using IBSaveEditor.UProperties.UArray;
namespace IBSaveEditor.UProperties;

/// <summary>
/// Used to package and pass tag data neatly. 
/// Ends up being used to construct UProperty's
/// </summary>
public record struct TagContainer
{
    public string name;
    public string type;
    public int size;
    public int arrayIndex;
    public string alternateName;
    public int arrayEntryCount;
    public ArrayMetadata arrayInfo;
    /// <summary>
    /// If we need to keep track of a UProperty's total size for struct and array purposes
    /// </summary>
    public bool bShouldTrackMetadataSize;
}

/// <summary>
/// Defines constant byte sizes used in the serialized UProperty binary layout.
/// These values represent fixed-length fields present in UProperty metadata.
/// </summary>
public static class UPropertyLayout
{
    public const int NULL_TERMINATOR_SIZE = sizeof(byte);
    public const int ARRAY_INDEX_SIZE = sizeof(int);
    public const int VALUE_SIZE = sizeof(int);
    public const int NAME_SIZE = sizeof(int);
    public const int BYTE_SIZE_SPECIAL = 0;
}

/// <summary>
/// String representation of all UProperty types discoverable inside of a Save Package.
/// </summary>
public class UType
{
    public const string INT_PROPERTY = "IntProperty";
    public const string FLOAT_PROPERTY = "FloatProperty";
    public const string BYTE_PROPERTY = "ByteProperty";
    public const string BOOL_PROPERTY = "BoolProperty";
    public const string STR_PROPERTY = "StrProperty";
    public const string NAME_PROPERTY = "NameProperty";
    public const string STRUCT_PROPERTY = "StructProperty";
    public const string ARRAY_PROPERTY = "ArrayProperty";
    public const string NONE = "None";
}

/// <summary>
/// Enum representation of all UProperty types discoverable inside of a Save Package.
/// </summary>
public enum PropertyType
{
    StructProperty,
    ArrayProperty,
    IntProperty,
    StrProperty,
    NameProperty,
    FloatProperty,
    BoolProperty,
    ByteProperty
}