using Newtonsoft.Json;
using IBSaveEditor.UProperties;
using IBSaveEditor.UProperties.UArray;
using IBSaveEditor.Package;


namespace IBSaveEditor.Json;
/// <summary>
/// Takes JSON text (not a file) containing deserialized save data and crunches it into a digestible
/// format to serialize the data.
/// </summary>
internal sealed class JsonDataCruncher
{
    private const string ENUM_PREFIX = "e";
    private const string FNAME_PREFIX = "ini_";
    private const string BYTE_PREFIX = "b";
    private const int NORMAL_READER_DEPTH = 1;

    private readonly HashSet<string> SpecialEnumNames = new() { "eCurrentPlayerType" };
    private readonly HashSet<string> SpecialIntNames = new() { "bWasEncrypted" };
    private readonly HashSet<string> SpecialStructNames = new() { "SavedCheevo" };

    private readonly JsonTextReader _reader;
    private readonly List<UProperty> crunchedList = new();
    private readonly Game game;

    public JsonDataCruncher(string jsonText, Game game, bool jsonIsFilePath = false)
    {
        _ = jsonIsFilePath;

        if (jsonText is null)
            throw new ArgumentNullException(nameof(jsonText));
        if (jsonText.Length == 0)
            throw new ArgumentException("JSON text is empty.", nameof(jsonText));

        this.game = game;
        IBEnum.SetGame(game);

        _reader = new JsonTextReader(new StringReader(jsonText))
        {
            CloseInput = true,
            DateParseHandling = DateParseHandling.None
        };
    }

    internal List<UProperty> ReadJsonFile()
    {
        try
        {
            while (_reader.Read())
            {
                if (_reader.Depth <= NORMAL_READER_DEPTH &&
                    _reader.TokenType is JsonToken.StartObject or JsonToken.EndObject)
                    continue;

                ReadJsonProperty(out UProperty? _);
            }

            return crunchedList;
        }
        catch (JsonReaderException jre)
        {
            throw new InvalidOperationException(
                $"Failed to read JSON (Line {jre.LineNumber}, Pos {jre.LinePosition}, Path '{jre.Path}').",
                jre);
        }
        catch (Exception exception) when (exception is not InvalidOperationException)
        {
            throw new InvalidOperationException("Failed to read JSON.", exception);
        }
    }

    private bool ShouldTreatAsByteProperty(string name) =>
        name.StartsWith(BYTE_PREFIX, StringComparison.Ordinal) && !SpecialIntNames.Contains(name);

    private bool ShouldTreatAsEnumProperty(string name) =>
        name.StartsWith(ENUM_PREFIX, StringComparison.Ordinal);

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

    private void ReadJsonProperty(out UProperty? property, bool addToCrunchCollection = true)
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

        string? name = UPropertyHelper.ReaderValueToString(_reader);
        if (string.IsNullOrEmpty(name))
            throw BuildReaderStateException("Property name is null/empty.");

