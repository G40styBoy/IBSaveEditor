using Newtonsoft.Json;

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

    private JsonTextReader _reader;
    private List<UProperty> crunchedList = new List<UProperty>();
    private Game game;

    public JsonDataCruncher(string jsonFile, Game game)
    {
        this.game = game;
        string jsonFileText = File.ReadAllText(jsonFile);
        _reader = new JsonTextReader(new StringReader(jsonFileText));
    }

    internal List<UProperty> ReadJsonFile()
    {
        try
        {
            while (_reader.Read())
            {
                // skip start and end token
                if (_reader.Depth <= NORMAL_READER_DEPTH && _reader.TokenType is JsonToken.StartObject or JsonToken.EndObject)
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

    private bool IsNestedProperty() => _reader.Depth > NORMAL_READER_DEPTH;

    private bool ReadJsonList() => _reader.Read() && _reader.TokenType is not JsonToken.EndObject;
    private bool ReadJsonDictionary() => _reader.Read() && _reader.TokenType is not JsonToken.EndArray;

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
        tag.name = UPropertyHelper.ReaderValueToString(_reader);
        if (tag.name is null)
            throw new InvalidOperationException("Property name is null");
    }

    private UProperty? ConstructUProperty(TagContainer tag)
    {
        _reader.Read();
        return _reader.TokenType switch
        {
            JsonToken.Integer => JsonInteger(tag),
            JsonToken.Float => JsonFloat(tag),
            JsonToken.Boolean => JsonBoolean(tag),
            JsonToken.String => JsonString(tag),
            JsonToken.StartObject => JsonObject(tag),
            JsonToken.StartArray => JsonArray(tag),
            // TODO: need to account for empty arrays more gracefully. Right now its kinda messy \
            // we account and at times expect this, tell compiler to ignore
            JsonToken.EndArray => null,
            _ => throw new NotSupportedException($"Unsupported property type: {tag.type}")
        };
    }

    private UProperty JsonInteger(TagContainer tag)
    {
        if (ShouldTreatAsByteProperty(tag.name))
        {
            RemovePrefix(ref tag.name, BYTE_PREFIX);
            PopulateUPropertyMetadata(ref tag, UType.BYTE_PROPERTY, sizeof(byte), 0);
            return UByteProperty.InstantiateProperty(ref _reader, tag);
        }

        PopulateUPropertyMetadata(ref tag, UType.INT_PROPERTY, sizeof(int), 0);
        return new UIntProperty(_reader, tag);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private UProperty JsonFloat(TagContainer tag)
    {
        PopulateUPropertyMetadata(ref tag, UType.FLOAT_PROPERTY, sizeof(float), 0);
        return new UFloatProperty(_reader, tag);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private UProperty JsonBoolean(TagContainer tag)
    {
        PopulateUPropertyMetadata(ref tag, UType.BOOL_PROPERTY, UProperty.BYTE_SIZE_SPECIAL, 0);
        return new UBoolProperty(_reader, tag);
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
        return new UStringProperty(_reader, tag);
    }

    private UProperty JsonObject(TagContainer tag)
    {
        // sizes here are determined by the value of the property. calculate these in constructors
        if (ShouldTreatAsEnumProperty(tag.name))
        {
            RemovePrefix(ref tag.name, ENUM_PREFIX, SpecialEnumNames);
            PopulateUPropertyMetadata(ref tag, UType.BYTE_PROPERTY, 0, 0);
            return UByteProperty.InstantiateProperty(ref _reader, tag);
        }

        // stand-alone struct logic
        var elements = ReadStructElement(_reader);
        PopulateUPropertyMetadata(ref tag, UType.STRUCT_PROPERTY, 0, 0);
        return new UStructProperty(_reader, tag, elements, string.Empty);
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

    private UProperty? JsonArray(TagContainer tag)
    {
        if (UArrayRegistry.TryGet(game, tag.name, out var metadata))
            tag.arrayInfo = metadata!;
        else
            throw new InvalidOperationException($"{tag.arrayInfo} is null");

        if (tag.arrayInfo.arrayType is ArrayType.Dynamic)
        {
            // Even if an array is empty, its size will include the bytes of the array entry count
            PopulateUPropertyMetadata(ref tag, UType.ARRAY_PROPERTY, sizeof(int), 0);
            return tag.arrayInfo.valueType switch
            {
                PropertyType.IntProperty => BuildArrayProperty(tag, _ => UPropertyHelper.ParseReaderValue<int>(_reader, int.TryParse)),
                PropertyType.FloatProperty => BuildArrayProperty(tag, _ => UPropertyHelper.ParseReaderValue<float>(_reader, float.TryParse)),
                PropertyType.StrProperty or PropertyType.NameProperty => BuildArrayProperty(tag, _ => UPropertyHelper.ReaderValueToString(_reader)),
                PropertyType.StructProperty => BuildArrayProperty(tag, _ => ReadStructElement(_reader)),
                _ => throw new NotSupportedException($"Unsupported dynamic array type: {tag.arrayInfo.valueType}")
            };
        }
        else 
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
            return null;
        }
    }

    private void ReconstructIntProperty(TagContainer parentTag)
    {
        var reconstructedIntPropertyList = new List<UProperty>();
        string parentName = parentTag.arrayInfo.arrayName.ToString();
        // we always get the enum type here since only 1 property type executes this method
        Type enumType = IBEnum.GetArrayIndexEnum(parentName);

        //skip over "{" 
        _reader.Read();
        while (ReadJsonList())
        {
            ReadPropertyName(out TagContainer tag);
            int arrayIndex = IBEnum.GetArrayIndexUsingReflection(enumType, UPropertyHelper.ReaderValueToString(_reader));
            PopulateUPropertyMetadata(ref tag, UType.INT_PROPERTY, sizeof(int), arrayIndex);

            tag.name = parentName;
            _reader.Read();

            reconstructedIntPropertyList.Add(new UIntProperty(_reader, tag));
        }
        AddPropertyListToCollection(reconstructedIntPropertyList);

        // skip over "]"
        _reader.Read();
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

            reconstructedFNamePropertyList.Add(new UStringProperty(_reader, tag));
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
            _reader.Read();
            enumType = IBEnum.GetArrayIndexEnum(parentName);
        }

        while (ReadJsonDictionary())
        {
            var tag = new TagContainer();
            tag.name = parentName;

            if (shouldCalculateIndex)
            {
                // read over the end object that encapsulates our data and break out of the loop
                if (_reader.TokenType is JsonToken.EndObject)
                {
                    _reader.Read();
                    break;
                }

                arrayIndex = IBEnum.GetArrayIndexUsingReflection(enumType, UPropertyHelper.ReaderValueToString(_reader));
            }
            PopulateUPropertyMetadata(ref tag, UType.STRUCT_PROPERTY, 0, arrayIndex);

            var elements = ReadStructElement(_reader);
            var property = new UStructProperty(_reader, tag, elements, parentTag.arrayInfo.alternateName.ToString());
            reconstructedStructList.Add(property);

            arrayIndex++;
        }

        AddPropertyListToCollection(reconstructedStructList);
    }

    private UArrayProperty<T> BuildArrayProperty<T>(TagContainer tag, Func<JsonTextReader, T> function) where T : notnull
    {
        var elements = new List<T>();

        while (ReadJsonDictionary())
            elements.Add(function(_reader));

        return new UArrayProperty<T>(_reader, tag, elements);
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