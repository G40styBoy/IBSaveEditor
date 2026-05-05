using Newtonsoft.Json;
using IBSaveEditor.UProperties;
using IBSaveEditor.UProperties.UArray;
using IBSaveEditor.Package;
using IBSaveEditor.Enums;

namespace IBSaveEditor.Json;

/// <summary>
/// Reads a JSON string containing deserialized save data and converts it into a list
/// of <see cref="UProperty"/> objects ready for binary serialization.
/// <para>
/// This is the inverse of <see cref="JsonDataParser"/>. Where the parser converts
/// binary → JSON, the cruncher converts JSON → binary-ready UProperty list.
/// </para>
/// <para>
/// Property types are inferred from the JSON token type and the property name's prefix:
/// <list type="bullet">
/// <item><c>b</c> prefix + integer → ByteProperty</item>
/// <item><c>e</c> prefix + object  → EnumByteProperty</item>
/// <item><c>ini_</c> prefix + string → NameProperty (FName)</item>
/// <item>object with "Enum"/"Enum Value" keys → EnumByteProperty</item>
/// <item>JSON array → ArrayProperty (looked up in <see cref="UArrayRegistry"/>)</item>
/// </list>
/// </para>
/// </summary>
internal sealed class JsonDataCruncher
{
    // Prefix/depth constants ────────────────────────────────────────────────

    private const string ENUM_PREFIX  = "e";
    private const string FNAME_PREFIX = "ini_";
    private const string BYTE_PREFIX  = "b";

    /// <summary>
    /// Properties at depth 1 are top-level save properties. Those deeper than this
    /// are children of structs or arrays and need their full metadata size tracked
    /// for correct binary serialization.
    /// </summary>
    private const int NORMAL_READER_DEPTH = 1;

    //  Edge-case name sets ───────────────────────────────────────────────────

    /// <summary>
    /// Names that start with "e" but are NOT enum properties.
    /// These are treated as their actual type (e.g. int) instead.
    /// </summary>
    private readonly HashSet<string> SpecialEnumNames = new() { "eCurrentPlayerType" };

    /// <summary>
    /// Names that start with "b" but are NOT byte properties.
    /// These are treated as int properties instead.
    /// </summary>
    private readonly HashSet<string> SpecialIntNames = new() { "bWasEncrypted" };

    /// <summary>
    /// Array names that use enum-keyed entries rather than sequential indices.
    /// These require a different reconstruction path.
    /// </summary>
    private readonly HashSet<string> SpecialStructNames = new() { "SavedCheevo" };

    //  State ─────────────────────────────────────────────────────────────────

    private readonly JsonTextReader  _reader;
    private readonly List<UProperty> _crunchedList = new();
    private readonly Game            _game;

    /// <param name="jsonText">The raw JSON string containing only the "data" object contents.</param>
    /// <param name="game">The game this save belongs to, used for array registry lookups.</param>
    public JsonDataCruncher(string jsonText, Game game)
    {
        ArgumentException.ThrowIfNullOrEmpty(jsonText);

        _game = game;
        IBEnum.SetGame(game);

        // DateParseHandling.None prevents Newtonsoft from silently converting
        // date-like strings into DateTime objects, which would corrupt name values.
        _reader = new JsonTextReader(new StringReader(jsonText))
        {
            CloseInput         = true,
            DateParseHandling  = DateParseHandling.None
        };
    }

    //  Public entry point ────────────────────────────────────────────────────

    /// <summary>
    /// Reads all properties from the JSON text and returns the completed list.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown with reader state context if any property fails to parse.</exception>
    internal List<UProperty> ReadJsonFile()
    {
        try
        {
            while (_reader.Read())
            {
                // Skip the root object open/close tokens and depth-1 object boundaries :
                // we only care about the property name/value tokens inside.
                if (_reader.Depth <= NORMAL_READER_DEPTH &&
                    _reader.TokenType is JsonToken.StartObject or JsonToken.EndObject)
                    continue;

                ReadJsonProperty(out _);
            }

            return _crunchedList;
        }
        catch (JsonReaderException jre)
        {
            throw new InvalidOperationException(
                $"Failed to read JSON (Line {jre.LineNumber}, Pos {jre.LinePosition}, Path '{jre.Path}').", jre);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException("Failed to read JSON.", ex);
        }
    }

