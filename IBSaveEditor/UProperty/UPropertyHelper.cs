using System.Runtime.CompilerServices;
using System.Text.Json;
using Newtonsoft.Json;
using IBSaveEditor.Wrappers;

namespace IBSaveEditor.UProperties;

/// <summary>
/// Shared helper methods used across all <see cref="UProperty"/> implementations.
/// Covers size calculation, JSON reading/writing, and binary serialization utilities.
/// Only reusable mechanics live here : no property-specific logic.
/// </summary>
public static class UPropertyHelper
{
    // Size calculation ──────────────────────────────────────────────────────

    /// <summary>
    /// Returns the byte length a string occupies in the Unreal binary format.
    /// <para>
    /// Empty strings serialize as a single 4-byte zero (the length field only).
    /// Non-empty strings serialize as: 4-byte length + UTF-8 bytes + 1-byte null terminator.
    /// </para>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetLittleEndianStringLength(string str)
    {
        if (str == string.Empty)
            return sizeof(int);

        return UPropertyLayout.VALUE_SIZE + str.Length + UPropertyLayout.NULL_TERMINATOR_SIZE;
    }

    /// <summary>
    /// Populates <see cref="UProperty.uPropertyElementSize"/> with the total byte size
    /// of the property including its full metadata header.
    /// <para>
    /// The metadata header layout is: name + type + value-size field + array-index field + content.
    /// Subclasses set <c>uPropertyElementSize</c> to the content-specific overhead (e.g. an enum
    /// name string) before calling this, which is why we use <c>??=</c> rather than overwriting.
    /// </para>
    /// </summary>
    public static void PopulatePropertyMetadataSize(UProperty property)
    {
        property.uPropertyElementSize ??= 0;
        property.uPropertyElementSize += GetLittleEndianStringLength(property.name);
        property.uPropertyElementSize += GetLittleEndianStringLength(property.type);
        property.uPropertyElementSize += UPropertyLayout.VALUE_SIZE;
        property.uPropertyElementSize += UPropertyLayout.ARRAY_INDEX_SIZE;
        property.uPropertyElementSize += property.size;
    }

    /// <summary>
    /// Calculates the total byte size of a list of <see cref="UProperty"/> objects
    /// by summing their pre-computed <see cref="UProperty.uPropertyElementSize"/> values,
    /// plus the trailing "None" terminator that every property list ends with.
    /// </summary>
    public static int CalculatePropertyContentSize(List<UProperty> properties)
        => CalculatePropertyListSize(properties);

    /// <summary>
    /// Calculates the total byte size of a dynamic array's element list.
    /// Dispatches to the appropriate calculation based on the element type.
    /// </summary>
    public static int CalculatePropertyContentSize(List<object> elements)
    {
        if (elements.Count == 0) return 0;

        return elements[0] switch
        {
            string          => CalculateStringArraySize(elements.OfType<string>()),
            int             => elements.Count * sizeof(int),
            float           => elements.Count * sizeof(float),
            bool            => elements.Count * sizeof(bool),
            List<UProperty> => CalculateNestedPropertyListsSize(elements.OfType<List<UProperty>>()),
            UProperty       => CalculatePropertyListSize(elements.OfType<UProperty>()),
            _               => throw new NotImplementedException(
                                   "Size calculation not implemented for this array element type.")
        };
    }

    // JSON reading ──────────────────────────────────────────────────────────

    /// <summary>Delegate matching the signature of the standard TryParse methods (e.g. <c>int.TryParse</c>).</summary>
    internal protected delegate bool TryParseDelegate<T>(string input, out T result);

    /// <summary>
    /// Reads the current <see cref="JsonTextReader"/> value as a string.
    /// Throws if the value is null : a null token at this point indicates a malformed JSON structure.
    /// </summary>
    public static string ReaderValueToString(JsonTextReader reader)
    {
        if (reader.Value is null)
            throw new InvalidCastException(
                $"Reader value is null. Expected a value token, got {reader.TokenType}.");

        return reader.Value.ToString() ?? string.Empty;
    }

