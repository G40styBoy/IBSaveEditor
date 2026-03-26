using System;
using System.Collections.ObjectModel;
using System.Linq;
using ReactiveUI;
using IBSaveEditor.Models;

namespace IBSaveEditor.ViewModels;

public class NodeViewModel : ReactiveObject
{
    private bool _isExpanded;
    private object? _editValue;
    private string _enumType  = string.Empty;
    private string _enumValue = string.Empty;

    /// <summary>Called whenever children are added or removed, so the main VM can refresh VisibleNodes.</summary>
    public Action? OnChildrenChanged { get; set; }

    public NodeViewModel(SaveNode node, int depth = 0, Action? onChildrenChanged = null)
    {
        BackingNode       = node;
        Depth             = depth;
        Indent            = depth * 16;
        OnChildrenChanged = onChildrenChanged;

        if (node is StructNode s)
            Children = new ObservableCollection<NodeViewModel>(
                s.Children.Select(c => new NodeViewModel(c, depth + 1, onChildrenChanged)));
        else if (node is ArrayNode a)
            Children = new ObservableCollection<NodeViewModel>(
                a.Items.Select(i => new NodeViewModel(i, depth + 1, onChildrenChanged)));
        else
            Children = new ObservableCollection<NodeViewModel>();

        if (node is PrimitiveNode p)   _editValue = p.Value;
        else if (node is EnumNode e) { _enumType = e.EnumType; _enumValue = e.EnumValue; }
    }

    public SaveNode BackingNode { get; }
    public int Depth  { get; }
    public int Indent { get; }

    public string   Name     => BackingNode.Name;
    public string   TypeHint => BackingNode.TypeHint;
    public NodeType NodeType => BackingNode.NodeType;

    public bool IsPrimitive  => BackingNode.NodeType == NodeType.Primitive;
    public bool IsStruct     => BackingNode.NodeType == NodeType.Struct;
    public bool IsArray      => BackingNode.NodeType == NodeType.Array;
    public bool IsEnum       => BackingNode.NodeType == NodeType.Enum;
    public bool IsFixedArray => BackingNode is ArrayNode { IsFixed: true };
    public bool HasChildren  => Children.Count > 0;
    public string ArrayItemTypeHint => BackingNode is ArrayNode a ? a.ItemTypeHint : "string";

