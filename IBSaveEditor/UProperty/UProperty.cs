using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Newtonsoft.Json;
using IBSaveEditor.Wrappers;
using IBSaveEditor.UProperties.UArray;
using IBSaveEditor.Enums;

namespace IBSaveEditor.UProperties;
/// <summary>
/// Abstract base class for all UE3 save properties. Provides common metadata fields
/// and defines the contract for JSON writing and binary serialization.
/// </summary>
public abstract class UProperty
{
    public string name;
    public string type;
    public int    size;
    public int    arrayIndex;

    /// <summary>The total byte size of this UProperty including its metadata header.</summary>
    public int? uPropertyElementSize;

    protected UProperty(TagContainer tag)
    {
        name       = tag.name;
        type       = tag.type;
        arrayIndex = tag.arrayIndex;
        size       = tag.size;
    }

    /// <summary>
    /// Returns whether the full metadata size should be tracked for this property.
    /// Used by subclasses during JSON deserialization to populate size fields for re-serialization.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private protected bool ShouldTrackFullSize(TagContainer tag) => tag.bShouldTrackMetadataSize;

    /// <summary>Writes this property's value to a <see cref="Utf8JsonWriter"/> without a property name.</summary>
    public abstract void WriteValueData(Utf8JsonWriter writer);

    /// <summary>Writes this property's value to a <see cref="Utf8JsonWriter"/> under the given name.</summary>
    public abstract void WriteValueData(Utf8JsonWriter writer, string name);

    /// <summary>Serializes this property's value into the UE3 binary format.</summary>
    public abstract void SerializeValue(UnrealBinaryWriter writer);
}

#region Primitive Properties

public class UIntProperty : UProperty
{
    public int value;

    /// <summary>Deserializes an int from the binary stream.</summary>
    public UIntProperty(UnrealBinaryReader reader, TagContainer tag)
        : base(tag) => value = reader.DeserializeInt();

    /// <summary>Reads an int from JSON.</summary>
    public UIntProperty(JsonTextReader reader, TagContainer tag) : base(tag)
    {
        value = UPropertyHelper.ParseReaderValue<int>(reader, int.TryParse);

        if (ShouldTrackFullSize(tag))
            UPropertyHelper.PopulatePropertyMetadataSize(this);
    }

    public override void WriteValueData(Utf8JsonWriter writer) { }
    public override void WriteValueData(Utf8JsonWriter writer, string name) => writer.WriteNumber(name, value);
    public override void SerializeValue(UnrealBinaryWriter writer)          => writer.Write(value);
}

public class UFloatProperty : UProperty
{
    public float value;

    // JSON drops trailing .0 by default which breaks downstream parsing : enforce a minimum decimal place.
    private static readonly string      FloatFormat     = "0.0#########";
    private static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;

    /// <summary>Deserializes a float from the binary stream.</summary>
    public UFloatProperty(UnrealBinaryReader reader, TagContainer tag)
        : base(tag) => value = reader.DeserializeFloat();

    /// <summary>Reads a float from JSON.</summary>
    public UFloatProperty(JsonTextReader reader, TagContainer tag) : base(tag)
    {
        value = UPropertyHelper.ParseReaderValue<float>(reader, float.TryParse);

        if (ShouldTrackFullSize(tag))
            UPropertyHelper.PopulatePropertyMetadataSize(this);
    }

    public override void WriteValueData(Utf8JsonWriter writer) { }

    public override void WriteValueData(Utf8JsonWriter writer, string name)
    {
        // Write as raw value to preserve the decimal format defined above.
        writer.WritePropertyName(name);
        writer.WriteRawValue(value.ToString(FloatFormat, InvariantCulture));
    }

    public override void SerializeValue(UnrealBinaryWriter writer) => writer.Write(value);
}

public class UBoolProperty : UProperty
{
    public bool value;

    /// <summary>Deserializes a bool from the binary stream.</summary>
    public UBoolProperty(UnrealBinaryReader reader, TagContainer tag)
        : base(tag) => value = reader.DeserializeBool();

