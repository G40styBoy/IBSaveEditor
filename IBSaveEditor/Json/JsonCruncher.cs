using Newtonsoft.Json;

namespace SaveDumper.JsonCruncher;

/// <summary>
/// Takes in a json file with deserialized data, and crunches it into a digestable format to serialize the data
/// </summary>
class JsonDataCruncher
{
    private const string ENUM_PREFIX = "e";
    private const string FNAME_PREFIX = "ini_";
    private const string BYTE_PREFIX = "b";
    private const int NORMAL_READER_DEPTH = 1;
    private readonly HashSet<string> SpecialEnumNames = new() { "eCurrentPlayerType" };
    private readonly HashSet<string> SpecialIntNames = new() { "bWasEncrypted" };
    private readonly HashSet<string> SpecialStructNames = new() { "SavedCheevo" };

    private JsonTextReader reader;
    private List<UProperty> crunchedList = new List<UProperty>();
    private List<ArrayMetadata> arrayData;
    private UPropertyDataHelper uHelper;

    public JsonDataCruncher(string jsonFile, Game type)
    {
        string jsonFileText = File.ReadAllText(jsonFile);
        reader = new JsonTextReader(new StringReader(jsonFileText));
        arrayData = UArray.PopulateArrayInfo(type);
        uHelper = new UPropertyDataHelper();
    }

