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
}

public class ArrayNode : SaveNode
{
    public override NodeType NodeType => NodeType.Array;
    public bool IsFixed { get; set; }
    public int? FixedLength { get; set; }
    /// <summary>The TypeHint of the items inside this array — inferred from existing items.</summary>
    public string ItemTypeHint { get; set; } = "string";
    public ObservableCollection<SaveNode> Items { get; set; } = new();
}
