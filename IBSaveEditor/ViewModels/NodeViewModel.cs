using System;
using System.Collections.ObjectModel;
using System.Linq;
using ReactiveUI;
using IBSaveEditor.Models;

namespace IBSaveEditor.ViewModels;

/// <summary>
/// ViewModel wrapping a single <see cref="SaveNode"/> for the editor UI.
/// <para>
/// Most properties are simple pass-throughs to the backing node, but this VM
/// also handles "display unwrapping" for static arrays of primitive types.
/// </para>
/// <para>
/// In static non-struct arrays (e.g. <c>NumConsumable</c>, <c>ShowConsumableBadge</c>),
/// the JSON layout wraps EVERY value into a single struct that lives at index [0]
/// of the array. So <c>NumConsumable</c> looks like:
/// <code>
/// [ { "TRA_GrabBag_Small": 1, "TRA_Potion_HealthL": 3, ... 14 keys ... } ]
/// </code>
/// </para>
/// <para>
/// The user shouldn't have to drill through that wrapper layer. Instead, this VM
/// promotes each key inside the wrapper to its OWN VM with the property name as
/// the display name and the value editable inline. The backing node tree is left
/// untouched so serialization round-trips cleanly.
/// </para>
/// </summary>
public class NodeViewModel : ReactiveObject
{
    private bool    _isExpanded;
    private object? _editValue;
    private string  _enumType  = string.Empty;
    private string  _enumValue = string.Empty;

    /// <summary>
    /// When non-null, this VM displays itself as a primitive whose value lives
    /// inside an entry of a parent wrapper struct. All UI reads/writes route here.
    /// </summary>
    private PrimitiveNode? _unwrappedTarget;

    /// <summary>
    /// When this VM represents a key promoted out of a wrapper struct, this is
    /// the wrapper that owns the underlying <see cref="_unwrappedTarget"/>.
    /// Needed for duplicate/remove operations to mutate the right collection.
    /// </summary>
    private StructNode? _unwrappedParentWrapper;

    /// <summary>Called whenever children are added/removed so the main VM can refresh VisibleNodes.</summary>
    public Action? OnChildrenChanged { get; set; }

    public NodeViewModel(SaveNode node, int depth = 0, Action? onChildrenChanged = null)
    {
        BackingNode       = node;
        Depth             = depth;
        Indent            = depth * 16;
        OnChildrenChanged = onChildrenChanged;

        if (node is ArrayNode a && a.UnwrapForDisplay)
        {
            // Static non-struct array — promote every key inside the wrapper
            // struct(s) to its own VM. Each key becomes a virtual array entry.
            Children = BuildUnwrappedChildren(a, depth + 1, onChildrenChanged);
        }
        else if (node is StructNode s)
        {
            Children = new ObservableCollection<NodeViewModel>(
                s.Children.Select(c => new NodeViewModel(c, depth + 1, onChildrenChanged)));
        }
        else if (node is ArrayNode arr)
        {
            // Regular array — items map 1:1 to children. Mark them as array items
            // so they don't redundantly show the same type badge as the container.
            Children = new ObservableCollection<NodeViewModel>(
                arr.Items.Select(i =>
                {
                    var childVm = new NodeViewModel(i, depth + 1, onChildrenChanged);
                    childVm.IsArrayItem = true;
                    return childVm;
                }));
        }
        else
            Children = new ObservableCollection<NodeViewModel>();

        if (node is PrimitiveNode p)   _editValue = p.Value;
        else if (node is EnumNode e) { _enumType = e.EnumType; _enumValue = e.EnumValue; }
    }

    /// <summary>
    /// For a wrapper-style array, walks every entry (typically just one wrapper
    /// struct) and promotes each key inside it to its own VM. The promoted VMs
    /// display as primitives with the key name and the inner value.
    /// </summary>
    private static ObservableCollection<NodeViewModel> BuildUnwrappedChildren(
        ArrayNode array, int depth, Action? onChildrenChanged)
    {
        var children = new ObservableCollection<NodeViewModel>();

        foreach (var item in array.Items)
        {
            // Each array item should be a wrapper struct — walk its keys
            if (item is not StructNode wrapper) continue;

            foreach (var entry in wrapper.Children)
            {
                if (entry is not PrimitiveNode prim) continue;

                var vm = new NodeViewModel(prim, depth, onChildrenChanged)
                {
                    _unwrappedTarget        = prim,
                    _unwrappedParentWrapper = wrapper,
                    IsArrayItem             = true
                };
                children.Add(vm);
            }
        }

        return children;
    }

