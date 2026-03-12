using System.Globalization;
using System.Text.Json;
using Newtonsoft.Json;

/// <summary>
/// Class handling everything UProperty related.
/// </summary>
public abstract class UProperty
{
    public string name;
    public string type;
    public int size;
    public int arrayIndex;

    /// <summary>
    /// The size of the entire UProperty including metadata.
    /// </summary>
    public int? uPropertyElementSize;

    protected UProperty(TagContainer tag)
    {
        name = tag.name;
        type = tag.type;
        arrayIndex = tag.arrayIndex;
        size = tag.size;
    }

    private protected bool ShouldTrackFullSize(TagContainer tag) => tag.bShouldTrackMetadataSize;

    public abstract void WriteValueData(Utf8JsonWriter writer);
    public abstract void WriteValueData(Utf8JsonWriter writer, string name);
    public abstract void SerializeValue(UnrealBinaryWriter writer);
}

public class UIntProperty : UProperty
{
    public int value;

    public UIntProperty(UnrealPackage upk, TagContainer tag)
        : base(tag) => value = upk.DeserializeInt();

    public UIntProperty(JsonTextReader reader, TagContainer tag) : base(tag)
    {
        value = UPropertyHelper.ParseReaderValue<int>(reader, int.TryParse);

        if (ShouldTrackFullSize(tag))
            UPropertyHelper.PopulatePropertyMetadataSize(this);
    }

    public override void WriteValueData(Utf8JsonWriter writer) { }

    public override void WriteValueData(Utf8JsonWriter writer, string name) => writer.WriteNumber(name, value);

    public override void SerializeValue(UnrealBinaryWriter writer) => writer.Write(value);
}

public class UFloatProperty : UProperty
{
    public float value;

    private static readonly string DoubleFormat = "0.0#########";
    private static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;

    public UFloatProperty(UnrealPackage upk, TagContainer tag)
        : base(tag) => value = upk.DeserializeFloat();

    public UFloatProperty(JsonTextReader reader, TagContainer tag) : base(tag)
    {
        value = UPropertyHelper.ParseReaderValue<float>(reader, float.TryParse);

        if (ShouldTrackFullSize(tag))
            UPropertyHelper.PopulatePropertyMetadataSize(this);
    }

    public override void WriteValueData(Utf8JsonWriter writer) { }

    public override void WriteValueData(Utf8JsonWriter writer, string name)
    {
        // JSON drops trailing .0 by default, which breaks downstream interpretation.
        writer.WritePropertyName(name);
        writer.WriteRawValue(value.ToString(DoubleFormat, InvariantCulture));
    }

    public override void SerializeValue(UnrealBinaryWriter writer) => writer.Write(value);
}

public class UBoolProperty : UProperty
{
    public bool value;

    public UBoolProperty(UnrealPackage upk, TagContainer tag)
        : base(tag) => value = upk.DeserializeBool();

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

    public override void SerializeValue(UnrealBinaryWriter writer) => writer.Write(value);
}

public class UStringProperty : UProperty
{
    public string value = string.Empty;

    public UStringProperty(UnrealPackage upk, TagContainer tag) : base(tag)
        => value = upk.DeserializeString();

    public UStringProperty(JsonTextReader reader, TagContainer tag) : base(tag)
    {
        value = UPropertyHelper.ReaderValueToString(reader);
        size = UPropertyHelper.GetLittleEndianStringLength(value);

        if (ShouldTrackFullSize(tag))
            UPropertyHelper.PopulatePropertyMetadataSize(this);
    }

    public override void WriteValueData(Utf8JsonWriter writer) => writer.WriteStringValue(value);

    public override void WriteValueData(Utf8JsonWriter writer, string name) => writer.WriteString(name, value);

    public override void SerializeValue(UnrealBinaryWriter writer) => writer.WriteUnrealString(value);
}

public class UNameProperty : UProperty
{
    private const string FNAME_PREFIX = "ini_";

    public string value = string.Empty;

    public UNameProperty(UnrealPackage upk, TagContainer tag) : base(tag)
        => value = upk.DeserializeString();

    public UNameProperty(JsonTextReader reader, TagContainer tag) : base(tag)
    {
        value = UPropertyHelper.ReaderValueToString(reader);
        size = UPropertyHelper.GetLittleEndianStringLength(value);

        if (ShouldTrackFullSize(tag))
            UPropertyHelper.PopulatePropertyMetadataSize(this);
    }

