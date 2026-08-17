using System.Collections.ObjectModel;
using ReactiveUI;

namespace IBSaveEditor.ViewModels;

/// <summary>
/// Owns the raw property-tree editing surface : search filtering, expand/collapse
/// state, the flattened <see cref="VisibleNodes"/> list, and the current selection.
/// <para>
/// Reads live off the root <see cref="NodeViewModel"/> collection it's given rather
/// than a snapshot, so it doesn't need to be reconstructed when
/// <c>MainWindowViewModel</c> clears and repopulates that collection on every load :
/// callers just call <see cref="RebuildVisibleList"/> (or <see cref="OnChildrenChanged"/>)
/// after mutating the root collection.
/// </para>
/// </summary>
public class AdvancedTreeViewModel : ReactiveObject, IShellTab
{
    private readonly ObservableCollection<NodeViewModel> _rootNodes;

    private NodeViewModel? _selectedNode;
    private string         _searchText = string.Empty;

    public AdvancedTreeViewModel(ObservableCollection<NodeViewModel> rootNodes)
    {
        _rootNodes = rootNodes;
        _rootNodes.CollectionChanged += (_, _) => this.RaisePropertyChanged(nameof(IsEmpty));
    }

    /// <summary>
    /// The shell's escape hatch : always present as a tab regardless of how many
    /// manifest tabs a game has, since it's the only surface that can reach a save
    /// path the manifest doesn't cover.
    /// </summary>
    public string Title => "Advanced";

    /// <summary>
    /// True before any file is loaded (root nodes are only ever cleared and left
    /// empty between loads, never left empty by a search filter - that only ever
    /// touches <see cref="VisibleNodes"/>). Drives the "no file loaded" empty state.
    /// </summary>
    public bool IsEmpty => _rootNodes.Count == 0;

    public ObservableCollection<NodeViewModel> VisibleNodes { get; } = new();

    public NodeViewModel? SelectedNode
    {
        get => _selectedNode;
        set => this.RaiseAndSetIfChanged(ref _selectedNode, value);
    }

    /// <summary>
    /// Search filter text. When non-empty, the property tree shows only
    /// matching nodes plus the path to each match (parents stay visible).
    /// </summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            this.RaiseAndSetIfChanged(ref _searchText, value);
            RebuildVisibleList();
        }
    }

    /// <summary>Toggles the expanded state of a node and rebuilds the visible list.</summary>
    public void ToggleExpand(NodeViewModel node)
    {
        if (!node.HasChildren) return;
        var previousSelection = SelectedNode;
        node.IsExpanded = !node.IsExpanded;
        RebuildVisibleList();
        SelectedNode = previousSelection;
    }

    /// <summary>Called when a node's children collection changes so the visible list refreshes.</summary>
    public void OnChildrenChanged()
    {
        var previousSelection = SelectedNode;
        RebuildVisibleList();
        SelectedNode = previousSelection;
    }

    /// <summary>
    /// Rebuilds the flat <see cref="VisibleNodes"/> list from the root tree,
    /// honoring the current expanded state and search filter.
    /// </summary>
    public void RebuildVisibleList()
    {
        VisibleNodes.Clear();

        if (string.IsNullOrWhiteSpace(_searchText))
        {
            // No filter : show full tree based on expanded state
            foreach (var root in _rootNodes)
                AppendVisible(root);
        }
        else
        {
            // Filter active : show all matches plus their ancestor path
            var filter = _searchText.Trim();
            foreach (var root in _rootNodes)
                AppendFiltered(root, filter);
        }
    }

    /// <summary>Recursively appends a node and its expanded children to VisibleNodes.</summary>
    private void AppendVisible(NodeViewModel node)
    {
        VisibleNodes.Add(node);
        if (node.IsExpanded)
            foreach (var child in node.Children)
                AppendVisible(child);
    }

    /// <summary>
    /// Recursively walks a subtree and appends matching nodes to <see cref="VisibleNodes"/>.
    /// <para>
    /// A node is included when either it matches the filter directly, OR any
    /// descendant matches. When a node matches directly, its entire subtree is
    /// shown so the user can see the children of matched containers.
    /// </para>
    /// </summary>
    /// <returns>True if this node or any descendant matched the filter.</returns>
    private bool AppendFiltered(NodeViewModel node, string filter)
    {
        bool selfMatches = node.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase)
                        || node.Name.Contains(filter, StringComparison.OrdinalIgnoreCase);

        // If self matches, show the whole subtree under this node
        if (selfMatches)
        {
            AppendSubtree(node);
            return true;
        }

        // Otherwise, only show this node if any descendant matches :
        // and only the path of matching descendants, not their full subtrees
        var pathDescendants = new List<NodeViewModel>();
        foreach (var child in node.Children)
            CollectMatchingPath(child, filter, pathDescendants);

        if (pathDescendants.Count > 0)
        {
            VisibleNodes.Add(node);
            foreach (var d in pathDescendants)
                VisibleNodes.Add(d);
            return true;
        }

        return false;
    }

    /// <summary>Appends a node and every descendant unconditionally.</summary>
    private void AppendSubtree(NodeViewModel node)
    {
        VisibleNodes.Add(node);
        foreach (var child in node.Children)
            AppendSubtree(child);
    }

    /// <summary>
    /// Walks a subtree to find matching nodes plus their ancestor path.
    /// Used when a parent did NOT match but we still need to show child matches.
    /// </summary>
    private bool CollectMatchingPath(NodeViewModel node, string filter, List<NodeViewModel> output)
    {
        bool selfMatches = node.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase)
                        || node.Name.Contains(filter, StringComparison.OrdinalIgnoreCase);

        if (selfMatches)
        {
            // Self matches → include the full subtree below
            AppendToList(node, output);
            return true;
        }

        // Self doesn't match : keep walking, only include if descendants match
        var subDescendants = new List<NodeViewModel>();
        foreach (var child in node.Children)
            CollectMatchingPath(child, filter, subDescendants);

        if (subDescendants.Count > 0)
        {
            output.Add(node);
            foreach (var d in subDescendants)
                output.Add(d);
            return true;
        }
        return false;
    }

    /// <summary>Appends a node and every descendant to a flat list.</summary>
    private void AppendToList(NodeViewModel node, List<NodeViewModel> output)
    {
        output.Add(node);
        foreach (var child in node.Children)
            AppendToList(child, output);
    }
}
