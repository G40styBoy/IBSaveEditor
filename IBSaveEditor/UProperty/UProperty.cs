using System.Text.Json;
using Newtonsoft.Json;
using System.Globalization;

/// <summary>
/// Class handling everything UObjects. Mainly stores metadata
/// </summary>
public abstract class UProperty
{
    public const int NULL_TERMINATOR = sizeof(byte);
    public const int ARRAY_INDEX_SIZE = sizeof(int);
    public const int VALUE_SIZE = sizeof(int);
    /// <summary>
    /// Size used for booleans inside of serialized data. Boolean size should usually be a byte
    /// </summary>
    public const int BYTE_SIZE_SPECIAL = 0;

    public string name;
    public string type;
    public int size;
    public int arrayIndex;
    public int? uPropertyElementSize; // This value is seperately constructed due to its special construction requirments

    public UProperty(TagContainer tag)
    {
        name = tag.name;
        type = tag.type;
        arrayIndex = tag.arrayIndex;
        size = tag.size;
    }

    private protected bool ShouldTrackFullSize(TagContainer tag) => tag.bShouldTrackMetadataSize is true;

    public abstract void WriteValueData(Utf8JsonWriter writer);
    public abstract void WriteValueData(Utf8JsonWriter writer, string name);
    public abstract void SerializeValue(BinaryWriter writer);
}

public class UIntProperty : UProperty
{
    public int value;
    public UIntProperty(UnrealPackage UPK, TagContainer tag)
        : base(tag) => value = UPK.DeserializeInt();

    public UIntProperty(JsonTextReader reader, TagContainer tag) : base(tag)
    {
        value = UPropertyHelper.ParseReaderValue<int>(reader, int.TryParse);

        if (ShouldTrackFullSize(tag))
            UPropertyHelper.PopulatePropertyMetadataSize(this);
    }

    public override void WriteValueData(Utf8JsonWriter writer) { }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void WriteValueData(Utf8JsonWriter writer, string name) => writer.WriteNumber(name, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void SerializeValue(BinaryWriter writer) => writer.Write(value); 
}

public class UFloatProperty : UProperty
{
    public float value;
    private readonly string doubleFormat = "0.0#########"; // More decimal places                              
    private readonly CultureInfo cultureInfo = CultureInfo.InvariantCulture;
    public UFloatProperty(UnrealPackage UPK, TagContainer tag)
        : base(tag) => value = UPK.DeserializeFloat();

    public UFloatProperty(JsonTextReader reader, TagContainer tag) : base(tag)
    {
        value = UPropertyHelper.ParseReaderValue<float>(reader, float.TryParse);
        if (ShouldTrackFullSize(tag))
            UPropertyHelper.PopulatePropertyMetadataSize(this);
    }

    public override void WriteValueData(Utf8JsonWriter writer) { }
    public override void WriteValueData(Utf8JsonWriter writer, string name)
    {
        // Since json rounds up .0 values it causes an issue for us when interpreting the json data
        // with this we work around the program omitting the .0 suffix
        writer.WritePropertyName(name);
        writer.WriteRawValue(value.ToString(doubleFormat, cultureInfo));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void SerializeValue(BinaryWriter writer) => writer.Write(value); 
}

public class UBoolProperty : UProperty
{
    public bool value;
    public UBoolProperty(UnrealPackage UPK, TagContainer tag)
        : base(tag) => value = UPK.DeserializeBool();

    public UBoolProperty(JsonTextReader reader, TagContainer tag) : base(tag)
    {
        value = UPropertyHelper.ParseReaderValue<bool>(reader, bool.TryParse);

        if (ShouldTrackFullSize(tag))
        {
            // with bools, they're value size is serialized as 0
            // we need to account for this when calculating its metadata size   
            uPropertyElementSize = sizeof(bool);
            UPropertyHelper.PopulatePropertyMetadataSize(this);
        }
    }
    public override void WriteValueData(Utf8JsonWriter writer) { }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void WriteValueData(Utf8JsonWriter writer, string name) => writer.WriteBoolean(name, value);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void SerializeValue(BinaryWriter writer) => writer.Write(value); 
}

public class UStringProperty : UProperty
{    
    public string value = string.Empty;