    /// <summary>Reads a bool from JSON.</summary>
    public UBoolProperty(JsonTextReader reader, TagContainer tag) : base(tag)
    {
        value = UPropertyHelper.ParseReaderValue<bool>(reader, bool.TryParse);

        if (ShouldTrackFullSize(tag))
        {
            // Bool values serialize with size 0 in metadata, but still occupy one byte in content.
            uPropertyElementSize = sizeof(bool);
            UPropertyHelper.PopulatePropertyMetadataSize(this);
        }
    }

    public override void WriteValueData(Utf8JsonWriter writer) { }
    public override void WriteValueData(Utf8JsonWriter writer, string name) => writer.WriteBoolean(name, value);
    public override void SerializeValue(UnrealBinaryWriter writer)          => writer.Write(value);
}

public class UStringProperty : UProperty
{
    public string value = string.Empty;

    /// <summary>Deserializes a string from the binary stream.</summary>
    public UStringProperty(UnrealBinaryReader reader, TagContainer tag)
        : base(tag) => value = reader.DeserializeString();

    /// <summary>Reads a string from JSON.</summary>
    public UStringProperty(JsonTextReader reader, TagContainer tag) : base(tag)
    {
        value = UPropertyHelper.ReaderValueToString(reader);
        size  = UPropertyHelper.GetLittleEndianStringLength(value);

        if (ShouldTrackFullSize(tag))
            UPropertyHelper.PopulatePropertyMetadataSize(this);
    }

    public override void WriteValueData(Utf8JsonWriter writer)              => writer.WriteStringValue(value);
    public override void WriteValueData(Utf8JsonWriter writer, string name) => writer.WriteString(name, value);
    public override void SerializeValue(UnrealBinaryWriter writer)          => writer.WriteUnrealString(value);
}

public class UNameProperty : UProperty
{
    private const string FNAME_PREFIX = "ini_";

    public string value = string.Empty;

    /// <summary>Deserializes a name property from the binary stream.</summary>
    public UNameProperty(UnrealBinaryReader reader, TagContainer tag)
        : base(tag) => value = reader.DeserializeString();

    /// <summary>Reads a name property from JSON.</summary>
    public UNameProperty(JsonTextReader reader, TagContainer tag) : base(tag)
    {
        value = UPropertyHelper.ReaderValueToString(reader);
        size  = UPropertyHelper.GetLittleEndianStringLength(value);

        if (ShouldTrackFullSize(tag))
            UPropertyHelper.PopulatePropertyMetadataSize(this);
    }

    public override void WriteValueData(Utf8JsonWriter writer)              => writer.WriteStringValue(value);

    /// <summary>
    /// Writes this name property with the ini_ prefix on the JSON key to mark it as an FName.
    /// </summary>
    public override void WriteValueData(Utf8JsonWriter writer, string name)
        => writer.WriteString($"{FNAME_PREFIX}{name}", value);

    public override void SerializeValue(UnrealBinaryWriter writer) => writer.WriteUnrealString(value);
}

#endregion

#region Byte Properties

/// <summary>
/// Abstract base for byte properties. Size determines whether this is a simple
/// byte value or an enum-backed byte : see <see cref="InstantiateProperty"/>.
/// </summary>
public abstract class UByteProperty : UProperty
{
    protected UByteProperty(TagContainer tag) : base(tag) { }

    /// <summary>
    /// Factory method for binary deserialization. Reads the identifier string first
    /// to determine which concrete type to construct.
    /// </summary>
    public static UByteProperty InstantiateProperty(UnrealBinaryReader reader, TagContainer tag)
    {
        string identifier = reader.DeserializeString();

        return tag.size switch
        {
            sizeof(byte)   => new USimpleByteProperty(reader, tag),
            > sizeof(byte) => new UEnumByteProperty(reader, tag, identifier),
            _              => throw new NotSupportedException($"Unsupported byte property size: {tag.size}")
        };
    }

    /// <summary>
    /// Factory method for JSON deserialization. Size of 1 = simple byte, size of 0 = enum byte.
    /// </summary>
    public static UByteProperty InstantiateProperty(JsonTextReader reader, TagContainer tag)
    {
        return tag.size switch
        {
            sizeof(byte) => new USimpleByteProperty(reader, tag),
            0            => new UEnumByteProperty(reader, tag),
            _            => throw new NotSupportedException($"Unsupported byte property size: {tag.size}")
        };
    }
}