    //  Property classification ───────────────────────────────────────────────

    /// <summary>
    /// Returns true when a property name should be treated as a ByteProperty.
    /// Properties starting with "b" are byte-typed unless they are in the
    /// <see cref="SpecialIntNames"/> exception set.
    /// </summary>
    private bool ShouldTreatAsByteProperty(string name) =>
        name.StartsWith(BYTE_PREFIX, StringComparison.Ordinal) && !SpecialIntNames.Contains(name);

    /// <summary>
    /// Returns true when a property name should be treated as an EnumByteProperty.
    /// Properties starting with "e" are enum-typed unless they are in the
    /// <see cref="SpecialEnumNames"/> exception set.
    /// </summary>
    private bool ShouldTreatAsEnumProperty(string name) =>
        name.StartsWith(ENUM_PREFIX, StringComparison.Ordinal) && !SpecialEnumNames.Contains(name);

    /// <summary>Returns true when this array uses enum-keyed entries (e.g. SavedCheevo).</summary>
    private bool IsSpecialStruct(string name) => SpecialStructNames.Contains(name);

    /// <summary>
    /// Returns true when the reader is inside a struct or array (depth > 1).
    /// Nested properties need their full metadata size tracked for binary serialization.
    /// </summary>
    private bool IsNestedProperty() => _reader.Depth > NORMAL_READER_DEPTH;

    //  Reader navigation helpers ─────────────────────────────────────────────

    /// <summary>
    /// Advances the reader and returns true while there are more entries in a JSON object.
    /// Used when reading named property lists (structs, static arrays).
    /// </summary>
    private bool ReadJsonList() =>
        _reader.Read() && _reader.TokenType is not JsonToken.EndObject;

    /// <summary>
    /// Advances the reader and returns true while there are more entries in a JSON array.
    /// Used when reading dynamic array element lists.
    /// </summary>
    private bool ReadJsonDictionary() =>
        _reader.Read() && _reader.TokenType is not JsonToken.EndArray;

    /// <summary>
    /// Advances the reader exactly once. Throws if the end of JSON is reached unexpectedly.
    /// </summary>
    /// <param name="context">A description of what was being read, used in the error message.</param>
    private void ExpectRead(string context)
    {
        if (!_reader.Read())
            throw BuildReaderStateException($"Unexpected end of JSON while {context}.");
    }

    //  Collection helpers ────────────────────────────────────────────────────

    /// <summary>Adds a single property to the top-level crunched list.</summary>
    private void AddPropertyToCollection(UProperty property) =>
        _crunchedList.Add(property);

    /// <summary>Adds all properties from a list to the top-level crunched list.</summary>
    private void AddPropertyListToCollection(List<UProperty> propertyList) =>
        _crunchedList.AddRange(propertyList);

    //  Core property reading ─────────────────────────────────────────────────

    /// <summary>
    /// Reads the current property name token and dispatches to the appropriate type
    /// constructor based on the next value token.
    /// </summary>
    /// <param name="property">The constructed property, or null if the token was EndArray.</param>
    /// <param name="addToCrunchCollection">
    /// When true, the property is added to the main crunched list.
    /// False when reading nested struct/array elements that belong to a parent's list instead.
    /// </param>
    private void ReadJsonProperty(out UProperty? property, bool addToCrunchCollection = true)
    {
        ReadPropertyName(out TagContainer tag);

        // Properties deeper than the root level need their full binary metadata size tracked.
        if (IsNestedProperty())
            tag.bShouldTrackMetadataSize = true;

        property = ConstructUProperty(tag);

        if (addToCrunchCollection && property is not null)
            AddPropertyToCollection(property);
    }

    /// <summary>
    /// Reads the property name token from the reader and stores it in a new <see cref="TagContainer"/>.
    /// Throws if the name is null or empty : that would indicate a malformed JSON structure.
    /// </summary>
    private void ReadPropertyName(out TagContainer tag)
    {
        tag      = new TagContainer();
        tag.name = UPropertyHelper.ReaderValueToString(_reader);

        if (string.IsNullOrEmpty(tag.name))
            throw BuildReaderStateException("Property name is null or empty.");
    }