    public UStringProperty(UnrealPackage UPK, TagContainer tag) : base(tag) => value = UPK.DeserializeString();

    public UStringProperty(JsonTextReader reader, TagContainer tag) : base(tag)
    {      
        value = UPropertyHelper.ReaderValueToString(reader);
        size = UPropertyHelper.ReturnLitteEndianStringLength(value);
        if (ShouldTrackFullSize(tag))
            UPropertyHelper.PopulatePropertyMetadataSize(this);
    }

    public override void WriteValueData(Utf8JsonWriter writer) => writer.WriteStringValue(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void WriteValueData(Utf8JsonWriter writer, string name) => writer.WriteString(name, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void SerializeValue(BinaryWriter writer) => UPropertyHelper.SerializeString(ref writer, value); 
}

public class UNameProperty : UProperty
{
    private const string FNAME_PREFIX = "ini_";
    // private bool useFriendly = Program.config.useFriendlyName;
    public string value = string.Empty;

    public UNameProperty(UnrealPackage UPK, TagContainer tag) : base(tag) => value = UPK.DeserializeString();

    public UNameProperty(JsonTextReader reader, TagContainer tag) : base(tag)
    {      
        value = UPropertyHelper.ReaderValueToString(reader);
        size = UPropertyHelper.ReturnLitteEndianStringLength(value);
        if (ShouldTrackFullSize(tag))
            UPropertyHelper.PopulatePropertyMetadataSize(this);
    }

    public override void WriteValueData(Utf8JsonWriter writer) => writer.WriteStringValue(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void WriteValueData(Utf8JsonWriter writer, string name)
    {
        name = $"{FNAME_PREFIX}{name}";
        writer.WriteString(name, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void SerializeValue(BinaryWriter writer) => UPropertyHelper.SerializeString(ref writer, value); 
}

public abstract class UByteProperty : UProperty
{
    public UByteProperty(TagContainer tag) : base(tag) {}

    public static UByteProperty InstantiateProperty(UnrealPackage UPK, TagContainer tag)
    {
        var identifier = UPK.DeserializeString();
        return tag.size switch
        {
            sizeof(byte) => new USimpleByteProperty(UPK, tag),
            > sizeof(byte) => new UEnumByteProperty(UPK, tag, identifier),
            _ => throw new NotSupportedException($"Unsupported byte property size: {tag.size}")
        };
    }

    public static UByteProperty InstantiateProperty(ref JsonTextReader reader, TagContainer tag)
    {
        return tag.size switch
        {
            sizeof(byte) => new USimpleByteProperty(reader, tag),
            0 => new UEnumByteProperty(ref reader, tag),
            _ => throw new NotSupportedException($"Unsupported byte property size: {tag.size}")
        };
    }
}

public class USimpleByteProperty : UByteProperty
{
    public byte value;
    public USimpleByteProperty(UnrealPackage UPK, TagContainer tag)
        : base(tag) => value = UPK.DeserializeByte();

    public USimpleByteProperty(JsonTextReader reader, TagContainer tag) : base(tag)
    {
        value = UPropertyHelper.ParseReaderValue<byte>(reader, byte.TryParse);

        if (ShouldTrackFullSize(tag))
        {
            // since there is no enumerator name, we need to serialize none in its place
            uPropertyElementSize = UPropertyHelper.ReturnLitteEndianStringLength(UType.NONE);
            UPropertyHelper.PopulatePropertyMetadataSize(this);
        }
    }

    public override void WriteValueData(Utf8JsonWriter writer) { }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void WriteValueData(Utf8JsonWriter writer, string name) => writer.WriteNumber($"b{name}", value);
    public override void SerializeValue(BinaryWriter writer)
    {
        UPropertyHelper.SerializeString(ref writer, "None");
        writer.Write(value);
    }
}

public class UEnumByteProperty : UByteProperty
{
    public string enumName = string.Empty;
    public string enumValue = string.Empty;
    private const string ENUM_PREFIX = "e";
    private const string EDGECASE_PLAYERTYPE = "eCurrentPlayerType";
    private const string ENUM_NAME = "Enum";
    private const string ENUM_VALUE = "Enum Value";


    public UEnumByteProperty(UnrealPackage UPK, TagContainer tag, string enumName) : base(tag)
    {
        this.enumName = enumName;
        enumValue = UPK.DeserializeString();
    }

    public UEnumByteProperty(ref JsonTextReader reader, TagContainer tag) : base(tag)
    {
        if (CheckPropertyName(ref reader, ENUM_NAME))
            enumName = UPropertyHelper.ReaderValueToString(reader);

        if (CheckPropertyName(ref reader, ENUM_VALUE))
            enumValue = UPropertyHelper.ReaderValueToString(reader);

        size = UPropertyHelper.ReturnLitteEndianStringLength(enumValue);
        // read past the "}" closing statement so our logic doesnt run into issues
        reader.Read();

        if (ShouldTrackFullSize(tag))
        {
            uPropertyElementSize = UPropertyHelper.ReturnLitteEndianStringLength(enumName);
            UPropertyHelper.PopulatePropertyMetadataSize(this);
        }
    }

    private bool CheckPropertyName(ref JsonTextReader reader, string stringExpected)
    {
        reader.Read();
        if (UPropertyHelper.ReaderValueToString(reader) != stringExpected)
            throw new InvalidDataException($"Expected {stringExpected} as a property name.");
        reader.Read();
        return true;
    }

    public override void WriteValueData(Utf8JsonWriter writer) { }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void WriteValueData(Utf8JsonWriter writer, string name)
    {
        // Edge case since "eCurrentPlayerType" is the only enum that is prefixed with e
        // this is the same name that gets loaded into the loaders .obj, so we cant change it
        if (name is not EDGECASE_PLAYERTYPE)
            name = $"{ENUM_PREFIX}{name}";
        writer.WriteStartObject(name);
        writer.WriteString(ENUM_NAME, enumName);
        writer.WriteString(ENUM_VALUE, enumValue);
        writer.WriteEndObject();
    }

    public override void SerializeValue(BinaryWriter writer)
    {
        UPropertyHelper.SerializeString(ref writer, enumName);
        UPropertyHelper.SerializeString(ref writer, enumValue);
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
        size += UPropertyHelper.CalculateSpecialPropertyContentSize(elements);

        if (this.structName == string.Empty)
            this.structName = AttemptResolveAltName();

        uPropertyElementSize = UPropertyHelper.ReturnLitteEndianStringLength(this.structName);
        UPropertyHelper.PopulatePropertyMetadataSize(this);
    }

    /// <summary>
    /// used for structs with alternames embedded in static arrays
    /// <summary/>
    private string AttemptResolveAltName()
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

    private void LoopJsonParsing(Utf8JsonWriter writer)
    {
        foreach (var element in elements)
            element.WriteValueData(writer, element.name);
    }

    public override void WriteValueData(Utf8JsonWriter writer) => LoopJsonParsing(writer);
    public override void WriteValueData(Utf8JsonWriter writer, string name)
    {
        writer.WriteStartObject(name);
        LoopJsonParsing(writer);
        writer.WriteEndObject();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void SerializeValue(BinaryWriter writer)
    {
        if (structName != string.Empty)
                UPropertyHelper.SerializeString(ref writer, structName);

        foreach (var element in elements)
        {
            UPropertyHelper.SerializeMetadata(ref writer, element);
            element.SerializeValue(writer);
        }

        UPropertyHelper.SerializeString(ref writer, "None");
    }
}

public class UArrayProperty<T> : UProperty
{
    public ArrayMetadata arrayInfo;
    public int arrayEntryCount;
    public List<T> elements;
    private const string EDGECASE_CHEEVO = "SavedCheevo";

    public UArrayProperty(TagContainer tag, List<T> elements) : base(tag)
    {
        arrayEntryCount = tag.arrayEntryCount;
        this.elements = elements;
        arrayInfo = tag.arrayInfo;
    }

    public UArrayProperty(JsonTextReader reader, TagContainer tag, List<T> elements) : base(tag)
    {
        arrayEntryCount = elements.Count;
        this.elements = elements;
        arrayInfo = tag.arrayInfo;
        size += UPropertyHelper.CalculateSpecialPropertyContentSize(elements);
        UPropertyHelper.PopulatePropertyMetadataSize(this);
    }

    private void LoopJsonParsing(Utf8JsonWriter writer)
    {
        if (arrayInfo.arrayType is ArrayType.Dynamic)
        {
            WriteDynamicElements(writer);
            return;
        }

        // dealing with only static arrays now
        // static arrays with the type "Array" arent supported
        if (arrayInfo.valueType is PropertyType.StructProperty)
        {
            if (name is EDGECASE_CHEEVO)
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
            return;
        }

        if (arrayInfo.valueType is PropertyType.IntProperty)
        {
            writer.WriteStartObject();
            LoopFunctionOverVariablesOfElementType<UProperty>(element => element.WriteValueData(writer, IBEnum.GetEnumEntryFromIndex(name, element.arrayIndex)));
            writer.WriteEndObject();
            return;
        }
        else
        {
            LoopFunctionOverVariablesOfElementType<UProperty>(element => element.WriteValueData(writer));
            return;
        }

        throw new NotImplementedException($"Unsupported array type: {arrayInfo.valueType}");
    }

    private void WriteDynamicElements(Utf8JsonWriter writer)
    {
        if (arrayEntryCount is 0)
            return;

        switch (elements[0])
        {
            case string:
                LoopFunctionOverVariablesOfElementType<string>(writer.WriteStringValue);
                break;
            case int:
                LoopFunctionOverVariablesOfElementType<int>(writer.WriteNumberValue);
                break;
            case float:
                LoopFunctionOverVariablesOfElementType<float>(writer.WriteNumberValue);
                break;
            case bool:
                LoopFunctionOverVariablesOfElementType<bool>(writer.WriteBooleanValue);
                break;
            case List<UProperty>:
                ReadDynamicStruct(writer);
                break;
            default:
                throw new NotImplementedException("Dynamic array type not implemented.");
        }
    }

    private void ReadDynamicStruct(Utf8JsonWriter writer)
    {
        foreach (var element in elements.OfType<List<UProperty>>())
        {
            writer.WriteStartObject();
            // nested array(s)
            foreach (var dynamicElement in element)
                dynamicElement.WriteValueData(writer, dynamicElement.name);
            writer.WriteEndObject();
        }
    }

    private void SerializeDynamicStruct(BinaryWriter writer)
    {
        foreach (var element in elements.OfType<List<UProperty>>())
        {
            // nested array(s)
            foreach (var dynamicElement in element)
            {
                UPropertyHelper.SerializeMetadata(ref writer, dynamicElement);
                dynamicElement.SerializeValue(writer);
            }

            UPropertyHelper.SerializeString(ref writer, "None");
        }
    }

    private void LoopFunctionOverVariablesOfElementType<Type>(Action<Type> func) where Type : notnull
    {
        var castedElements = elements.OfType<Type>();
        foreach (Type element in castedElements)
            func(element);
    }

    public override void WriteValueData(Utf8JsonWriter writer) => LoopJsonParsing(writer);
    public override void WriteValueData(Utf8JsonWriter writer, string name)
    {
        writer.WriteStartArray(name);
        LoopJsonParsing(writer);
        writer.WriteEndArray();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void SerializeValue(BinaryWriter writer)
    {
        writer.Write(arrayEntryCount);

        if (arrayEntryCount is 0)
            return;

        switch (elements[0])
        {
            case int:
                LoopFunctionOverVariablesOfElementType<int>(writer.Write);
                break;
            case float:
                LoopFunctionOverVariablesOfElementType<float>(writer.Write);
                break;
            case bool:
                LoopFunctionOverVariablesOfElementType<bool>(writer.Write);
                break;
            case List<UProperty>:
                SerializeDynamicStruct(writer);
                break;
            // we need a hack here because the writer.Write method for strings appends the size as a stand-alone int
            // this is bad practice, but a hack needs to be in place here
            case string:
                var castedElements = elements.OfType<string>();
                foreach (var element in castedElements)
                    UPropertyHelper.SerializeString(ref writer, element);

                break;
            default:
                throw new NotImplementedException("Dynamic array type not implemented.");
        }
    }
}

/// <summary>
/// Helper class for <see cref="UProperty"/> and any other class that needs it as a dependency.
/// </summary>
public static class UPropertyHelper
{
    public static int ReturnLitteEndianStringLength(string str)
    {
        if (str == string.Empty)
            return sizeof(int);
        return UProperty.VALUE_SIZE + str.Length + UProperty.NULL_TERMINATOR;
    }
    public static void PopulatePropertyMetadataSize(UProperty property)
    {
        property.uPropertyElementSize ??= 0;
        property.uPropertyElementSize += ReturnLitteEndianStringLength(property.name); // name string size
        property.uPropertyElementSize += ReturnLitteEndianStringLength(property.type); // name type size
        property.uPropertyElementSize += UProperty.VALUE_SIZE; // Little endian value size
        property.uPropertyElementSize += UProperty.ARRAY_INDEX_SIZE; // little endian array index
        property.uPropertyElementSize += property.size; // actual size of value
    }

    public static string ReaderValueToString(JsonTextReader reader)
    {
        string str;
        str = reader.Value?.ToString() ?? string.Empty;
        if (reader.Value is null)
            throw new InvalidCastException($"Reader.Value is null. Expected a value, got {reader.TokenType}");
        return str;
    }

    internal static T ParseReaderValue<T>(JsonTextReader reader, TryParseDelegate<T> tryParse) where T : struct
    {
        // we account for a null result, silence warning
        string str = reader.Value?.ToString()!;  
        if (str is null)
            throw new InvalidCastException($"Reader.Value is null for {reader.TokenType}");

        if (!tryParse(str, out T result))
            throw new ArgumentException($"Cannot convert '{str}' to {typeof(T).Name}");

        return result;
    }

    internal protected delegate bool TryParseDelegate<T>(string input, out T result);

    public static int CalculateSpecialPropertyContentSize<T>(List<T> elements)
    {
        if (elements.Count is 0)
            return 0;

        return elements[0] switch
        {
            string => GetSpecialPropertyArraySize(elements),
            int => elements.Count * sizeof(int),
            float => elements.Count * sizeof(float),
            bool => elements.Count * sizeof(bool),
            List<UProperty> => GetStructArraySize(elements),
            UProperty => GetSpecialPropertyStructSize(elements),
            _ => throw new NotImplementedException("Dynamic array size calculation not implemented for this type.")
        };
    }

    private static int GetSpecialPropertyArraySize<T>(List<T> elements)
    {
        int totalSize = 0;

        foreach (string element in elements.OfType<string>())
            totalSize += ReturnLitteEndianStringLength(element);

        return totalSize;
    }

    private static int GetSpecialPropertyStructSize<T>(List<T> elements) => CalculatePropertyListSize(elements.OfType<UProperty>());

    private static int CalculatePropertyListSize(IEnumerable<UProperty> properties)
    {
        int totalSize = 0;

        foreach (var property in properties)
            totalSize += property.uPropertyElementSize ?? 0;

        // Always add the "None" terminator
        totalSize += ReturnLitteEndianStringLength(UType.NONE);

        return totalSize;
    }

    private static int GetStructArraySize<T>(List<T> elements)
    {
        int totalSize = 0;

        foreach (var nestedArray in elements.OfType<List<UProperty>>())
            totalSize += CalculatePropertyListSize(nestedArray);

        return totalSize;
    }

    /// <summary>
    /// Overwrite method for BinaryWriter.write(string) so it does not append the size to the beginning of the string
    /// </summary>
    public static void SerializeString(ref BinaryWriter binWriter, string str)
    {
        // write the size of the 0 string
        if (str == string.Empty)
        {
            binWriter.Write(0);
            return;
        }

        // instead of using binWriter.Write directly for strings, use this work-around 
        // avoids appending the string size as a byte to the beginning of the string in hex
        byte[] strBytes = Encoding.UTF8.GetBytes(str);
        binWriter.Write(str.Length + sizeof(byte)); // str + nt
        binWriter.Write(strBytes);
        binWriter.Write((byte)0);  // null term
    }

    public static void SerializeMetadata(ref BinaryWriter binWriter, UProperty property)
    {
        SerializeString(ref binWriter, property.name);
        SerializeString(ref binWriter, property.type);
        binWriter.Write(property.size);
        binWriter.Write(property.arrayIndex);
    }
}