public class USimpleByteProperty : UByteProperty
{
    public byte value;

    /// <summary>Deserializes a single byte from the binary stream.</summary>
    public USimpleByteProperty(UnrealBinaryReader reader, TagContainer tag)
        : base(tag) => value = reader.DeserializeByte();

    /// <summary>Reads a byte from JSON.</summary>
    public USimpleByteProperty(JsonTextReader reader, TagContainer tag) : base(tag)
    {
        value = UPropertyHelper.ParseReaderValue<byte>(reader, byte.TryParse);

        if (ShouldTrackFullSize(tag))
        {
            // Simple byte properties serialize "None" in place of an enum name.
            uPropertyElementSize = UPropertyHelper.GetLittleEndianStringLength(UType.NONE);
            UPropertyHelper.PopulatePropertyMetadataSize(this);
        }
    }

    public override void WriteValueData(Utf8JsonWriter writer) { }

    /// <summary>Written with "b" prefix on the key to distinguish from int properties in JSON.</summary>
    public override void WriteValueData(Utf8JsonWriter writer, string name) => writer.WriteNumber($"b{name}", value);

    public override void SerializeValue(UnrealBinaryWriter writer)
    {
        writer.WriteUnrealString(UType.NONE);
        writer.Write(value);
    }
}

public class UEnumByteProperty : UByteProperty
{
    private const string ENUM_PREFIX          = "e";
    private const string EDGECASE_PLAYERTYPE  = "eCurrentPlayerType";
    private const string ENUM_NAME            = "Enum";
    private const string ENUM_VALUE           = "Enum Value";

    public string enumName  = string.Empty;
    public string enumValue = string.Empty;

    /// <summary>Deserializes an enum byte property from the binary stream.</summary>
    public UEnumByteProperty(UnrealBinaryReader reader, TagContainer tag, string enumName) : base(tag)
    {
        this.enumName = enumName;
        enumValue     = reader.DeserializeString();
    }

    /// <summary>Reads an enum byte property from JSON. Expects an object with "Enum" and "Enum Value" keys.</summary>
    public UEnumByteProperty(JsonTextReader reader, TagContainer tag) : base(tag)
    {
        if (ReadExpectedPropertyName(reader, ENUM_NAME))
            enumName = UPropertyHelper.ReaderValueToString(reader);

        if (ReadExpectedPropertyName(reader, ENUM_VALUE))
            enumValue = UPropertyHelper.ReaderValueToString(reader);

        size = UPropertyHelper.GetLittleEndianStringLength(enumValue);

        // Move past the closing brace of the enum object.
        reader.Read();

        if (ShouldTrackFullSize(tag))
        {
            uPropertyElementSize = UPropertyHelper.GetLittleEndianStringLength(enumName);
            UPropertyHelper.PopulatePropertyMetadataSize(this);
        }
    }

    /// <summary>
    /// Reads a property name token from the JSON reader and validates it matches the expected name.
    /// </summary>
    private bool ReadExpectedPropertyName(JsonTextReader reader, string expected)
    {
        reader.Read();

        if (UPropertyHelper.ReaderValueToString(reader) != expected)
            throw new InvalidDataException($"Expected '{expected}' as a property name.");

        reader.Read();
        return true;
    }

    public override void WriteValueData(Utf8JsonWriter writer) { }

    /// <summary>
    /// Writes the enum as a JSON object with "Enum" and "Enum Value" keys.
    /// The property name gets the "e" prefix unless it is the eCurrentPlayerType edge case.
    /// </summary>
    public override void WriteValueData(Utf8JsonWriter writer, string name)
    {
        if (name is not EDGECASE_PLAYERTYPE)
            name = $"{ENUM_PREFIX}{name}";

        writer.WriteStartObject(name);
        writer.WriteString(ENUM_NAME,  enumName);
        writer.WriteString(ENUM_VALUE, enumValue);
        writer.WriteEndObject();
    }

