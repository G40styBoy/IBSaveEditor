using System.Text.Json;
using Newtonsoft.Json;
using IBSaveEditor.Wrappers;

namespace IBSaveEditor.UProperties;
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