using System.Collections.ObjectModel;

namespace IBSaveEditor.Models;

public enum NodeType { Primitive, Struct, Array, Enum }

public abstract class SaveNode
{
    public string Name { get; set; } = string.Empty;
    public string TypeHint { get; set; } = string.Empty;
    public abstract NodeType NodeType { get; }
}

public class PrimitiveNode : SaveNode
{
    public override NodeType NodeType => NodeType.Primitive;
    public object? Value { get; set; }
}

public class EnumNode : SaveNode
{
    public override NodeType NodeType => NodeType.Enum;
    public string EnumType { get; set; } = string.Empty;
    public string EnumValue { get; set; } = string.Empty;
}

public class StructNode : SaveNode
{
    public override NodeType NodeType => NodeType.Struct;
    public ObservableCollection<SaveNode> Children { get; set; } = new();

    public bool UnwrapForDisplay { get; set; }
}

public class ArrayNode : SaveNode
{
    public override NodeType NodeType => NodeType.Array;
    public bool IsFixed { get; set; }
    public int? FixedLength { get; set; }

    /// <summary>The TypeHint of the items inside this array : inferred from the registry.</summary>
    public string ItemTypeHint { get; set; } = "string";

    /// <summary>
    /// True when this array's items have a JSON wrapper struct that should be
    /// hidden from the user. Set by the registry-aware <c>JsonToNodeTree</c>
    /// for static arrays of non-struct types (e.g. NumConsumable, ShowConsumableBadge).
    /// <para>
    /// The wrapper is preserved in the data layer for round-trip integrity :
    /// only the UI looks through it.
    /// </para>
    /// </summary>
    public bool UnwrapForDisplay { get; set; }

    public ObservableCollection<SaveNode> Items { get; set; } = new();
}