    /// <summary>
    /// Advances to the next value token and dispatches to the correct type builder
    /// based on the token type.
    /// </summary>
    private UProperty? ConstructUProperty(TagContainer tag)
    {
        ExpectRead("reading property value token");

        return _reader.TokenType switch
        {
            JsonToken.Integer     => BuildIntegerProperty(tag),
            JsonToken.Float       => BuildFloatProperty(tag),
            JsonToken.Boolean     => BuildBooleanProperty(tag),
            JsonToken.String      => BuildStringProperty(tag),
            JsonToken.StartObject => BuildObjectProperty(tag),
            JsonToken.StartArray  => BuildArrayProperty(tag),
            JsonToken.EndArray    => null, // signals end of a parent array
            _ => throw BuildReaderStateException($"Unsupported JSON token '{_reader.TokenType}'.")
        };
    }

    //  Type builders ─────────────────────────────────────────────────────────

    /// <summary>
    /// Builds an integer-valued property. If the name is "b"-prefixed it becomes
    /// a ByteProperty; otherwise it becomes an IntProperty.
    /// </summary>
    private UProperty BuildIntegerProperty(TagContainer tag)
    {
        if (ShouldTreatAsByteProperty(tag.name))
        {
            // Strip the "b" prefix : it exists only in the JSON to distinguish
            // byte properties from int properties. The binary format doesn't use it.
            RemovePrefix(ref tag.name, BYTE_PREFIX);
            PopulateTagMetadata(ref tag, UType.BYTE_PROPERTY, sizeof(byte), 0);

            try { return UByteProperty.InstantiateProperty(_reader, tag); }
            catch (Exception ex)
            { throw BuildReaderStateException($"Failed to build ByteProperty '{tag.name}'.", ex); }
        }

        PopulateTagMetadata(ref tag, UType.INT_PROPERTY, sizeof(int), 0);

        try { return new UIntProperty(_reader, tag); }
        catch (Exception ex)
        { throw BuildReaderStateException($"Failed to build IntProperty '{tag.name}'.", ex); }
    }

    private UProperty BuildFloatProperty(TagContainer tag)
    {
        PopulateTagMetadata(ref tag, UType.FLOAT_PROPERTY, sizeof(float), 0);

        try { return new UFloatProperty(_reader, tag); }
        catch (Exception ex)
        { throw BuildReaderStateException($"Failed to build FloatProperty '{tag.name}'.", ex); }
    }

    private UProperty BuildBooleanProperty(TagContainer tag)
    {
        PopulateTagMetadata(ref tag, UType.BOOL_PROPERTY, UPropertyLayout.BYTE_SIZE_SPECIAL, 0);

        try { return new UBoolProperty(_reader, tag); }
        catch (Exception ex)
        { throw BuildReaderStateException($"Failed to build BoolProperty '{tag.name}'.", ex); }
    }

    /// <summary>
    /// Builds a string-valued property. If the name has the "ini_" prefix it is an
    /// FName (NameProperty); otherwise it is a plain StrProperty.
    /// </summary>
    private UProperty BuildStringProperty(TagContainer tag)
    {
        if (tag.name.StartsWith(FNAME_PREFIX, StringComparison.Ordinal))
        {
            // Strip the ini_ prefix : it was written by UNameProperty.WriteValueData
            // to mark FNames and must be removed before writing back to binary.
            RemovePrefix(ref tag.name, FNAME_PREFIX);
            PopulateTagMetadata(ref tag, UType.NAME_PROPERTY, 0, 0);
        }
        else
        {
            PopulateTagMetadata(ref tag, UType.STR_PROPERTY, 0, 0);
        }

        try { return new UStringProperty(_reader, tag); }
        catch (Exception ex)
        { throw BuildReaderStateException($"Failed to build String/Name property '{tag.name}'.", ex); }
    }

