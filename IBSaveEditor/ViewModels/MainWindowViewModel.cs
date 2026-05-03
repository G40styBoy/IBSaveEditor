using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReactiveUI;
using IBSaveEditor.Services;
using IBSaveEditor.Package;
using IBSaveEditor.Json;
using IBSaveEditor.Serialize;
using IBSaveEditor.Util;

namespace IBSaveEditor.ViewModels;

public class MainWindowViewModel : ReactiveObject
{
    private string?        _filePath;
    private NodeViewModel? _selectedNode;
    private string         _statusMessage = "No file loaded.";
    private bool           _isDirty;
    private string         _searchText    = string.Empty;

    private readonly ObservableCollection<NodeViewModel> _rootNodes = new();

    // Store the raw original JSON string so we can re-parse it clean on save/export
    private string? _originalJson;
    private bool    _hasDataWrapper;
    private string  _dataKey = "data";

    /// <summary>The game this save belongs to. Set during load.</summary>
    private Game _currentGame;

    public ObservableCollection<NodeViewModel> VisibleNodes { get; } = new();
    public ObservableCollection<string>        LogEntries   { get; } = new();

    public MainWindowViewModel()
    {
        OpenCommand   = ReactiveCommand.CreateFromTask(OpenFileAsync);
        SaveCommand   = ReactiveCommand.CreateFromTask(SaveFileAsync,
            this.WhenAnyValue(x => x.FilePath).Select(p => p != null));
        ReloadCommand = ReactiveCommand.CreateFromTask(ReloadAsync,
            this.WhenAnyValue(x => x.FilePath).Select(p => p != null));
    }

    public ReactiveCommand<Unit, Unit> OpenCommand   { get; }
    public ReactiveCommand<Unit, Unit> SaveCommand   { get; }
    public ReactiveCommand<Unit, Unit> ReloadCommand { get; }

    public string? FilePath
    {
        get => _filePath;
        private set => this.RaiseAndSetIfChanged(ref _filePath, value);
    }

    public string WindowTitle => "Infinity Blade Save Editor";

    public NodeViewModel? SelectedNode
    {
        get => _selectedNode;
        set => this.RaiseAndSetIfChanged(ref _selectedNode, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    public bool IsDirty
    {
        get => _isDirty;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isDirty, value);
            this.RaisePropertyChanged(nameof(WindowTitle));
        }
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

    /// <summary>
    /// Loads a save by deserializing a binary .bin file in memory.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if deserialization fails.</exception>
    public void LoadFromBin(string binPath)
    {
        try
        {
            Log($"Opening: {Path.GetFileName(binPath)}");
            StatusMessage = "Deserializing...";

            using var upk = new UnrealPackage(binPath);
            var properties = upk.DeserializeUPK();
            if (properties == null || properties.Count == 0)
                throw new InvalidOperationException("Deserialization produced no properties.");
            Log($"Deserialized {properties.Count} properties.");

            // Get JSON as string directly — no disk write
            var parser = new JsonDataParser(properties, upk.info);
            var json = parser.ReturnDataAsString();

            LoadFromJsonString(json, binPath);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            Log($"ERROR: {ex.Message}");
        }
    }

    /// <summary>Loads a save from an existing JSON file on disk.</summary>
    public void LoadFromPath(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            LoadFromJsonString(json, path);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            Log($"ERROR opening file: {ex.Message}");
        }
    }

    /// <summary>
    /// Shared loader for both .bin and .json paths. Parses the JSON, extracts
    /// the game from metadata, and builds the node tree.
    /// </summary>
    private void LoadFromJsonString(string json, string sourcePath)
    {
        var jobj = JObject.Parse(json);

        // Extract game from metadata — must succeed before we try to build the tree
        _currentGame = GameMetadataExtractor.Extract(jobj);
        Log($"Game: {_currentGame}");

        // Store raw string — re-parsed fresh on every save to avoid mutation issues
        _originalJson = json;

        // Detect envelope and remember the exact key casing
        var dataProp = jobj.Properties()
            .FirstOrDefault(p => p.Name.Equals("data", StringComparison.OrdinalIgnoreCase));
        _hasDataWrapper = dataProp != null;
        _dataKey        = dataProp?.Name ?? "data";

        var nodes = JsonToNodeTree.Convert(jobj, _currentGame);

        _rootNodes.Clear();
        SelectedNode = null;
        foreach (var n in nodes)
            _rootNodes.Add(new NodeViewModel(n, 0, OnChildrenChanged));

        FilePath = sourcePath;
        IsDirty  = false;
        RebuildVisibleList();
        StatusMessage = $"Loaded  {_rootNodes.Count} properties.";
        Log($"Opened {Path.GetFileName(sourcePath)}");
    }