    internal List<UProperty> ReadJsonFile()
    {
        try
        {
            while (reader.Read())
            {
                // skip start and end token
                if (reader.Depth <= NORMAL_READER_DEPTH && reader.TokenType is JsonToken.StartObject or JsonToken.EndObject)
                    continue;
                ReadJsonProperty(out UProperty property);
            }

            return crunchedList;
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception);
            return null!;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool ShouldTreatAsByteProperty(string name) => name.StartsWith(BYTE_PREFIX) && !SpecialIntNames.Contains(name);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool ShouldTreatAsEnumProperty(string name) => name.StartsWith(ENUM_PREFIX);

    private bool IsSpecialStruct(string name) => SpecialStructNames.Contains(name);

    private bool IsNestedProperty() => reader.Depth > NORMAL_READER_DEPTH;

    private bool ReadJsonList() => reader.Read() && reader.TokenType is not JsonToken.EndObject;
    private bool ReadJsonDictionary() => reader.Read() && reader.TokenType is not JsonToken.EndArray;

    private void AddPropertyToCollection(UProperty property) => crunchedList.Add(property);
    private void AddPropertyListToCollection(List<UProperty> propertyList)
    {
        foreach (var element in propertyList)
            crunchedList.Add(element);
    }

    private void ReadJsonProperty(out UProperty property, bool addToCrunchCollection = true)
    {
        ReadPropertyName(out TagContainer tag);
        if (IsNestedProperty())
            tag.bShouldTrackMetadataSize = true;
        property = ConstructUProperty(tag);

        if (addToCrunchCollection && property is not null)
            AddPropertyToCollection(property);
    }

    private void ReadPropertyName(out TagContainer tag)
    {
        tag = new TagContainer();
        tag.name = uHelper.ReaderValueToString(reader);
        if (tag.name is null)
            throw new InvalidOperationException("Property name is null");
    }

    private UProperty ConstructUProperty(TagContainer tag)
    {
        reader.Read();
        return reader.TokenType switch
        {
            JsonToken.Integer => JsonInteger(tag),
            JsonToken.Float => JsonFloat(tag),
            JsonToken.Boolean => JsonBoolean(tag),
            JsonToken.String => JsonString(tag),
            JsonToken.StartObject => JsonObject(tag),
            JsonToken.StartArray => JsonArray(tag),
            // TODO: need to account for empty arrays more gracefully. Right now its kinda messy \
            // we account and at times expect this, tell compiler to ignore
            JsonToken.EndArray => null!,
            _ => throw new NotSupportedException($"Unsupported property type: {tag.type}")
        };
    }

    private UProperty JsonInteger(TagContainer tag)
    {
        if (ShouldTreatAsByteProperty(tag.name))
        {
            RemovePrefix(ref tag.name, BYTE_PREFIX);
            PopulateUPropertyMetadata(ref tag, UType.BYTE_PROPERTY, sizeof(byte), 0);
            return UByteProperty.InstantiateProperty(ref reader, tag);
        }

        PopulateUPropertyMetadata(ref tag, UType.INT_PROPERTY, sizeof(int), 0);
        return new UIntProperty(reader, tag);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private UProperty JsonFloat(TagContainer tag)
    {
        PopulateUPropertyMetadata(ref tag, UType.FLOAT_PROPERTY, sizeof(float), 0);
        return new UFloatProperty(reader, tag);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private UProperty JsonBoolean(TagContainer tag)
    {
        PopulateUPropertyMetadata(ref tag, UType.BOOL_PROPERTY, UProperty.BYTE_SIZE_SPECIAL, 0);
        return new UBoolProperty(reader, tag);
    }

    private UProperty JsonString(TagContainer tag)
    {
        if (tag.name.StartsWith(FNAME_PREFIX))
        {
            RemovePrefix(ref tag.name, FNAME_PREFIX);
            PopulateUPropertyMetadata(ref tag, UType.NAME_PROPERTY, 0, 0);
        }
        else
            PopulateUPropertyMetadata(ref tag, UType.STR_PROPERTY, 0, 0);
        return new UStringProperty(reader, tag);
    }

    private UProperty JsonObject(TagContainer tag)
    {
        // sizes here are determined by the value of the property. calculate these in constructors
        if (ShouldTreatAsEnumProperty(tag.name))
        {
            RemovePrefix(ref tag.name, ENUM_PREFIX, SpecialEnumNames);
            PopulateUPropertyMetadata(ref tag, UType.BYTE_PROPERTY, 0, 0);
            return UByteProperty.InstantiateProperty(ref reader, tag);
        }

        // stand-alone struct logic
        var elements = ReadStructElement(reader);
        PopulateUPropertyMetadata(ref tag, UType.STRUCT_PROPERTY, 0, 0);
        return new UStructProperty(reader, tag, elements, string.Empty);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RemovePrefix(ref string text, string prefix)
    {
        if (text.StartsWith(prefix))
            text = text[prefix.Length..];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RemovePrefix(ref string text, string prefix, HashSet<string> specialCase)
    {
        if (text.StartsWith(prefix) && !specialCase.Contains(text))
            text = text[prefix.Length..];
    }

    private UProperty JsonArray(TagContainer tag)
    {
        tag.arrayInfo = UArray.GetCurrentArray(arrayData, tag.name)!;
        if (tag.arrayInfo is null)
            throw new InvalidOperationException($"{tag.arrayInfo} is null");

        if (tag.arrayInfo.arrayType is ArrayType.Dynamic)
        {
            // Even if an array is empty, its size will include the bytes of the array entry count
            PopulateUPropertyMetadata(ref tag, UType.ARRAY_PROPERTY, sizeof(int), 0);
            return tag.arrayInfo.valueType switch
            {
                PropertyType.IntProperty => BuildArrayProperty(tag, _ => uHelper.ParseReaderValue<int>(reader, int.TryParse)),
                PropertyType.FloatProperty => BuildArrayProperty(tag, _ => uHelper.ParseReaderValue<float>(reader, float.TryParse)),
                PropertyType.StrProperty or PropertyType.NameProperty => BuildArrayProperty(tag, _ => uHelper.ReaderValueToString(reader)),
                PropertyType.StructProperty => BuildArrayProperty(tag, _ => ReadStructElement(reader)),
                _ => throw new NotSupportedException($"Unsupported dynamic array type: {tag.arrayInfo.valueType}")
            };
        }
        else if (tag.arrayInfo.arrayType is ArrayType.Static)
        {
            // we dont expect data back here since we are generating multiple properties
            // return null and let the logic handle itself
            switch (tag.arrayInfo.valueType)
            {
                // only ints, fnames, and structs encounter static arrays
                case PropertyType.IntProperty:
                    ReconstructIntProperty(tag);
                    break;
                case PropertyType.NameProperty:
                    ReconstructFNameProperty(tag);
                    break;
                case PropertyType.StructProperty:
                    ReconstructStructProperty(tag);
                    break;
                default: throw new NotSupportedException($"Unsupported static array type: {tag.arrayInfo.valueType}");
            }
            return null!;
        }

        // This should never happen, but account for it
        throw new InvalidOperationException();
    }

    private void ReconstructIntProperty(TagContainer parentTag)
    {
        var reconstructedIntPropertyList = new List<UProperty>();
        string parentName = parentTag.arrayInfo.arrayName.ToString();
        // we always get the enum type here since only 1 property type executes this method
        Type enumType = IBEnum.GetArrayIndexEnum(parentName);

        //skip over "{" 
        reader.Read();
        while (ReadJsonList())
        {
            ReadPropertyName(out TagContainer tag);
            int arrayIndex = IBEnum.GetArrayIndexUsingReflection(enumType, uHelper.ReaderValueToString(reader));
            PopulateUPropertyMetadata(ref tag, UType.INT_PROPERTY, sizeof(int), arrayIndex);

            tag.name = parentName;
            reader.Read();

            reconstructedIntPropertyList.Add(new UIntProperty(reader, tag));
        }
        AddPropertyListToCollection(reconstructedIntPropertyList);

        // skip over "]"
        reader.Read();
    }

    private void ReconstructFNameProperty(TagContainer parentTag)
    {
        var reconstructedFNamePropertyList = new List<UProperty>();
        string parentName = parentTag.arrayInfo.arrayName.ToString();
        int arrayIndex = 0;

        while (ReadJsonDictionary())
        {
            var tag = new TagContainer();
            tag.name = parentName;
            PopulateUPropertyMetadata(ref tag, UType.NAME_PROPERTY, 0, arrayIndex);

            reconstructedFNamePropertyList.Add(new UStringProperty(reader, tag));
            arrayIndex++;
        }

        AddPropertyListToCollection(reconstructedFNamePropertyList);
    }

    private void ReconstructStructProperty(TagContainer parentTag)
    {
        var reconstructedStructList = new List<UProperty>();
        string parentName = parentTag.arrayInfo.arrayName.ToString();
        int arrayIndex = 0;
        bool shouldCalculateIndex = IsSpecialStruct(parentTag.name);
        Type enumType = null!;

        // read over the object that encapsulates our static struct data
        if (shouldCalculateIndex)
        {
            reader.Read();
            enumType = IBEnum.GetArrayIndexEnum(parentName);
        }

        while (ReadJsonDictionary())
        {
            var tag = new TagContainer();
            tag.name = parentName;

            if (shouldCalculateIndex)
            {
                // read over the end object that encapsulates our data and break out of the loop
                if (reader.TokenType is JsonToken.EndObject)
                {
                    reader.Read();
                    break;
                }

                arrayIndex = IBEnum.GetArrayIndexUsingReflection(enumType, uHelper.ReaderValueToString(reader));
            }
            PopulateUPropertyMetadata(ref tag, UType.STRUCT_PROPERTY, 0, arrayIndex);

            var elements = ReadStructElement(reader);

            var property = new UStructProperty(reader, tag, elements, parentTag.arrayInfo.alternateName.ToString());
            reconstructedStructList.Add(property);

            arrayIndex++;
        }

        AddPropertyListToCollection(reconstructedStructList);
    }

    private UArrayProperty<T> BuildArrayProperty<T>(TagContainer tag, Func<JsonTextReader, T> function) where T : notnull
    {
        var elements = new List<T>();

        while (ReadJsonDictionary())
            elements.Add(function(reader));

        return new UArrayProperty<T>(reader, tag, elements);
    }

    private List<UProperty> ReadStructElement(JsonTextReader reader)
    {
        var elements = new List<UProperty>();

        while (ReadJsonList())
        {
            if (reader.TokenType is JsonToken.StartObject)
                continue;
            else if (reader.TokenType is JsonToken.EndArray)
                break;

            ReadJsonProperty(out UProperty property, addToCrunchCollection: false);
            elements.Add(property);
        }

        return elements;
    }

    /// <summary>
    /// Populate the main metadata for a property.
    /// </summary>
    /// This will be used to parse data we can inference from the property type
    /// This data is without calling the property constructor
    private void PopulateUPropertyMetadata(ref TagContainer tag, string type, int size, int arrayIndex)
    {
        tag.type = type;
        tag.arrayIndex = arrayIndex;
        tag.size = size;
    }
}