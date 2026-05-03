using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using IBSaveEditor.Models;
using IBSaveEditor.Package;
using IBSaveEditor.UProperties.UArray;

namespace IBSaveEditor.Services;

/// <summary>
/// Converts a parsed JSON envelope into a tree of <see cref="SaveNode"/>
/// objects ready for display in the editor.
/// <para>
/// The conversion is game-aware — arrays are resolved against the
/// <see cref="UArrayRegistry"/> via <see cref="ArrayTypeResolver"/> so that
/// each array's <see cref="ArrayNode.ItemTypeHint"/> reflects the actual item
/// type (int, name, struct, etc.) and its registry metadata is attached for
/// the view layer to use when deciding whether to unwrap items for display.
/// </para>
/// <para>
/// Backing data is NEVER mutated by display concerns. Wrapper structs around
/// primitive items in static arrays remain in the node tree so round-tripping
/// to JSON for serialization stays correct. The view-side <c>NodeViewModel</c>
/// is responsible for looking through wrappers when rendering.
/// </para>
/// </summary>
public static class JsonToNodeTree
{
    private const string EnumTypeKey  = "Enum";
    private const string EnumValueKey = "Enum Value";

    /// <summary>
    /// Converts a root JObject to a node list, automatically unwrapping the
    /// top-level "data" envelope so metadata siblings are never shown.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when an array property is encountered that isn't registered for the given game.
    /// </exception>
    public static List<SaveNode> Convert(JObject root, Game game)
    {
        // Locate the "data" envelope key (case-insensitive)
        var dataProp = root.Properties()
            .FirstOrDefault(p => p.Name.Equals("data", StringComparison.OrdinalIgnoreCase));

        var source = dataProp?.Value as JObject ?? root;

        var nodes = new List<SaveNode>();
        foreach (var prop in source.Properties())
            nodes.Add(FromToken(prop.Name, prop.Value, game));
        return nodes;
    }

    /// <summary>Routes a JSON token to the appropriate node builder based on its type.</summary>
    private static SaveNode FromToken(string name, JToken token, Game game)
    {
        return token.Type switch
        {
            JTokenType.Object => FromObject(name, (JObject)token, game),
            JTokenType.Array  => FromArray(name, (JArray)token, game),
            _                 => FromPrimitive(name, token)
        };
    }

    /// <summary>
    /// Converts a JSON object to either a <see cref="StructNode"/> or an
    /// <see cref="EnumNode"/> depending on whether it has the enum signature.
    /// </summary>
    private static SaveNode FromObject(string name, JObject obj, Game game)
    {
        // Enum nodes are objects with exactly two keys: "Enum" and "Enum Value"
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
            node.Children.Add(FromToken(prop.Name, prop.Value, game));
        return node;
    }

    /// <summary>
    /// Converts a JSON array to an <see cref="ArrayNode"/>. The array is looked
    /// up in the registry to determine the type its items hold; this drives the
    /// badge displayed for the array container and its items in the UI.
    /// </summary>
    private static ArrayNode FromArray(string name, JArray arr, Game game)
    {
        var meta = ArrayTypeResolver.Resolve(game, name)
            ?? throw new InvalidOperationException(
                $"Array '{name}' is not registered for game {game}. " +
                $"Add it to UArrayRegistry to load this save.");

        var itemTypeHint = ArrayTypeResolver.ToTypeHint(meta.valueType);

        var node = new ArrayNode
        {
            Name           = name,
            TypeHint       = "array",
            IsFixed        = false,
            ItemTypeHint   = itemTypeHint,
            UnwrapForDisplay = ArrayTypeResolver.ShouldUnwrapForDisplay(meta)
        };

        // Build child nodes WITHOUT mutating the wrapper structure —
        // backing data stays exactly as JSON-parsed, the view layer looks
        // through wrappers when rendering.
        int i = 0;
        foreach (var item in arr)
            node.Items.Add(FromToken($"[{i++}]", item, game));

        return node;
    }

    /// <summary>
    /// Converts a primitive JSON token to a <see cref="PrimitiveNode"/>,
    /// inferring the type hint from the property name and value.
    /// </summary>
    private static PrimitiveNode FromPrimitive(string name, JToken token)
    {
        var typeHint = ResolveTypeHint(name, token);
        return new PrimitiveNode { Name = name, TypeHint = typeHint, Value = token.ToObject<object>() };
    }

    /// <summary>
    /// Resolves the correct type hint for a primitive token.
    /// In UE3, properties prefixed with "b" are either <c>bool</c> (if the JSON
    /// value is a boolean) or <c>byte</c> (if the JSON value is numeric),
    /// EXCEPT <c>bWasEncrypted</c> which is always stored as an int.
    /// </summary>
    private static string ResolveTypeHint(string name, JToken token)
    {
        bool isBPrefixed = name.Length > 1
                        && name[0] == 'b'
                        && char.IsUpper(name[1])
                        && !name.Equals("bWasEncrypted", StringComparison.OrdinalIgnoreCase);

        if (isBPrefixed)
        {
            if (token.Type == JTokenType.Boolean) return "bool";
            if (token.Type == JTokenType.Integer) return "byte";
        }

        return token.Type switch
        {
            JTokenType.Integer => "int",
            JTokenType.Float   => "float",
            JTokenType.Boolean => "bool",
            JTokenType.Bytes   => "byte",
            _                  => "string"
        };
    }
}