    /// <summary>
    /// Builds a JSON-object-valued property. If the name is "e"-prefixed it is an
    /// EnumByteProperty; otherwise it is a StructProperty.
    /// </summary>
    private UProperty BuildObjectProperty(TagContainer tag)
    {
        if (ShouldTreatAsEnumProperty(tag.name))
        {
            // Strip the "e" prefix : same reasoning as the "b" prefix strip above.
            RemovePrefix(ref tag.name, ENUM_PREFIX, SpecialEnumNames);
            PopulateTagMetadata(ref tag, UType.BYTE_PROPERTY, 0, 0);

            try { return UByteProperty.InstantiateProperty(_reader, tag); }
            catch (Exception ex)
            { throw BuildReaderStateException($"Failed to build Enum(Byte) property '{tag.name}'.", ex); }
        }

        List<UProperty> elements;
        try { elements = ReadStructElement(_reader); }
        catch (Exception ex)
        { throw BuildReaderStateException($"Failed to read struct elements for '{tag.name}'.", ex); }

        PopulateTagMetadata(ref tag, UType.STRUCT_PROPERTY, 0, 0);

        try { return new UStructProperty(_reader, tag, elements, string.Empty); }
        catch (Exception ex)
        { throw BuildReaderStateException($"Failed to build StructProperty '{tag.name}'.", ex); }
    }

    /// <summary>
    /// Builds an array property. The array name must exist in the <see cref="UArrayRegistry"/>
    /// for the current game. Static and dynamic arrays follow completely different reading paths.
    /// </summary>
    private UProperty? BuildArrayProperty(TagContainer tag)
    {
        if (!UArrayRegistry.TryGet(_game, tag.name, out var metadata) || metadata is null)
            throw BuildReaderStateException($"Missing array registry metadata for '{tag.name}'.");

        tag.arrayInfo = metadata;

        if (tag.arrayInfo.arrayType is ArrayType.Dynamic)
            return BuildDynamicArrayProperty(tag);

        BuildStaticArrayProperty(tag);

        // Static arrays are added to the collection directly inside the builder :
        // returning null tells the caller not to add anything further.
        return null;
    }

    // Dynamic array builders ────────────────────────────────────────────────

    /// <summary>
    /// Builds a dynamic array property by dispatching to the correct element reader
    /// based on the registered value type.
    /// </summary>
    private UArrayProperty BuildDynamicArrayProperty(TagContainer tag)
    {
        PopulateTagMetadata(ref tag, UType.ARRAY_PROPERTY, sizeof(int), 0);

        try
        {
            return tag.arrayInfo.valueType switch
            {
                PropertyType.IntProperty =>
                    BuildDynamicArrayElements(tag, r => (object)UPropertyHelper.ParseReaderValue<int>(r, int.TryParse)),

                PropertyType.FloatProperty =>
                    BuildDynamicArrayElements(tag, r => (object)UPropertyHelper.ParseReaderValue<float>(r, float.TryParse)),

                PropertyType.StrProperty or PropertyType.NameProperty =>
                    BuildDynamicArrayElements(tag, r => (object)JsonUtils.RequireString(r.Value?.ToString(), tag.name)),

                PropertyType.StructProperty =>
                    BuildDynamicArrayElements(tag, r => ReadStructElement(r)),

                _ => throw BuildReaderStateException($"Unsupported dynamic array value type: {tag.arrayInfo.valueType}")
            };
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw BuildReaderStateException($"Failed to build dynamic array '{tag.name}'.", ex);
        }
    }

    /// <summary>
    /// Reads elements from a JSON array and collects them into a <see cref="UArrayProperty"/>.
    /// Each element is read using the provided function delegate.
    /// </summary>
    private UArrayProperty BuildDynamicArrayElements<T>(TagContainer tag, Func<JsonTextReader, T> readElement)
        where T : notnull
    {
        var elements = new List<object>();

        while (ReadJsonDictionary())
        {
            try { elements.Add(readElement(_reader)); }
            catch (Exception ex)
            { throw BuildReaderStateException($"Failed reading dynamic array element for '{tag.name}'.", ex); }
        }

        try { return new UArrayProperty(_reader, tag, elements); }
        catch (Exception ex)
        { throw BuildReaderStateException($"Failed to build ArrayProperty '{tag.name}'.", ex); }
    }

    // Static array builders ─────────────────────────────────────────────────

