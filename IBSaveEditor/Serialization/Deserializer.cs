using IBSaveEditor.UProperties;
using IBSaveEditor.UProperties.UArray;
using IBSaveEditor.Package;
using IBSaveEditor.Wrappers;
using IBSaveEditor.Util;

namespace IBSaveEditor.Serialize;
/// <summary>
/// Reads raw unencrypted binary data from an <see cref="UnrealPackage"/> stream and
/// converts it into a list of strongly-typed <see cref="UProperty"/> objects.
/// <para>
/// The deserializer reads properties sequentially from the current stream position.
/// Each property starts with a name string : a "None" sentinel signals end of the list.
/// Static arrays are detected early via the <see cref="UArrayRegistry"/> and handled
/// differently from dynamic arrays: their entries are read by name-matching rather
/// than by a stored count.
/// </para>
/// </summary>
internal sealed class Deserializer
{
    // Guards against malformed static arrays that would otherwise loop forever.
    private const int MAX_STATIC_ARRAY_ELEMENTS = 2000;

    // Sentinel used when size/arrayIndex fields are not applicable (e.g. static array tags).
    private const int UNINITIALIZED_VALUE = -1;

    private readonly List<UProperty>    _propertyCollection = new();
    private readonly UnrealPackage      _upk;
    private readonly UnrealStream       _stream;
    private readonly UnrealBinaryReader _reader;

    public Deserializer(UnrealPackage upk)
    {
        _upk    = upk;
        _stream = upk.Stream;
        _reader = upk.Reader;
    }

    // Public entry point ────────────────────────────────────────────────────

    /// <summary>
    /// Reads all properties from the stream sequentially until a "None" sentinel
    /// or end-of-file is reached.
    /// </summary>
    /// <returns>The complete list of deserialized properties.</returns>
    /// <exception cref="InvalidDataException">
    /// Thrown when a property cannot be read at a given stream position.
    /// In <c>DEBUG</c> builds, the stream is also dumped to OUTPUT before throwing.
    /// </exception>
    public List<UProperty> DeserializePackage()
    {
        while (!_stream.IsEndOfFile)
        {
            long startPos = _stream.Position;
            try
            {
                var property = ConstructTag();
                if (property is null) break;
                _propertyCollection.Add(property);
            }
            catch (Exception ex)
            {
#if DEBUG
                DebugDumper.DumpStream(_stream, startPos);
#endif
                throw new InvalidDataException(
                    $"Failed to deserialize package at stream position {startPos}.\n{ex.Message}");
            }
        }
        return _propertyCollection;
    }

    // Tag construction ──────────────────────────────────────────────────────

    /// <summary>
    /// Reads the next property tag from the stream and constructs the appropriate
    /// <see cref="UProperty"/> subtype from it.
    /// <para>
    /// The first value read is always the property name. If "None" is encountered
    /// the method returns null to signal end of the property list.
    /// </para>
    /// <para>
    /// Static array detection is enabled by default. When the name is found in the
    /// <see cref="UArrayRegistry"/> as a static array, the stream position is reverted
    /// so the name can be re-read as part of the array collection loop.
    /// </para>
    /// </summary>
    /// <param name="allowStaticArrayDetection">
    /// False when this call is already inside a static array loop : prevents
    /// re-detecting the same array name as another static array.
    /// </param>
    private UProperty? ConstructTag(bool allowStaticArrayDetection = true)
    {
        try
        {
            var tag  = new TagContainer();
            tag.name = _reader.DeserializeString();

            // "None" signals end of this property list.
            if (tag.name is UType.NONE)
                return null;

            // Look up array metadata. Null is valid here : most properties aren't arrays.
            UArrayRegistry.TryGet(_upk.info.game, tag.name, out var metadata);
            tag.arrayInfo = metadata!;

            if (allowStaticArrayDetection && tag.arrayInfo?.arrayType is ArrayType.Static)
            {
                // Revert the stream past the name we just read so the static array
                // builder can collect entries by matching the name on each iteration.
                _stream.RevertStringPosition(tag.name);
                PopulateTagMetadata(ref tag, UType.ARRAY_PROPERTY, UNINITIALIZED_VALUE, UNINITIALIZED_VALUE);
                tag.arrayEntryCount = 0;
            }
            else
            {
                // Normal property : read type, size, and array index from the stream.
                PopulateTagMetadata(ref tag,
                    _reader.DeserializeString(),
                    _reader.DeserializeInt(),
                    _reader.DeserializeInt());

                // Dynamic arrays also store their entry count in the header.
                if (tag.type is UType.ARRAY_PROPERTY)
                    tag.arrayEntryCount = _reader.DeserializeInt();
            }

            return ConstructUProperty(tag);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to construct tag.", ex);
        }
    }