        tag.name = name;
    }

    private UProperty? ConstructUProperty(TagContainer tag)
    {
        ExpectRead("reading property value token");

        return _reader.TokenType switch
        {
            JsonToken.Integer => JsonInteger(tag),
            JsonToken.Float => JsonFloat(tag),
            JsonToken.Boolean => JsonBoolean(tag),
            JsonToken.String => JsonString(tag),
            JsonToken.StartObject => JsonObject(tag),
            JsonToken.StartArray => JsonArray(tag),
            JsonToken.EndArray => null,
            _ => throw BuildReaderStateException($"Unsupported JSON token '{_reader.TokenType}'.")
        };
    }

    private UProperty JsonInteger(TagContainer tag)
    {
        if (ShouldTreatAsByteProperty(tag.name))
        {
            RemovePrefix(ref tag.name, BYTE_PREFIX);
            PopulateUPropertyMetadata(ref tag, UType.BYTE_PROPERTY, sizeof(byte), 0);

            try { return UByteProperty.InstantiateProperty(_reader, tag); }
            catch (Exception ex) { throw BuildReaderStateException($"Failed to build ByteProperty '{tag.name}'.", ex); }
        }

        PopulateUPropertyMetadata(ref tag, UType.INT_PROPERTY, sizeof(int), 0);

        try { return new UIntProperty(_reader, tag); }
        catch (Exception ex) { throw BuildReaderStateException($"Failed to build IntProperty '{tag.name}'.", ex); }
    }

    private UProperty JsonFloat(TagContainer tag)
    {
        PopulateUPropertyMetadata(ref tag, UType.FLOAT_PROPERTY, sizeof(float), 0);

        try { return new UFloatProperty(_reader, tag); }
        catch (Exception ex) { throw BuildReaderStateException($"Failed to build FloatProperty '{tag.name}'.", ex); }
    }

    private UProperty JsonBoolean(TagContainer tag)
    {
        PopulateUPropertyMetadata(ref tag, UType.BOOL_PROPERTY, UPropertyLayout.BYTE_SIZE_SPECIAL, 0);

        try { return new UBoolProperty(_reader, tag); }
        catch (Exception ex) { throw BuildReaderStateException($"Failed to build BoolProperty '{tag.name}'.", ex); }
    }

    private UProperty JsonString(TagContainer tag)
    {
        if (tag.name.StartsWith(FNAME_PREFIX, StringComparison.Ordinal))
        {
            RemovePrefix(ref tag.name, FNAME_PREFIX);
            PopulateUPropertyMetadata(ref tag, UType.NAME_PROPERTY, 0, 0);
        }
        else
        {
            PopulateUPropertyMetadata(ref tag, UType.STR_PROPERTY, 0, 0);
        }

        try { return new UStringProperty(_reader, tag); }
        catch (Exception ex) { throw BuildReaderStateException($"Failed to build String/Name property '{tag.name}'.", ex); }
    }

    private UProperty JsonObject(TagContainer tag)
    {
        if (ShouldTreatAsEnumProperty(tag.name))
        {
            RemovePrefix(ref tag.name, ENUM_PREFIX, SpecialEnumNames);
            PopulateUPropertyMetadata(ref tag, UType.BYTE_PROPERTY, 0, 0);

            try { return UByteProperty.InstantiateProperty(_reader, tag); }
            catch (Exception ex) { throw BuildReaderStateException($"Failed to build Enum(Byte) property '{tag.name}'.", ex); }
        }

        List<UProperty> elements;
        try { elements = ReadStructElement(_reader); }
        catch (Exception ex) { throw BuildReaderStateException($"Failed to read struct elements for '{tag.name}'.", ex); }

        PopulateUPropertyMetadata(ref tag, UType.STRUCT_PROPERTY, 0, 0);

        try { return new UStructProperty(_reader, tag, elements, string.Empty); }
        catch (Exception ex) { throw BuildReaderStateException($"Failed to build StructProperty '{tag.name}'.", ex); }
    }

    private UProperty? JsonArray(TagContainer tag)
    {
        if (!UArrayRegistry.TryGet(game, tag.name, out var metadata) || metadata is null)
            throw BuildReaderStateException($"Missing array metadata for '{tag.name}'.");

        tag.arrayInfo = metadata;

        if (tag.arrayInfo.arrayType is ArrayType.Dynamic)
        {
            PopulateUPropertyMetadata(ref tag, UType.ARRAY_PROPERTY, sizeof(int), 0);

            try
            {
                return tag.arrayInfo.valueType switch
                {
                    PropertyType.IntProperty =>
                        BuildArrayProperty(tag, _ => UPropertyHelper.ParseReaderValue<int>(_reader, int.TryParse)),

                    PropertyType.FloatProperty =>
                        BuildArrayProperty(tag, _ => UPropertyHelper.ParseReaderValue<float>(_reader, float.TryParse)),

                    PropertyType.StrProperty or PropertyType.NameProperty =>
                        BuildArrayProperty(tag, _ => JsonUtils.RequireString(UPropertyHelper.ReaderValueToString(_reader), "array string element")),

                    PropertyType.StructProperty =>
                        BuildArrayProperty(tag, _ => ReadStructElement(_reader)),

                    _ => throw BuildReaderStateException($"Unsupported dynamic array type: {tag.arrayInfo.valueType}")
                };
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            { 
                throw BuildReaderStateException($"Failed to build dynamic array '{tag.name}'.", ex);
            }
        }

        try
        {
            switch (tag.arrayInfo.valueType)
            {
                case PropertyType.IntProperty: ReconstructIntProperty(tag); break;
                case PropertyType.NameProperty: ReconstructFNameProperty(tag); break;
                case PropertyType.StructProperty: ReconstructStructProperty(tag); break;
                default: throw BuildReaderStateException($"Unsupported static array type: {tag.arrayInfo.valueType}");
            }
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw BuildReaderStateException($"Failed to reconstruct static array '{tag.name}'.", ex);
        }

        return null;
    }

    private void RemovePrefix(ref string text, string prefix)
    {
        if (text.StartsWith(prefix, StringComparison.Ordinal))
            text = text[prefix.Length..];
    }

    private void RemovePrefix(ref string text, string prefix, HashSet<string> specialCase)
    {
        if (text.StartsWith(prefix, StringComparison.Ordinal) && !specialCase.Contains(text))
            text = text[prefix.Length..];
    }
    private void ReconstructIntProperty(TagContainer parentTag)
    {
        var reconstructedIntPropertyList = new List<UProperty>();

        string parentName = parentTag.arrayInfo.arrayName.ToString();
        Type enumType;

        try
        {
            enumType = IBEnum.GetArrayIndexEnum(parentName);
        }
        catch (Exception ex)
        {
            throw BuildReaderStateException($"Failed to resolve array index enum for '{parentName}'.", ex);
        }

        // skip over "{"
        ExpectRead("entering static int array object");

        while (ReadJsonList())
        {
            ReadPropertyName(out TagContainer tag);

            string? rawKey = UPropertyHelper.ReaderValueToString(_reader);
            if (string.IsNullOrEmpty(rawKey))
                throw BuildReaderStateException($"Static int array '{parentName}' has null/empty index key.");

            int arrayIndex;
            try
            {
                arrayIndex = IBEnum.GetArrayIndexUsingReflection(enumType, rawKey);
            }
            catch (Exception ex)
            {
                throw BuildReaderStateException($"Failed to map array index '{rawKey}' for '{parentName}'.", ex);
            }

            PopulateUPropertyMetadata(ref tag, UType.INT_PROPERTY, sizeof(int), arrayIndex);

            tag.name = parentName;

            ExpectRead("reading static int array value");

            try
            {
                reconstructedIntPropertyList.Add(new UIntProperty(_reader, tag));
            }
            catch (Exception ex)
            {
                throw BuildReaderStateException($"Failed to build static int element '{parentName}[{arrayIndex}]'.", ex);
            }
        }

        AddPropertyListToCollection(reconstructedIntPropertyList);

        // skip over "]" / end token progression
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

            try
            {
                reconstructedFNamePropertyList.Add(new UStringProperty(_reader, tag));
            }
            catch (Exception ex)
            {
                throw BuildReaderStateException($"Failed to build static fname element '{parentName}[{arrayIndex}]'.", ex);
            }

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
        Type? enumType = null;

        // read over the object that encapsulates our static struct data
        if (shouldCalculateIndex)
        {
            ExpectRead("entering static struct index object");

            try
            {
                enumType = IBEnum.GetArrayIndexEnum(parentName);
            }
            catch (Exception ex)
            {
                throw BuildReaderStateException($"Failed to resolve array index enum for '{parentName}'.", ex);
            }
        }

        while (ReadJsonDictionary())
        {
            var tag = new TagContainer();
            tag.name = parentName;

            if (shouldCalculateIndex)
            {
                // end object that encapsulates our data; break out cleanly
                if (_reader.TokenType is JsonToken.EndObject)
                {
                    _reader.Read();
                    break;
                }

                string? rawKey = UPropertyHelper.ReaderValueToString(_reader);
                if (string.IsNullOrEmpty(rawKey))
                    throw BuildReaderStateException($"Static struct array '{parentName}' has null/empty index key.");

                try
                {
                    arrayIndex = IBEnum.GetArrayIndexUsingReflection(enumType!, rawKey);
                }
                catch (Exception ex)
                {
                    throw BuildReaderStateException($"Failed to map struct array index '{rawKey}' for '{parentName}'.", ex);
                }
            }

            PopulateUPropertyMetadata(ref tag, UType.STRUCT_PROPERTY, 0, arrayIndex);

            List<UProperty> elements;
            try
            {
                elements = ReadStructElement(_reader);
            }
            catch (Exception ex)
            {
                throw BuildReaderStateException($"Failed reading struct elements for '{parentName}[{arrayIndex}]'.", ex);
            }

            try
            {
                var property = new UStructProperty(_reader, tag, elements, parentTag.arrayInfo.alternateName.ToString());
                reconstructedStructList.Add(property);
            }
            catch (Exception ex)
            {
                throw BuildReaderStateException($"Failed to build static struct element '{parentName}[{arrayIndex}]'.", ex);
            }

            arrayIndex++;
        }

        AddPropertyListToCollection(reconstructedStructList);
    }

    private UArrayProperty BuildArrayProperty<T>(TagContainer tag, Func<JsonTextReader, T> function) where T : notnull
    {
        var elements = new List<object>();

        while (ReadJsonDictionary())
        {
            try
            {
                elements.Add(function(_reader));
            }
            catch (Exception ex)
            {
                throw BuildReaderStateException($"Failed reading array element for '{tag.name}'.", ex);
            }
        }

        try
        {
            return new UArrayProperty(_reader, tag, elements);
        }
        catch (Exception ex)
        {
            throw BuildReaderStateException($"Failed to build ArrayProperty '{tag.name}'.", ex);
        }
    }

    private List<UProperty> ReadStructElement(JsonTextReader reader)
    {
        var elements = new List<UProperty>();

        while (ReadJsonList())
        {
            if (reader.TokenType is JsonToken.StartObject)
                continue;

            if (reader.TokenType is JsonToken.EndArray)
                break;

            ReadJsonProperty(out UProperty? property, addToCrunchCollection: false);

            if (property is not null)
                elements.Add(property);
        }

        return elements;
    }

    private void PopulateUPropertyMetadata(ref TagContainer tag, string type, int size, int arrayIndex)
    {
        tag.type = type;
        tag.arrayIndex = arrayIndex;
        tag.size = size;
    }

    private void ExpectRead(string context)
    {
        if (!_reader.Read())
            throw BuildReaderStateException($"Unexpected end of JSON while {context}.");
    }

    private InvalidOperationException BuildReaderStateException(string message, Exception? inner = null)
    {
        string state = $"(Token {_reader.TokenType}, Depth {_reader.Depth}, Path '{_reader.Path}', Line {_reader.LineNumber}, Pos {_reader.LinePosition})";
        return inner is null
            ? new InvalidOperationException($"{message} {state}")
            : new InvalidOperationException($"{message} {state}", inner);
    }
}