    /// <summary>
    /// Routes static array reconstruction to the correct path based on the array's value type.
    /// Static arrays are written back to the crunched list directly rather than being returned.
    /// </summary>
    private void BuildStaticArrayProperty(TagContainer tag)
    {
        switch (tag.arrayInfo.valueType)
        {
            case PropertyType.IntProperty:
                ReconstructStaticNumericArray(tag, UType.INT_PROPERTY, sizeof(int),
                    t => new UIntProperty(_reader, t));
                break;

            case PropertyType.ByteProperty:
                ReconstructStaticNumericArray(tag, UType.BYTE_PROPERTY, sizeof(byte),
                    t => UByteProperty.InstantiateProperty(_reader, t),
                    true);
                break;

            case PropertyType.NameProperty:
                ReconstructFNameArray(tag);
                break;

            case PropertyType.StructProperty:
                ReconstructStaticStructArray(tag);
                break;

            default:
                throw BuildReaderStateException(
                    $"Unsupported static array value type: {tag.arrayInfo.valueType}");
        }
    }

    /// <summary>
    /// Reconstructs a static numeric array (int or byte) from a keyed JSON object.
    /// Each key in the object maps to an array index via <see cref="IBEnum"/> reflection.
    /// The "b" prefix is stripped from byte array keys before the index lookup.
    /// </summary>
    private void ReconstructStaticNumericArray(
        TagContainer                 parentTag,
        string                       uType,
        int                          elementSize,
        Func<TagContainer, UProperty> buildElement,
        bool transformName = false)
    {
        var    list       = new List<UProperty>();
        string parentName = parentTag.arrayInfo.arrayName.ToString();

        Type enumType;
        try { enumType = IBEnum.GetArrayIndexEnum(parentName); }
        catch (Exception ex)
        { throw BuildReaderStateException($"Failed to resolve array index enum for '{parentName}'.", ex); }

        // Advance past the opening brace of the static array object.
        ExpectRead("entering static array object");

        while (ReadJsonList())
        {
            ReadPropertyName(out TagContainer tag);

            // Apply name transformation if provided (e.g. strip "b" from byte array keys).
            if (transformName)
            {
                RemovePrefix(ref tag.name, BYTE_PREFIX);
                if (string.IsNullOrEmpty(tag.name))
                    throw BuildReaderStateException($"Static array '{parentName}' has null/empty index key.");
            }

            int arrayIndex;
            try { arrayIndex = IBEnum.GetArrayIndexUsingReflection(enumType, tag.name); }
            catch (Exception ex)
            { throw BuildReaderStateException($"Failed to map array index '{tag.name}' for '{parentName}'.", ex); }

            PopulateTagMetadata(ref tag, uType, elementSize, arrayIndex);
            tag.name = parentName;
            ExpectRead("reading static array value");

            try { list.Add(buildElement(tag)); }
            catch (Exception ex)
            { throw BuildReaderStateException($"Failed to build static element '{parentName}[{arrayIndex}]'.", ex); }
        }

        AddPropertyListToCollection(list);

        // Read past the closing brace of the wrapping array token.
        _reader.Read();
    }

    /// <summary>
    /// Reconstructs a static FName (NameProperty) array from a JSON array of strings.
    /// Each element becomes a NameProperty with a sequential array index.
    /// </summary>
    private void ReconstructFNameArray(TagContainer parentTag)
    {
        var    list       = new List<UProperty>();
        string parentName = parentTag.arrayInfo.arrayName.ToString();
        int    arrayIndex = 0;

        while (ReadJsonDictionary())
        {
            var tag  = new TagContainer();
            tag.name = parentName;
            PopulateTagMetadata(ref tag, UType.NAME_PROPERTY, 0, arrayIndex);

            try { list.Add(new UStringProperty(_reader, tag)); }
            catch (Exception ex)
            { throw BuildReaderStateException($"Failed to build static fname element '{parentName}[{arrayIndex}]'.", ex); }

            arrayIndex++;
        }

        AddPropertyListToCollection(list);
    }