    /// <summary>Saves the current node tree to disk as JSON, preserving metadata.</summary>
    public void SaveToPath(string path)
    {
        try
        {
            foreach (var n in _rootNodes) n.CommitToNode();

            var dataObj = NodeTreeToJson.Convert(_rootNodes.Select(v => v.BackingNode));

            JObject outputRoot;
            if (_hasDataWrapper && _originalJson != null)
            {
                // Re-parse original from stored raw string — guaranteed unmodified
                outputRoot = JObject.Parse(_originalJson);
                // Replace only the data section, metadata stays untouched
                outputRoot[_dataKey] = dataObj;
            }
            else
            {
                outputRoot = dataObj;
            }

            File.WriteAllText(path, outputRoot.ToString(Formatting.Indented));

            // Update stored raw string so subsequent saves stay consistent
            _originalJson = outputRoot.ToString(Formatting.Indented);

            FilePath = path;
            IsDirty  = false;
            StatusMessage = $"Saved  {Path.GetFileName(path)}";
            Log($"Saved JSON: {Path.GetFileName(path)}");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving: {ex.Message}";
            Log($"ERROR saving: {ex.Message}");
        }
    }

    /// <summary>
    /// Exports the current node tree to a binary .bin file via the full
    /// JSON → cruncher → serializer pipeline, writing to OUTPUT.
    /// </summary>
    public void ExportToBin()
    {
        if (_originalJson == null)
        {
            Log("ERROR: No save data loaded.");
            return;
        }

        ToolPaths.ValidateOutputDirectory();
        try
        {
            // Commit any pending edits to the node tree first
            foreach (var n in _rootNodes) n.CommitToNode();

            var dataObj = NodeTreeToJson.Convert(_rootNodes.Select(v => v.BackingNode));
            JObject outputRoot;
            if (_hasDataWrapper)
            {
                outputRoot = JObject.Parse(_originalJson);
                outputRoot[_dataKey] = dataObj;
            }
            else
            {
                outputRoot = dataObj;
            }

            var currentJson = outputRoot.ToString(Formatting.Indented);

            Log($"Serializing: {Path.GetFileName(FilePath ?? "save")}");
            StatusMessage = "Serializing...";

            using var tempFile = new TempJsonFile(currentJson);
            EnvelopeMeta meta = JsonUtils.ReadMeta(tempFile.Path);
            var info = new PackageInfo();
            info.SetPackageName(meta.PackageName);
            info.SetGame(meta.Game);
            info.SetIsEncrypted(meta.IsEncrypted);
            info.SetSaveVersion(meta.SaveVersion);
            info.SetSaveMagic(meta.SaveMagic);
            Log($"Metadata: {meta.PackageName} / {meta.Game}");

            string dataJson  = JsonUtils.ExtractDataObjectJson(tempFile.Path, "data");
            var cruncher     = new JsonDataCruncher(dataJson, info.game);
            var crunchedData = cruncher.ReadJsonFile();
            if (crunchedData == null)
                throw new InvalidOperationException("JSON could not be parsed into save data.");
            Log("JSON crunched successfully.");

            using var serializer = new Serializer(info, crunchedData);
            serializer.SerializeAndOutputData();

            StatusMessage = "Exported. .bin written to OUTPUT.";
            Log(".bin written to OUTPUT. Done.");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export failed: {ex.Message}";
            Log($"ERROR: {ex.Message}");
        }
    }

    public void MarkDirty() => IsDirty = true;

    /// <summary>Called by NodeViewModel when its children collection changes.</summary>
    private void OnChildrenChanged()
    {
        var previousSelection = SelectedNode;
        RebuildVisibleList();
        SelectedNode = previousSelection;
    }

    /// <summary>
    /// Rebuilds the flat <see cref="VisibleNodes"/> list from the root tree,
    /// honoring the current expanded state and search filter.
    /// </summary>
    private void RebuildVisibleList()
    {
        VisibleNodes.Clear();

        if (string.IsNullOrWhiteSpace(_searchText))
        {
            // No filter — show full tree based on expanded state
            foreach (var root in _rootNodes)
                AppendVisible(root);
        }
        else
        {
            // Filter active — show all matches plus their ancestor path
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

        // Otherwise, only show this node if any descendant matches —
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

        // Self doesn't match — keep walking, only include if descendants match
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

    public void Log(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        LogEntries.Add($"[{timestamp}]  {message}");
    }

    public void ClearLog() => LogEntries.Clear();

    // ── Command stubs ─────────────────────────────────────────────────────────
    private Task OpenFileAsync()  => Task.CompletedTask;
    private Task SaveFileAsync()  { if (FilePath != null) SaveToPath(FilePath); return Task.CompletedTask; }
    private Task ReloadAsync()
    {
        if (FilePath == null) return Task.CompletedTask;
        if (Path.GetExtension(FilePath).Equals(".bin", StringComparison.OrdinalIgnoreCase))
            LoadFromBin(FilePath);
        else
            LoadFromPath(FilePath);
        return Task.CompletedTask;
    }
}