    public override void SerializeValue(UnrealBinaryWriter writer)
    {
        writer.WriteUnrealString(enumName);
        writer.WriteUnrealString(enumValue);
    }
}

#endregion

#region Composite Properties

public class UStructProperty : UProperty
{
    public List<UProperty> elements;
    public string          structName;

    /// <summary>Constructs a struct property from deserialized binary data.</summary>
    public UStructProperty(TagContainer tag, string structName, List<UProperty> elements) : base(tag)
    {
        this.structName = structName;
        this.elements   = elements;
    }

    /// <summary>Reads a struct property from JSON.</summary>
    public UStructProperty(JsonTextReader reader, TagContainer tag, List<UProperty> elements, string structName) : base(tag)
    {
        this.structName = structName;
        this.elements   = elements;

        size += UPropertyHelper.CalculatePropertyContentSize(elements);

        // Resolve the alternate UnrealScript struct name if one wasn't provided.
        if (this.structName == string.Empty)
            this.structName = ResolveAlternateStructName();

        uPropertyElementSize = UPropertyHelper.GetLittleEndianStringLength(this.structName);
        UPropertyHelper.PopulatePropertyMetadataSize(this);
    }

    /// <summary>
    /// Resolves the UnrealScript variable name for structs that use an alternate internal name.
    /// This is IB3-specific : the struct name written to the binary differs from the property name.
    /// </summary>
    private string ResolveAlternateStructName() => name switch
    {
        "Data"                       => "ItemEnhanceData",
        "ForcedMapVariation"         => "BossMapDefinition",
        "CurrentTotalTrackingStats"  => "BattleTrackingStats",
        "GameOptions"                => "PersistGameOptions",
        "SocialChallengeSaveEvents"  => "SocialChallengeSave",
        _                            => ""
    };

    public override void WriteValueData(Utf8JsonWriter writer)
        => UPropertyHelper.WritePropertyListJson(writer, elements);

    public override void WriteValueData(Utf8JsonWriter writer, string name)
    {
        writer.WriteStartObject(name);
        UPropertyHelper.WritePropertyListJson(writer, elements);
        writer.WriteEndObject();
    }

    public override void SerializeValue(UnrealBinaryWriter writer)
    {
        if (structName != string.Empty)
            writer.WriteUnrealString(structName);

        UPropertyHelper.SerializePropertyList(writer, elements);
    }
}

public class UArrayProperty : UProperty
{
    // SavedCheevo uses enum-keyed entries rather than indexed entries.
    private const string EDGECASE_CHEEVO = "SavedCheevo";

    public ArrayMetadata arrayInfo;
    public int           arrayEntryCount;
    public List<object>  elements;

    /// <summary>Constructs an array property from deserialized binary data.</summary>
    public UArrayProperty(TagContainer tag, List<object> elements) : base(tag)
    {
        arrayEntryCount = tag.arrayEntryCount;
        this.elements   = elements;
        arrayInfo       = tag.arrayInfo;
    }

    /// <summary>Reads an array property from JSON.</summary>
    public UArrayProperty(JsonTextReader reader, TagContainer tag, List<object> elements) : base(tag)
    {
        arrayEntryCount = elements.Count;
        this.elements   = elements;
        arrayInfo       = tag.arrayInfo;

        size += UPropertyHelper.CalculatePropertyContentSize(elements);
        UPropertyHelper.PopulatePropertyMetadataSize(this);
    }

    public override void WriteValueData(Utf8JsonWriter writer) => WriteArrayContentsAsJson(writer);

    public override void WriteValueData(Utf8JsonWriter writer, string name)
    {
        writer.WriteStartArray(name);
        WriteArrayContentsAsJson(writer);
        writer.WriteEndArray();
    }

    public override void SerializeValue(UnrealBinaryWriter writer)
    {
        writer.Write(arrayEntryCount);

        if (arrayEntryCount == 0) return;

        SerializeArrayContents(writer);
    }

    // JSON writing ──────────────────────────────────────────────────────────

    private void WriteArrayContentsAsJson(Utf8JsonWriter writer)
    {
        if (arrayInfo.arrayType is ArrayType.Dynamic)
            WriteDynamicArrayContentsAsJson(writer);
        else
            WriteStaticArrayContentsAsJson(writer);
    }