    public override void WriteValueData(Utf8JsonWriter writer) => writer.WriteStringValue(value);

    public override void WriteValueData(Utf8JsonWriter writer, string name)
        => writer.WriteString($"{FNAME_PREFIX}{name}", value);

    public override void SerializeValue(UnrealBinaryWriter writer) => writer.WriteUnrealString(value);
}

public abstract class UByteProperty : UProperty
{
    protected UByteProperty(TagContainer tag) : base(tag) { }

    public static UByteProperty InstantiateProperty(UnrealPackage upk, TagContainer tag)
    {
        string identifier = upk.DeserializeString();

        return tag.size switch
        {
            sizeof(byte) => new USimpleByteProperty(upk, tag),
            > sizeof(byte) => new UEnumByteProperty(upk, tag, identifier),
            _ => throw new NotSupportedException($"Unsupported byte property size: {tag.size}")
        };
    }

    public static UByteProperty InstantiateProperty(JsonTextReader reader, TagContainer tag)
    {
        return tag.size switch
        {
            sizeof(byte) => new USimpleByteProperty(reader, tag),
            0 => new UEnumByteProperty(reader, tag),
            _ => throw new NotSupportedException($"Unsupported byte property size: {tag.size}")
        };
    }
}

public class USimpleByteProperty : UByteProperty
{
    public byte value;

    public USimpleByteProperty(UnrealPackage upk, TagContainer tag)
        : base(tag) => value = upk.DeserializeByte();

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

    public override void WriteValueData(Utf8JsonWriter writer, string name) => writer.WriteNumber($"b{name}", value);

    public override void SerializeValue(UnrealBinaryWriter writer)
    {
        writer.WriteUnrealString(UType.NONE);
        writer.Write(value);
    }
}

public class UEnumByteProperty : UByteProperty
{
    private const string ENUM_PREFIX = "e";
    private const string EDGECASE_PLAYERTYPE = "eCurrentPlayerType";
    private const string ENUM_NAME = "Enum";
    private const string ENUM_VALUE = "Enum Value";

    public string enumName = string.Empty;
    public string enumValue = string.Empty;

    public UEnumByteProperty(UnrealPackage upk, TagContainer tag, string enumName) : base(tag)
    {
        this.enumName = enumName;
        enumValue = upk.DeserializeString();
    }

    public UEnumByteProperty(JsonTextReader reader, TagContainer tag) : base(tag)
    {
        if (ReadExpectedPropertyName(reader, ENUM_NAME))
            enumName = UPropertyHelper.ReaderValueToString(reader);

        if (ReadExpectedPropertyName(reader, ENUM_VALUE))
            enumValue = UPropertyHelper.ReaderValueToString(reader);

        size = UPropertyHelper.GetLittleEndianStringLength(enumValue);

        // Move past the closing brace.
        reader.Read();

        if (ShouldTrackFullSize(tag))
        {
            uPropertyElementSize = UPropertyHelper.GetLittleEndianStringLength(enumName);
            UPropertyHelper.PopulatePropertyMetadataSize(this);
        }
    }

    private bool ReadExpectedPropertyName(JsonTextReader reader, string expected)
    {
        reader.Read();

        if (UPropertyHelper.ReaderValueToString(reader) != expected)
            throw new InvalidDataException($"Expected {expected} as a property name.");

        reader.Read();
        return true;
    }

    public override void WriteValueData(Utf8JsonWriter writer) { }

    public override void WriteValueData(Utf8JsonWriter writer, string name)
    {
        if (name is not EDGECASE_PLAYERTYPE)
            name = $"{ENUM_PREFIX}{name}";

        writer.WriteStartObject(name);
        writer.WriteString(ENUM_NAME, enumName);
        writer.WriteString(ENUM_VALUE, enumValue);
        writer.WriteEndObject();
    }

    public override void SerializeValue(UnrealBinaryWriter writer)
    {
        writer.WriteUnrealString(enumName);
        writer.WriteUnrealString(enumValue);
    }
}

public class UStructProperty : UProperty
{
    public List<UProperty> elements;
    public string structName;

    public UStructProperty(TagContainer tag, string structName, List<UProperty> elements) : base(tag)
    {
        this.structName = structName;
        this.elements = elements;
    }