    /// <summary>
    /// Reconstructs a static struct array from a JSON array of objects.
    /// Special structs like SavedCheevo use enum-keyed entries rather than sequential indices :
    /// those require an extra index-lookup step via <see cref="IBEnum"/> reflection.
    /// </summary>
    private void ReconstructStaticStructArray(TagContainer parentTag)
    {
        var    list               = new List<UProperty>();
        string parentName         = parentTag.arrayInfo.arrayName.ToString();
        int    arrayIndex         = 0;
        bool   needsIndexLookup   = IsSpecialStruct(parentTag.name);
        Type?  enumType           = null;

        if (needsIndexLookup)
        {
            // Advance past the outer object that wraps the enum-keyed entries.
            ExpectRead("entering static struct index object");

            try { enumType = IBEnum.GetArrayIndexEnum(parentName); }
            catch (Exception ex)
            { throw BuildReaderStateException($"Failed to resolve array index enum for '{parentName}'.", ex); }
        }

        while (ReadJsonDictionary())
        {
            var tag  = new TagContainer();
            tag.name = parentName;

            if (needsIndexLookup)
            {
                // The enum-keyed outer object ends with EndObject : break cleanly.
                if (_reader.TokenType is JsonToken.EndObject)
                {
                    _reader.Read();
                    break;
                }

                var rawKey = UPropertyHelper.ReaderValueToString(_reader);
                if (string.IsNullOrEmpty(rawKey))
                    throw BuildReaderStateException($"Static struct array '{parentName}' has null/empty index key.");

                try { arrayIndex = IBEnum.GetArrayIndexUsingReflection(enumType!, rawKey); }
                catch (Exception ex)
                { throw BuildReaderStateException($"Failed to map struct array index '{rawKey}' for '{parentName}'.", ex); }
            }

            PopulateTagMetadata(ref tag, UType.STRUCT_PROPERTY, 0, arrayIndex);

            List<UProperty> elements;
            try { elements = ReadStructElement(_reader); }
            catch (Exception ex)
            { throw BuildReaderStateException($"Failed reading struct elements for '{parentName}[{arrayIndex}]'.", ex); }

            try { list.Add(new UStructProperty(_reader, tag, elements, parentTag.arrayInfo.alternateName.ToString())); }
            catch (Exception ex)
            { throw BuildReaderStateException($"Failed to build static struct element '{parentName}[{arrayIndex}]'.", ex); }

            arrayIndex++;
        }

        AddPropertyListToCollection(list);
    }

    //  Struct element reader ─────────────────────────────────────────────────

    /// <summary>
    /// Reads a self-terminating list of named properties from a JSON object and
    /// returns them as a flat list. Used for struct bodies and struct array elements.
    /// Properties are NOT added to the main crunched list : they belong to the struct.
    /// </summary>
    private List<UProperty> ReadStructElement(JsonTextReader reader)
    {
        var elements = new List<UProperty>();

        while (ReadJsonList())
        {
            // Skip inner object open tokens : we only care about property name tokens.
            if (reader.TokenType is JsonToken.StartObject) continue;
            if (reader.TokenType is JsonToken.EndArray)    break;

            ReadJsonProperty(out var property, addToCrunchCollection: false);
            if (property is not null)
                elements.Add(property);
        }

        return elements;
    }

    //  Shared helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Fills the type, size, and arrayIndex fields of a tag container.
    /// </summary>
    private static void PopulateTagMetadata(ref TagContainer tag, string type, int size, int arrayIndex)
    {
        tag.type       = type;
        tag.size       = size;
        tag.arrayIndex = arrayIndex;
    }

    /// <summary>
    /// Removes a prefix from a property name in place.
    /// Optionally skips removal if the full name is in an exception set.
    /// </summary>
    private static void RemovePrefix(ref string name, string prefix, HashSet<string>? exceptions = null)
    {
        if (exceptions?.Contains(name) == true) return;
        if (name.StartsWith(prefix, StringComparison.Ordinal))
            name = name[prefix.Length..];
    }

    /// <summary>
    /// Builds an <see cref="InvalidOperationException"/> that includes the current
    /// reader state (token type, depth, path, line, position) for easier debugging.
    /// </summary>
    private InvalidOperationException BuildReaderStateException(string message, Exception? inner = null)
    {
        var state = $"(Token {_reader.TokenType}, Depth {_reader.Depth}, " +
                    $"Path '{_reader.Path}', Line {_reader.LineNumber}, Pos {_reader.LinePosition})";

        return inner is null
            ? new InvalidOperationException($"{message} {state}")
            : new InvalidOperationException($"{message} {state}", inner);
    }
}
