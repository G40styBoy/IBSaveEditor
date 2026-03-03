// JsonUtils.cs
#nullable enable
using System;
using System.IO;
using System.Text.Json;

public static class JsonUtils
{
    public static EnvelopeMeta ReadMeta(string jsonPath)
    {
        try
        {
            using FileStream fs = File.OpenRead(jsonPath);
            using JsonDocument doc = JsonDocument.Parse(fs);

            JsonElement root = doc.RootElement;
            JsonElement metadata = ReadProperty(root, "metadata");

            Game game = ReadEnum<Game>(metadata, "game");
            bool isEncrypted = ReadBoolean(metadata, "isEncrypted");
            string packageName = ReadString(metadata, "packageName");
            uint saveVersion = ReadUInt32(metadata, "saveVersion");
            uint saveMagic = ReadUInt32(metadata, "saveMagic");

            return new EnvelopeMeta(game, isEncrypted, packageName, saveVersion, saveMagic);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException("Failed to read JSON metadata.", ex);
        }
    }

    public static JsonElement ReadProperty(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out JsonElement property))
            throw new InvalidOperationException($"Invalid JSON: missing '{propertyName}'.");

        return property;
    }

    public static string ExtractDataObjectJson(string jsonPath, string objectName)
    {
        try
        {
            using FileStream fs = File.OpenRead(jsonPath);
            using JsonDocument doc = JsonDocument.Parse(fs);

            JsonElement root = doc.RootElement;

            if (!root.TryGetProperty(objectName, out JsonElement dataEl))
                throw new InvalidOperationException($"Invalid JSON: missing '{objectName}' object.");

            return dataEl.GetRawText();
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException($"Failed to extract '{objectName}' object from JSON.", ex);
        }
    }


    public static string ReadString(JsonElement parent, string propertyName)
    {
        JsonElement el = ReadProperty(parent, propertyName);

        if (el.ValueKind != JsonValueKind.String)
            throw new InvalidOperationException($"Invalid JSON: '{propertyName}' must be a string.");

        string? value = el.GetString();
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Invalid JSON: '{propertyName}' is empty.");

        return value;
    }

    public static bool ReadBoolean(JsonElement parent, string propertyName)
    {
        JsonElement el = ReadProperty(parent, propertyName);

        if (el.ValueKind != JsonValueKind.True && el.ValueKind != JsonValueKind.False)
            throw new InvalidOperationException($"Invalid JSON: '{propertyName}' must be a boolean.");

        return el.GetBoolean();
    }

    public static uint ReadUInt32(JsonElement parent, string propertyName)
    {
        JsonElement el = ReadProperty(parent, propertyName);

        if (el.ValueKind != JsonValueKind.Number)
            throw new InvalidOperationException($"Invalid JSON: '{propertyName}' must be a number.");

        if (!el.TryGetUInt32(out uint value))
            throw new InvalidOperationException($"Invalid JSON: '{propertyName}' is out of range for UInt32.");

        return value;
    }

    public static TEnum ReadEnum<TEnum>(JsonElement parent, string propertyName) where TEnum : struct, Enum
    {
        string text = ReadString(parent, propertyName);

        if (!Enum.TryParse<TEnum>(text, ignoreCase: true, out var value))
            throw new InvalidOperationException($"Invalid JSON: '{propertyName}' has invalid value '{text}'.");

        return value;
    }


    /// <summary>
    /// Common guard for "string must exist" scenarios.
    /// Useful in JsonDataCruncher for Name/String array elements.
    /// </summary>
    public static string RequireString(string? value, string context)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Unexpected null/empty string while reading {context}.");
        return value;
    }

    /// <summary>
    /// Common guard for "file path must exist" scenarios.
    /// </summary>
    public static void RequireFileExists(string path, string? paramName = null)
    {
        if (path is null)
            throw new ArgumentNullException(paramName ?? nameof(path));
        if (!File.Exists(path))
            throw new FileNotFoundException("The specified file does not exist.", path);
    }
}