    private void WriteStaticArrayContentsAsJson(Utf8JsonWriter writer)
    {
        if (arrayInfo.valueType is PropertyType.StructProperty)
        {
            WriteStaticStructArrayAsJson(writer);
            return;
        }

        // NumConsumable and ShowConsumableBadge use a keyed-object layout for their int/byte entries.
        if (arrayInfo.valueType is PropertyType.IntProperty or PropertyType.ByteProperty)
        {
            WriteStaticNumArrayAsJson(writer);
            return;
        }

        WriteStaticPropertyArrayAsJson(writer);
    }

    /// <summary>
    /// Writes static struct array entries. SavedCheevo is an edge case that uses struct-based
    /// keys rather than sequential indices.
    /// </summary>
    private void WriteStaticStructArrayAsJson(Utf8JsonWriter writer)
    {
        if (name == EDGECASE_CHEEVO)
        {
            writer.WriteStartObject();
            foreach (UStructProperty element in elements.OfType<UStructProperty>())
                element.WriteValueData(writer, IBEnum.GetEnumEntryFromIndex(name, element.arrayIndex));
            writer.WriteEndObject();
            return;
        }

        foreach (UStructProperty element in elements.OfType<UStructProperty>())
        {
            writer.WriteStartObject();
            element.WriteValueData(writer);
            writer.WriteEndObject();
        }
    }

    /// <summary>
    /// Writes static numeric array entries as a keyed object.
    /// Keys are looked up by array index from the IBEnum registry.
    /// </summary>
    private void WriteStaticNumArrayAsJson(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        foreach (UProperty element in elements.OfType<UProperty>())
            element.WriteValueData(writer, IBEnum.GetEnumEntryFromIndex(name, element.arrayIndex));
        writer.WriteEndObject();
    }

    private void WriteStaticPropertyArrayAsJson(Utf8JsonWriter writer)
    {
        foreach (UProperty element in elements.OfType<UProperty>())
            element.WriteValueData(writer);
    }

    /// <summary>
    /// Writes dynamic array contents. Dispatches on the type of the first element
    /// to determine how to write the remaining entries.
    /// </summary>
    private void WriteDynamicArrayContentsAsJson(Utf8JsonWriter writer)
    {
        object? first = UPropertyHelper.GetFirstOrNull(elements);

        switch (first)
        {
            case null:
                return;
            case string:
                foreach (string e in elements.OfType<string>())     writer.WriteStringValue(e);
                break;
            case int:
                foreach (int e in elements.OfType<int>())           writer.WriteNumberValue(e);
                break;
            case float:
                foreach (float e in elements.OfType<float>())       writer.WriteNumberValue(e);
                break;
            case bool:
                foreach (bool e in elements.OfType<bool>())         writer.WriteBooleanValue(e);
                break;
            case List<UProperty>:
                UPropertyHelper.WriteNestedPropertyListsJson(writer, elements.OfType<List<UProperty>>());
                break;
            default:
                throw new NotImplementedException("Dynamic array type not implemented.");
        }
    }

    // Binary serialization ──────────────────────────────────────────────────

    /// <summary>
    /// Serializes dynamic array contents. Dispatches on the first element's type.
    /// Static arrays are serialized through their individual UProperty elements.
    /// </summary>
    private void SerializeArrayContents(UnrealBinaryWriter writer)
    {
        object? first = UPropertyHelper.GetFirstOrNull(elements);

        switch (first)
        {
            case int:
                foreach (int e in elements.OfType<int>())       writer.Write(e);
                break;
            case float:
                foreach (float e in elements.OfType<float>())   writer.Write(e);
                break;
            case bool:
                foreach (bool e in elements.OfType<bool>())     writer.Write(e);
                break;
            case string:
                foreach (string e in elements.OfType<string>()) writer.WriteUnrealString(e);
                break;
            case List<UProperty>:
                UPropertyHelper.SerializeNestedPropertyLists(writer, elements.OfType<List<UProperty>>());
                break;
            default:
                throw new NotImplementedException("Dynamic array type not implemented.");
        }
    }
}

#endregion