    /// <summary>
    /// Reads and parses the current <see cref="JsonTextReader"/> value into a struct type
    /// using the provided TryParse delegate (e.g. <c>int.TryParse</c>, <c>float.TryParse</c>).
    /// Throws if the value is null or cannot be parsed into the target type.
    /// </summary>
    internal static T ParseReaderValue<T>(JsonTextReader reader, TryParseDelegate<T> tryParse) where T : struct
    {
        if (reader.Value is null)
            throw new InvalidCastException(
                $"Reader value is null for token {reader.TokenType}.");

        var str = reader.Value.ToString()!;

        if (!tryParse(str, out T result))
            throw new ArgumentException(
                $"Cannot convert '{str}' to {typeof(T).Name}.");

        return result;
    }

    /// <summary>
    /// Returns the first element of a list, or null if the list is empty.
    /// Used by array serialization to determine the element type before dispatching.
    /// </summary>
    public static object? GetFirstOrNull(List<object> elements)
        => elements.Count > 0 ? elements[0] : null;

    // JSON writing ──────────────────────────────────────────────────────────

    /// <summary>
    /// Writes every property in the list as a named JSON value using each property's own name as the key.
    /// </summary>
    public static void WritePropertyListJson(Utf8JsonWriter writer, IEnumerable<UProperty> properties)
    {
        foreach (var property in properties)
            property.WriteValueData(writer, property.name);
    }

    /// <summary>
    /// Writes a sequence of nested property lists as an array of JSON objects.
    /// Each list becomes one object in the array.
    /// </summary>
    public static void WriteNestedPropertyListsJson(
        Utf8JsonWriter writer, IEnumerable<List<UProperty>> nestedLists)
    {
        foreach (var list in nestedLists)
        {
            writer.WriteStartObject();
            WritePropertyListJson(writer, list);
            writer.WriteEndObject();
        }
    }

    // Binary serialization ──────────────────────────────────────────────────

    /// <summary>
    /// Serializes a list of properties into binary format : each property's metadata header
    /// followed by its value, terminated with a "None" string as required by the UE3 format.
    /// </summary>
    public static void SerializePropertyList(UnrealBinaryWriter writer, IEnumerable<UProperty> properties)
    {
        foreach (var property in properties)
        {
            writer.WritePropertyMetadata(property);
            property.SerializeValue(writer);
        }

        // Every UE3 property list is terminated with a "None" sentinel string.
        writer.WriteUnrealString(UType.NONE);
    }

    /// <summary>
    /// Serializes a sequence of nested property lists, each terminated with "None".
    /// Used for dynamic arrays of structs.
    /// </summary>
    public static void SerializeNestedPropertyLists(
        UnrealBinaryWriter writer, IEnumerable<List<UProperty>> nestedLists)
    {
        foreach (var list in nestedLists)
            SerializePropertyList(writer, list);
    }

    // Private size helpers ──────────────────────────────────────────────────

    /// <summary>
    /// Sums the binary lengths of a sequence of strings, using Unreal's
    /// length-prefixed null-terminated encoding.
    /// </summary>
    private static int CalculateStringArraySize(IEnumerable<string> elements)
    {
        int total = 0;
        foreach (var s in elements)
            total += GetLittleEndianStringLength(s);
        return total;
    }

    /// <summary>
    /// Sums the binary sizes of a sequence of nested property lists.
    /// </summary>
    private static int CalculateNestedPropertyListsSize(IEnumerable<List<UProperty>> nestedLists)
    {
        int total = 0;
        foreach (var list in nestedLists)
            total += CalculatePropertyListSize(list);
        return total;
    }

    /// <summary>
    /// Sums the pre-computed element sizes of a property list, plus the
    /// trailing "None" terminator that every property list ends with.
    /// </summary>
    private static int CalculatePropertyListSize(IEnumerable<UProperty> properties)
    {
        int total = 0;
        foreach (var property in properties)
            total += property.uPropertyElementSize ?? 0;

        total += GetLittleEndianStringLength(UType.NONE);
        return total;
    }
}