    public UStructProperty(JsonTextReader reader, TagContainer tag, List<UProperty> elements, string structName) : base(tag)
    {
        this.structName = structName;
        this.elements = elements;

        size += UPropertyHelper.CalculatePropertyContentSize(elements);

        if (this.structName == string.Empty)
            this.structName = ResolveAlternateStructName();

        uPropertyElementSize = UPropertyHelper.GetLittleEndianStringLength(this.structName);
        UPropertyHelper.PopulatePropertyMetadataSize(this);
    }

    private string ResolveAlternateStructName()
    {
        return name switch
        {
            "Data" => "ItemEnhanceData",
            "ForcedMapVariation" => "BossMapDefinition",
            "CurrentTotalTrackingStats" => "BattleTrackingStats",
            "GameOptions" => "PersistGameOptions",
            "SocialChallengeSaveEvents" => "SocialChallengeSave",
            _ => string.Empty
        };
    }

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
    private const string EDGECASE_CHEEVO = "SavedCheevo";

    public ArrayMetadata arrayInfo;
    public int arrayEntryCount;
    public List<object> elements;

    public UArrayProperty(TagContainer tag, List<object> elements) : base(tag)
    {
        arrayEntryCount = tag.arrayEntryCount;
        this.elements = elements;
        arrayInfo = tag.arrayInfo;
    }

    public UArrayProperty(JsonTextReader reader, TagContainer tag, List<object> elements) : base(tag)
    {
        arrayEntryCount = elements.Count;
        this.elements = elements;
        arrayInfo = tag.arrayInfo;

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

        if (arrayEntryCount == 0)
            return;

        SerializeArrayContents(writer);
    }

    private void WriteArrayContentsAsJson(Utf8JsonWriter writer)
    {
        if (arrayInfo.arrayType is ArrayType.Dynamic)
        {
            WriteDynamicArrayContentsAsJson(writer);
            return;
        }

        WriteStaticArrayContentsAsJson(writer);
    }

    private void WriteStaticArrayContentsAsJson(Utf8JsonWriter writer)
    {
        if (arrayInfo.valueType is PropertyType.StructProperty)
        {
            WriteStaticStructArrayAsJson(writer);
            return;
        }

        if (arrayInfo.valueType is PropertyType.IntProperty)
        {
            WriteStaticIntArrayAsJson(writer);
            return;
        }

        WriteStaticPropertyArrayAsJson(writer);
    }

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

    private void WriteStaticIntArrayAsJson(Utf8JsonWriter writer)
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

    private void WriteDynamicArrayContentsAsJson(Utf8JsonWriter writer)
    {
        object? firstElement = UPropertyHelper.GetFirstOrNull(elements);

        switch (firstElement)
        {
            case null:
                return;

            case string:
                foreach (string element in elements.OfType<string>())
                    writer.WriteStringValue(element);
                break;

            case int:
                foreach (int element in elements.OfType<int>())
                    writer.WriteNumberValue(element);
                break;

            case float:
                foreach (float element in elements.OfType<float>())
                    writer.WriteNumberValue(element);
                break;

            case bool:
                foreach (bool element in elements.OfType<bool>())
                    writer.WriteBooleanValue(element);
                break;

            case List<UProperty>:
                UPropertyHelper.WriteNestedPropertyListsJson(writer, elements.OfType<List<UProperty>>());
                break;

            default:
                throw new NotImplementedException("Dynamic array type not implemented.");
        }
    }

    private void SerializeArrayContents(UnrealBinaryWriter writer)
    {
        object? firstElement = UPropertyHelper.GetFirstOrNull(elements);

        switch (firstElement)
        {
            case int:
                foreach (int element in elements.OfType<int>())
                    writer.Write(element);
                break;

            case float:
                foreach (float element in elements.OfType<float>())
                    writer.Write(element);
                break;

            case bool:
                foreach (bool element in elements.OfType<bool>())
                    writer.Write(element);
                break;

            case string:
                foreach (string element in elements.OfType<string>())
                    writer.WriteUnrealString(element);
                break;

            case List<UProperty>:
                UPropertyHelper.SerializeNestedPropertyLists(writer, elements.OfType<List<UProperty>>());
                break;

            default:
                throw new NotImplementedException("Dynamic array type not implemented.");
        }
    }
}

/// <summary>
/// Helper methods shared across UProperty implementations.
/// Only reusable mechanics live here.
/// </summary>
public static class UPropertyHelper
{
    public static int GetLittleEndianStringLength(string str)
    {
        if (str == string.Empty)
            return sizeof(int);

        return UPropertyLayout.VALUE_SIZE + str.Length + UPropertyLayout.NULL_TERMINATOR_SIZE;
    }