    public SaveNode BackingNode { get; }
    public int Depth  { get; }
    public int Indent { get; }

    public string Name => BackingNode.Name;

    /// <summary>
    /// User-facing display name. Array indices like <c>[0]</c> are shown as <c>Entry 1</c>.
    /// Promoted wrapper keys show their original name (e.g. <c>TRA_GrabBag_Small</c>).
    /// </summary>
    public string DisplayName
    {
        get
        {
            // Promoted keys keep their property name as-is
            if (_unwrappedTarget != null) return BackingNode.Name;

            var n = BackingNode.Name;
            if (n.Length >= 3 && n[0] == '[' && n[n.Length - 1] == ']')
            {
                if (int.TryParse(n.Substring(1, n.Length - 2), out var idx))
                    return $"Entry {idx + 1}";
            }
            return n;
        }
    }

    /// <summary>
    /// Type hint used for the badge. Reflects the inner primitive when unwrapped,
    /// or the array's contained type for arrays.
    /// </summary>
    public string TypeHint
    {
        get
        {
            if (_unwrappedTarget != null) return _unwrappedTarget.TypeHint;
            if (BackingNode is ArrayNode a) return a.ItemTypeHint;
            return BackingNode.TypeHint;
        }
    }

    public NodeType NodeType =>
        _unwrappedTarget != null ? NodeType.Primitive : BackingNode.NodeType;

    public bool IsPrimitive  => NodeType == NodeType.Primitive;
    public bool IsStruct     => NodeType == NodeType.Struct;
    public bool IsArray      => NodeType == NodeType.Array;
    public bool IsEnum       => NodeType == NodeType.Enum;
    public bool IsFixedArray => BackingNode is ArrayNode { IsFixed: true };

    /// <summary>
    /// True when this VM represents a direct item of an array. Set by the
    /// parent array when building children. Used by the UI to suppress the
    /// type badge on array items (the array container already labels them).
    /// </summary>
    public bool IsArrayItem { get; private set; }

    /// <summary>
    /// True when ANY badge should be shown for this node. Suppressed for direct
    /// array items because the array container's badge already declares the type.
    /// </summary>
    public bool ShowBadge => !IsArrayItem;

    /// <summary>True when the type (non-fname) badge should render.</summary>
    public bool ShowBadgeNonIni => ShowBadge && !IsIni;

    /// <summary>True when the fname badge should render.</summary>
    public bool ShowBadgeIni => ShowBadge && IsIni;

    /// <summary>
    /// True when this node should display the <c>fname</c> badge. Triggers when:
    /// (1) the property name starts with <c>ini_</c>, OR
    /// (2) the value is a string starting with <c>ini_</c>, OR
    /// (3) the node is an array whose registered item type is <c>name</c>
    ///     (every value inside such an array is by definition an fname).
    /// </summary>
    public bool IsIni
    {
        get
        {
            if (BackingNode.Name.StartsWith("ini_", StringComparison.OrdinalIgnoreCase))
                return true;

            // Arrays of name-typed items are entirely fname-based per registry rules
            if (BackingNode is ArrayNode arr)
                return arr.ItemTypeHint == "name";

            // Other primitives — check the value
            var v = (_unwrappedTarget?.Value
                  ?? (BackingNode as PrimitiveNode)?.Value)?.ToString();

            return v != null
                && v.StartsWith("ini_", StringComparison.OrdinalIgnoreCase);
        }
    }

    public bool HasChildren => Children.Count > 0;

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