    /// <summary>Fills the type, size, and arrayIndex fields of a tag container.</summary>
    private static void PopulateTagMetadata(ref TagContainer tag, string type, int size, int arrayIndex)
    {
        tag.type       = type;
        tag.size       = size;
        tag.arrayIndex = arrayIndex;
    }

    //  Property construction ─────────────────────────────────────────────────

    /// <summary>
    /// Dispatches to the correct <see cref="UProperty"/> subtype constructor based on the tag type.
    /// </summary>
    private UProperty ConstructUProperty(TagContainer tag) => tag.type switch
    {
        UType.INT_PROPERTY    => new UIntProperty(_reader, tag),
        UType.FLOAT_PROPERTY  => new UFloatProperty(_reader, tag),
        UType.BOOL_PROPERTY   => new UBoolProperty(_reader, tag),
        UType.BYTE_PROPERTY   => UByteProperty.InstantiateProperty(_reader, tag),
        UType.STR_PROPERTY    => new UStringProperty(_reader, tag),
        UType.NAME_PROPERTY   => new UNameProperty(_reader, tag),
        UType.STRUCT_PROPERTY => CreateStructProperty(tag),
        UType.ARRAY_PROPERTY  => CreateArrayProperty(tag),
        _                     => throw new NotSupportedException($"Unsupported property type: {tag.type}")
    };

    /// <summary>
    /// Reads a struct property. The alternate struct name (used during serialization
    /// to write the correct UnrealScript type name) is read here but not used in
    /// the deserialized output: it is stored on the tag.
    /// </summary>
    private UStructProperty CreateStructProperty(TagContainer tag)
    {
        tag.alternateName = _reader.DeserializeString();
        var elements      = ReadPropertyList();
        return new UStructProperty(tag, tag.alternateName, elements);
    }

    /// <summary>
    /// Routes array construction to the appropriate builder based on whether the
    /// array is static or dynamic, and on the element type for dynamic arrays.
    /// </summary>
    private UProperty CreateArrayProperty(TagContainer tag)
    {
        if (tag.arrayInfo.arrayType is ArrayType.Static)
            return BuildStaticArrayProperty(tag);

        return tag.arrayInfo.valueType switch
        {
            PropertyType.IntProperty                                  => BuildDynamicArrayProperty(tag, _ => _reader.DeserializeInt()),
            PropertyType.FloatProperty                                => BuildDynamicArrayProperty(tag, _ => _reader.DeserializeFloat()),
            PropertyType.StrProperty or PropertyType.NameProperty    => BuildDynamicArrayProperty(tag, _ => _reader.DeserializeString()),
            PropertyType.StructProperty                               => BuildDynamicArrayProperty(tag, _ => ReadPropertyList()),
            _ => throw new NotSupportedException($"Unsupported array value type: {tag.arrayInfo.valueType}")
        };
    }

    // Array builders ────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a dynamic array by reading exactly <see cref="TagContainer.arrayEntryCount"/>
    /// elements, each read via the provided reader delegate.
    /// </summary>
    private UArrayProperty BuildDynamicArrayProperty<T>(TagContainer tag, Func<UnrealBinaryReader, T> readElement) where T : notnull
    {
        var elements = new List<object>(tag.arrayEntryCount);
        for (int i = 0; i < tag.arrayEntryCount; i++)
            elements.Add(readElement(_reader));

        return new UArrayProperty(tag, elements);
    }

    /// <summary>
    /// Builds a static array by collecting entries as long as the next property name
    /// matches the array name. Static arrays don't store a count : entries are
    /// identified by repeating the same property name in the stream.
    /// <para>
    /// The loop guard at <see cref="MAX_STATIC_ARRAY_ELEMENTS"/> prevents an infinite
    /// loop if the stream is malformed.
    /// </para>
    /// </summary>
    private UArrayProperty BuildStaticArrayProperty(TagContainer tag)
    {
        var elements  = new List<object>();
        int loopCount = 0;

        while (true)
        {
            if (loopCount > MAX_STATIC_ARRAY_ELEMENTS)
                throw new InvalidDataException(
                    $"Static array '{tag.name}' exceeded {MAX_STATIC_ARRAY_ELEMENTS} entries : possible infinite loop.");

            // Peek ahead: if the next name doesn't match, we've read all entries.
            if (_reader.PeekString() != tag.name)
                break;

            var property = ConstructTag(allowStaticArrayDetection: false);
            if (property != null)
                elements.Add(property);

            tag.arrayEntryCount++;
            loopCount++;
        }

        return new UArrayProperty(tag, elements);
    }

    // Struct helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Reads a self-terminating list of properties from the stream until a "None"
    /// sentinel is encountered. Used for struct bodies and struct array elements.
    /// </summary>
    private List<UProperty> ReadPropertyList()
    {
        var elements = new List<UProperty>();
        UProperty? property;

        while ((property = ConstructTag()) != null)
            elements.Add(property);

        return elements;
    }
}