    public static void PopulatePropertyMetadataSize(UProperty property)
    {
        property.uPropertyElementSize ??= 0;
        property.uPropertyElementSize += GetLittleEndianStringLength(property.name);
        property.uPropertyElementSize += GetLittleEndianStringLength(property.type);
        property.uPropertyElementSize += UPropertyLayout.VALUE_SIZE;
        property.uPropertyElementSize += UPropertyLayout.ARRAY_INDEX_SIZE;
        property.uPropertyElementSize += property.size;
    }

    public static string ReaderValueToString(JsonTextReader reader)
    {
        if (reader.Value is null)
            throw new InvalidCastException($"Reader.Value is null. Expected a value, got {reader.TokenType}");

        return reader.Value.ToString() ?? string.Empty;
    }

    internal static T ParseReaderValue<T>(JsonTextReader reader, TryParseDelegate<T> tryParse) where T : struct
    {
        if (reader.Value is null)
            throw new InvalidCastException($"Reader.Value is null for {reader.TokenType}");

        string str = reader.Value.ToString()!;

        if (!tryParse(str, out T result))
            throw new ArgumentException($"Cannot convert '{str}' to {typeof(T).Name}");

        return result;
    }

    internal protected delegate bool TryParseDelegate<T>(string input, out T result);

    public static int CalculatePropertyContentSize(List<UProperty> properties)
        => CalculatePropertyListSize(properties);

    public static int CalculatePropertyContentSize(List<object> elements)
    {
        if (elements.Count == 0)
            return 0;

        return elements[0] switch
        {
            string => CalculateStringArraySize(elements.OfType<string>()),
            int => elements.Count * sizeof(int),
            float => elements.Count * sizeof(float),
            bool => elements.Count * sizeof(bool),
            List<UProperty> => CalculateNestedPropertyListsSize(elements.OfType<List<UProperty>>()),
            UProperty => CalculatePropertyListSize(elements.OfType<UProperty>()),
            _ => throw new NotImplementedException("Dynamic array size calculation not implemented for this type.")
        };
    }

    public static void WritePropertyListJson(Utf8JsonWriter writer, IEnumerable<UProperty> properties)
    {
        foreach (UProperty property in properties)
            property.WriteValueData(writer, property.name);
    }

    public static void WriteNestedPropertyListsJson(Utf8JsonWriter writer, IEnumerable<List<UProperty>> nestedPropertyLists)
    {
        foreach (List<UProperty> propertyList in nestedPropertyLists)
        {
            writer.WriteStartObject();
            WritePropertyListJson(writer, propertyList);
            writer.WriteEndObject();
        }
    }

    public static void SerializePropertyList(UnrealBinaryWriter writer, IEnumerable<UProperty> properties)
    {
        foreach (UProperty property in properties)
        {
            writer.WritePropertyMetadata(property);
            property.SerializeValue(writer);
        }

        writer.WriteUnrealString(UType.NONE);
    }

    public static void SerializeNestedPropertyLists(UnrealBinaryWriter writer, IEnumerable<List<UProperty>> nestedPropertyLists)
    {
        foreach (List<UProperty> propertyList in nestedPropertyLists)
            SerializePropertyList(writer, propertyList);
    }

    public static object? GetFirstOrNull(List<object> elements)
    {
        if (elements.Count == 0)
            return null;

        return elements[0];
    }

    private static int CalculateStringArraySize(IEnumerable<string> elements)
    {
        int totalSize = 0;

        foreach (string element in elements)
            totalSize += GetLittleEndianStringLength(element);

        return totalSize;
    }

    private static int CalculateNestedPropertyListsSize(IEnumerable<List<UProperty>> nestedPropertyLists)
    {
        int totalSize = 0;

        foreach (List<UProperty> propertyList in nestedPropertyLists)
            totalSize += CalculatePropertyListSize(propertyList);

        return totalSize;
    }

    private static int CalculatePropertyListSize(IEnumerable<UProperty> properties)
    {
        int totalSize = 0;

        foreach (UProperty property in properties)
            totalSize += property.uPropertyElementSize ?? 0;

        totalSize += GetLittleEndianStringLength(UType.NONE);
        return totalSize;
    }
}