    /// <summary>
    /// The edit value shown in the property editor. Routes to the inner primitive
    /// when this node is a promoted wrapper key.
    /// </summary>
    public object? EditValue
    {
        get => _editValue;
        set
        {
            this.RaiseAndSetIfChanged(ref _editValue, value);
            if (_unwrappedTarget != null)
                _unwrappedTarget.Value = value;
            else if (BackingNode is PrimitiveNode p)
                p.Value = value;
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

    // ── Mutation ──────────────────────────────────────────────────────────────

    public void AddItem(string? typeHintOverride = null)
    {
        if (BackingNode is not ArrayNode a || a.IsFixed) return;
        var typeHint = typeHintOverride ?? a.ItemTypeHint;
        var idx      = a.Items.Count;
        var newNode  = MakeNode($"[{idx}]", typeHint);
        a.Items.Add(newNode);
        if (idx == 0) a.ItemTypeHint = typeHint;
        IsExpanded = true;
        var newVm = new NodeViewModel(newNode, Depth + 1, OnChildrenChanged);
        newVm.IsArrayItem = true;
        Children.Add(newVm);
        this.RaisePropertyChanged(nameof(HasChildren));
        this.RaisePropertyChanged(nameof(ExpanderGlyph));
        OnChildrenChanged?.Invoke();
    }

    public void RemoveArrayItem(NodeViewModel item)
    {
        if (BackingNode is not ArrayNode a || a.IsFixed) return;

        // Wrapper-style arrays — remove the underlying wrapper key, not an Items entry
        if (a.UnwrapForDisplay && item._unwrappedTarget != null && item._unwrappedParentWrapper != null)
        {
            item._unwrappedParentWrapper.Children.Remove(item._unwrappedTarget);
            Children.Remove(item);
            this.RaisePropertyChanged(nameof(HasChildren));
            this.RaisePropertyChanged(nameof(ExpanderGlyph));
            OnChildrenChanged?.Invoke();
            return;
        }

        // Normal array — items map 1:1 to children
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

        // Wrapper-style arrays — duplicate the underlying wrapper key
        if (a.UnwrapForDisplay && item._unwrappedTarget != null && item._unwrappedParentWrapper != null)
        {
            var clonedTarget = (PrimitiveNode)CloneNode(item._unwrappedTarget, item._unwrappedTarget.Name);
            item._unwrappedParentWrapper.Children.Add(clonedTarget);

            var _clonedVm = new NodeViewModel(clonedTarget, Depth + 1, OnChildrenChanged)
            {
                _unwrappedTarget        = clonedTarget,
                _unwrappedParentWrapper = item._unwrappedParentWrapper
            };

            IsExpanded = true;
            Children.Add(_clonedVm);
            this.RaisePropertyChanged(nameof(HasChildren));
            this.RaisePropertyChanged(nameof(ExpanderGlyph));
            OnChildrenChanged?.Invoke();
            return;
        }

        // Normal array — clone the matching item
        var idx = Children.IndexOf(item);
        if (idx < 0) return;
        var cloned = CloneNode(a.Items[idx], $"[{a.Items.Count}]");
        a.Items.Add(cloned);
        IsExpanded = true;
        var clonedVm = new NodeViewModel(cloned, Depth + 1, OnChildrenChanged);
        clonedVm.IsArrayItem = true;
        Children.Add(clonedVm);
        this.RaisePropertyChanged(nameof(HasChildren));
        this.RaisePropertyChanged(nameof(ExpanderGlyph));
        OnChildrenChanged?.Invoke();
    }

    public void DuplicateStructMember(NodeViewModel member)
    {
        if (BackingNode is not StructNode s) return;
        var idx = Children.IndexOf(member);
        if (idx < 0) return;
        var newName = s.Children[idx].Name;
        var cloned = CloneNode(s.Children[idx], newName);
        s.Children.Add(cloned);
        IsExpanded = true;
        Children.Add(new NodeViewModel(cloned, Depth + 1, OnChildrenChanged));
        this.RaisePropertyChanged(nameof(HasChildren));
        this.RaisePropertyChanged(nameof(ExpanderGlyph));
        OnChildrenChanged?.Invoke();
    }

    /// <summary>Deep-clones a SaveNode with a new name.</summary>
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
                var na = new ArrayNode
                {
                    Name           = newName,
                    TypeHint       = "array",
                    IsFixed        = a.IsFixed,
                    ItemTypeHint   = a.ItemTypeHint,
                    UnwrapForDisplay = a.UnwrapForDisplay
                };
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
        "struct" => new StructNode    { Name = name, TypeHint = "struct" },
        "array"  => new ArrayNode     { Name = name, TypeHint = "array", IsFixed = false },
        "enum"   => new EnumNode      { Name = name, TypeHint = "enum" },
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

    /// <summary>Pushes any in-flight edit value back to the backing node.</summary>
    public void CommitToNode()
    {
        if (_unwrappedTarget != null)
            _unwrappedTarget.Value = _editValue;
        else if (BackingNode is PrimitiveNode p)
            p.Value = _editValue;
        else
            foreach (var child in Children) child.CommitToNode();
    }
}