    public ObservableCollection<NodeViewModel> Children { get; }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            this.RaiseAndSetIfChanged(ref _isExpanded, value);
            this.RaisePropertyChanged(nameof(ExpanderGlyph));
        }
    }

    public string ExpanderGlyph => HasChildren ? (IsExpanded ? "▾" : "▸") : "  ";

    public object? EditValue
    {
        get => _editValue;
        set
        {
            this.RaiseAndSetIfChanged(ref _editValue, value);
            if (BackingNode is PrimitiveNode p) p.Value = value;
        }
    }

    public string EnumType
    {
        get => _enumType;
        set { this.RaiseAndSetIfChanged(ref _enumType, value); if (BackingNode is EnumNode e) e.EnumType = value; }
    }

    public string EnumValue
    {
        get => _enumValue;
        set { this.RaiseAndSetIfChanged(ref _enumValue, value); if (BackingNode is EnumNode e) e.EnumValue = value; }
    }

    public void AddItem(string? typeHintOverride = null)
    {
        if (BackingNode is not ArrayNode a || a.IsFixed) return;
        var typeHint = typeHintOverride ?? a.ItemTypeHint;
        var idx      = a.Items.Count;
        var newNode  = MakeNode($"[{idx}]", typeHint);
        a.Items.Add(newNode);
        if (idx == 0) a.ItemTypeHint = typeHint;
        // Auto-expand so the new item is visible in the tree
        IsExpanded = true;
        Children.Add(new NodeViewModel(newNode, Depth + 1, OnChildrenChanged));
        this.RaisePropertyChanged(nameof(HasChildren));
        this.RaisePropertyChanged(nameof(ExpanderGlyph));
        OnChildrenChanged?.Invoke();
    }

    public void RemoveArrayItem(NodeViewModel item)
    {
        if (BackingNode is not ArrayNode a || a.IsFixed) return;
        var idx = Children.IndexOf(item);
        if (idx < 0) return;
        a.Items.RemoveAt(idx);
        Children.RemoveAt(idx);
        for (int i = idx; i < a.Items.Count; i++)
            a.Items[i].Name = $"[{i}]";
        this.RaisePropertyChanged(nameof(HasChildren));
        this.RaisePropertyChanged(nameof(ExpanderGlyph));
        OnChildrenChanged?.Invoke();
    }

    public void AddMember(string name, string typeHint)
    {
        if (BackingNode is not StructNode s) return;
        var newNode = MakeNode(name, typeHint);
        s.Children.Add(newNode);
        IsExpanded = true;
        Children.Add(new NodeViewModel(newNode, Depth + 1, OnChildrenChanged));
        this.RaisePropertyChanged(nameof(HasChildren));
        this.RaisePropertyChanged(nameof(ExpanderGlyph));
        OnChildrenChanged?.Invoke();
    }

    public void RemoveStructMember(NodeViewModel member)
    {
        if (BackingNode is not StructNode s) return;
        var idx = Children.IndexOf(member);
        if (idx < 0) return;
        s.Children.RemoveAt(idx);
        Children.RemoveAt(idx);
        this.RaisePropertyChanged(nameof(HasChildren));
        this.RaisePropertyChanged(nameof(ExpanderGlyph));
        OnChildrenChanged?.Invoke();
    }


    public void DuplicateArrayItem(NodeViewModel item)
    {
        if (BackingNode is not ArrayNode a || a.IsFixed) return;
        var idx = Children.IndexOf(item);
        if (idx < 0) return;
        var cloned = CloneNode(a.Items[idx], $"[{a.Items.Count}]");
        a.Items.Add(cloned);
        IsExpanded = true;
        Children.Add(new NodeViewModel(cloned, Depth + 1, OnChildrenChanged));
        this.RaisePropertyChanged(nameof(HasChildren));
        this.RaisePropertyChanged(nameof(ExpanderGlyph));
        OnChildrenChanged?.Invoke();
    }

    public void DuplicateStructMember(NodeViewModel member)
    {
        if (BackingNode is not StructNode s) return;
        var idx = Children.IndexOf(member);
        if (idx < 0) return;
        // Find a unique name by appending _copy
        var baseName = s.Children[idx].Name;
        var newName  = baseName + "_copy";
        int suffix   = 2;
        while (s.Children.Any(c => c.Name == newName))
            newName = baseName + "_copy" + suffix++;
        var cloned = CloneNode(s.Children[idx], newName);
        s.Children.Add(cloned);
        IsExpanded = true;
        Children.Add(new NodeViewModel(cloned, Depth + 1, OnChildrenChanged));
        this.RaisePropertyChanged(nameof(HasChildren));
        this.RaisePropertyChanged(nameof(ExpanderGlyph));
        OnChildrenChanged?.Invoke();
    }

    // Deep-clone a SaveNode with a new name
    private static SaveNode CloneNode(SaveNode source, string newName)
    {
        switch (source)
        {
            case PrimitiveNode p:
                return new PrimitiveNode { Name = newName, TypeHint = p.TypeHint, Value = p.Value };
            case EnumNode e:
                return new EnumNode { Name = newName, TypeHint = "enum", EnumType = e.EnumType, EnumValue = e.EnumValue };
            case StructNode s:
                var ns = new StructNode { Name = newName, TypeHint = "struct" };
                foreach (var child in s.Children)
                    ns.Children.Add(CloneNode(child, child.Name));
                return ns;
            case ArrayNode a:
                var na = new ArrayNode { Name = newName, TypeHint = "array", IsFixed = a.IsFixed, ItemTypeHint = a.ItemTypeHint };
                int i = 0;
                foreach (var item in a.Items)
                    na.Items.Add(CloneNode(item, $"[{i++}]"));
                return na;
            default:
                return new PrimitiveNode { Name = newName, TypeHint = "string", Value = string.Empty };
        }
    }

    private static SaveNode MakeNode(string name, string typeHint) => typeHint switch
    {
        "struct" => new StructNode   { Name = name, TypeHint = "struct" },
        "array"  => new ArrayNode    { Name = name, TypeHint = "array", IsFixed = false },
        "enum"   => new EnumNode     { Name = name, TypeHint = "enum" },
        _        => new PrimitiveNode { Name = name, TypeHint = typeHint, Value = DefaultValue(typeHint) }
    };

    private static object DefaultValue(string t) => t switch
    {
        "int"   => (long)0,
        "float" => 0.0,
        "bool"  => false,
        "byte"  => (long)0,
        _       => string.Empty
    };

    public void CommitToNode()
    {
        if (BackingNode is PrimitiveNode p) p.Value = _editValue;
        else foreach (var child in Children) child.CommitToNode();
    }
}
