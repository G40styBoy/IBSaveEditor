using Newtonsoft.Json.Linq;
using IBSaveEditor.Models;

namespace IBSaveEditor.Services;

public static class JsonToNodeTree
{
    private const string EnumTypeKey  = "Enum";
    private const string EnumValueKey = "Enum Value";

    public static List<SaveNode> Convert(JObject root)
    {
        // Case-insensitive search for "data" key
        var dataProp = root.Properties()
            .FirstOrDefault(p => p.Name.Equals("data", System.StringComparison.OrdinalIgnoreCase));

        if (dataProp?.Value is JObject dataObj)
        {
            var nodes = new List<SaveNode>();
            foreach (var prop in dataObj.Properties())
                nodes.Add(FromToken(prop.Name, prop.Value));
            return nodes;
        }

        // Fallback: show everything
        var fallback = new List<SaveNode>();
        foreach (var prop in root.Properties())
            fallback.Add(FromToken(prop.Name, prop.Value));
        return fallback;
    }

    public static SaveNode FromToken(string name, JToken token)
    {
        return token.Type switch
        {
            JTokenType.Object => FromObject(name, (JObject)token),
            JTokenType.Array  => FromArray(name, (JArray)token),
            _                 => FromPrimitive(name, token)
        };
    }

    private static SaveNode FromObject(string name, JObject obj)
    {
        var keys = obj.Properties().Select(p => p.Name).ToList();
        if (keys.Count == 2 && keys.Contains(EnumTypeKey) && keys.Contains(EnumValueKey))
        {
            return new EnumNode
            {
                Name      = name,
                TypeHint  = "enum",
                EnumType  = obj[EnumTypeKey]?.ToString() ?? string.Empty,
                EnumValue = obj[EnumValueKey]?.ToString() ?? string.Empty
            };
        }

        var node = new StructNode { Name = name, TypeHint = "struct" };
        foreach (var prop in obj.Properties())
            node.Children.Add(FromToken(prop.Name, prop.Value));
        return node;
    }

    private static ArrayNode FromArray(string name, JArray arr)
    {
        var node = new ArrayNode { Name = name, TypeHint = "array", IsFixed = false };
        int i = 0;
        foreach (var item in arr)
            node.Items.Add(FromToken($"[{i++}]", item));
        node.ItemTypeHint = node.Items.Count > 0 ? node.Items[0].TypeHint : "string";
        return node;
    }

    private static PrimitiveNode FromPrimitive(string name, JToken token)
    {
        var typeHint = token.Type switch
        {
            JTokenType.Integer => "int",
            JTokenType.Float   => "float",
            JTokenType.Boolean => "bool",
            JTokenType.Bytes   => "byte",
            _                  => "string"
        };
        return new PrimitiveNode { Name = name, TypeHint = typeHint, Value = token.ToObject<object>() };
    }
}
