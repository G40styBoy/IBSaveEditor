using Newtonsoft.Json.Linq;
using IBSaveEditor.Models;

namespace IBSaveEditor.Services;

public static class NodeTreeToJson
{
    public static JObject Convert(IEnumerable<SaveNode> nodes)
    {
        var obj = new JObject();
        foreach (var node in nodes)
            obj[node.Name] = ToToken(node);
        return obj;
    }

    private static JToken ToToken(SaveNode node) => node switch
    {
        PrimitiveNode p => p.Value == null ? JValue.CreateNull() : JToken.FromObject(p.Value),
        EnumNode e      => EnumToObject(e),
        StructNode s    => StructToObject(s),
        ArrayNode a     => ArrayToJArray(a),
        _               => JValue.CreateNull()
    };

    private static JObject EnumToObject(EnumNode e) => new()
    {
        ["Enum"]       = e.EnumType,
        ["Enum Value"] = e.EnumValue
    };

    private static JObject StructToObject(StructNode s)
    {
        var obj = new JObject();
        foreach (var child in s.Children)
            obj[child.Name] = ToToken(child);
        return obj;
    }

    private static JArray ArrayToJArray(ArrayNode a)
    {
        var arr = new JArray();
        foreach (var item in a.Items)
            arr.Add(ToToken(item));
        return arr;
    }
}
