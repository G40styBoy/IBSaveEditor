using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReactiveUI;
using IBSaveEditor.Manifest;
using IBSaveEditor.Models;
using IBSaveEditor.Services;
using IBSaveEditor.Package;
using IBSaveEditor.Json;
using IBSaveEditor.Serialize;
using IBSaveEditor.Util;

namespace IBSaveEditor.ViewModels;

public class MainWindowViewModel : ReactiveObject
{
    private string?        _filePath;
    private string         _statusMessage = "No file loaded.";
    private bool           _isDirty;
    private Fields.ManifestTabsViewModel? _manifestTabs;
    private IReadOnlyList<IShellTab> _shellTabs = Array.Empty<IShellTab>();
    private IShellTab? _selectedShellTab;

    private readonly ObservableCollection<NodeViewModel> _rootNodes = new();

    // Store the raw original JSON string so we can re-parse it clean on save/export
    private string? _originalJson;
    private bool    _hasDataWrapper;
    private string  _dataKey = "data";

    /// <summary>The game this save belongs to. Set during load.</summary>
    private Game _currentGame;

    public ObservableCollection<string> LogEntries { get; } = new();

    /// <summary>Owns the raw property-tree editing surface (search, expand state, selection).</summary>
    public AdvancedTreeViewModel AdvancedTree { get; }

    /// <summary>
    /// The root save-data node tree, independent of expansion state (unlike
    /// <see cref="AdvancedTreeViewModel.VisibleNodes"/>, which only lists what's currently expanded).
    /// This is the seam manifest-driven field editing (<c>Fields.FieldViewModel</c>)
    /// binds against : both surfaces mutate the same backing <see cref="SaveNode"/>
    /// objects, so edits made through either one stay in sync.
    /// </summary>
    public IReadOnlyList<SaveNode> RootNodes => _rootNodes.Select(v => v.BackingNode).ToList();

    /// <summary>The game the currently loaded save belongs to.</summary>
    public Game CurrentGame => _currentGame;

    /// <summary>True once a save is loaded and its game has at least one manifest tab worth previewing.</summary>
    private bool HasManifestTabs => FilePath != null && ManifestRegistry.Get(_currentGame).Tabs.Count > 0;

    /// <summary>
    /// The manifest tabs for whatever save is currently loaded, or null if
    /// <see cref="HasManifestTabs"/> is false. Rebuilt from scratch inside
    /// <see cref="LoadFromJsonString"/> on every load : <see cref="Fields.FieldViewModel"/>
    /// resolves its backing node once, in its constructor, so a tab set built against a
    /// previous save's <see cref="SaveNode"/> instances would silently keep pointing at
    /// them after <see cref="_rootNodes"/> is cleared and repopulated - edits would appear
    /// to work while export wrote nothing. Rebuilding here rather than caching keeps that
    /// impossible instead of relying on callers to remember to refresh it.
    /// </summary>
    public Fields.ManifestTabsViewModel? ManifestTabs
    {
        get => _manifestTabs;
        private set => this.RaiseAndSetIfChanged(ref _manifestTabs, value);
    }

    /// <summary>
    /// The main shell's tabs: every manifest tab for the currently loaded save (if any),
    /// followed by <see cref="AdvancedTree"/> - always present, even for a game whose
    /// manifest has zero tabs, since it's the only surface that can reach a save path
    /// the manifest doesn't cover.
    /// </summary>
    public IReadOnlyList<IShellTab> ShellTabs
    {
        get => _shellTabs;
        private set => this.RaiseAndSetIfChanged(ref _shellTabs, value);
    }

    /// <summary>
    /// The shell's active tab. Set explicitly (rather than left to TabControl's default
    /// selection) every time <see cref="ShellTabs"/> is rebuilt : <see cref="AdvancedTree"/>
    /// is the one stable instance across every rebuild, so if the control were left to
    /// preserve selection by reference on its own, loading a save while Advanced happened
    /// to be selected - including the very first load, before any manifest tab has ever
    /// existed - would keep landing on the escape hatch instead of the manifest tabs it's
    /// meant to be secondary to.
    /// </summary>
    public IShellTab? SelectedShellTab
    {
        get => _selectedShellTab;
        set => this.RaiseAndSetIfChanged(ref _selectedShellTab, value);
    }

    public MainWindowViewModel()
    {
        AdvancedTree = new AdvancedTreeViewModel(_rootNodes);
        ShellTabs    = new List<IShellTab> { AdvancedTree };
        SelectedShellTab = AdvancedTree;

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
    /// Loads a save by deserializing a binary .bin file in memory.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if deserialization fails.</exception>
    public void LoadFromBin(string binPath)
    {
        try
        {
            Log($"Loading file: {Path.GetFileName(binPath)}");
            StatusMessage = "Deserializing...";

            using var upk = new UnrealPackage(binPath);
            var properties = upk.ReadProperties();
            if (properties == null || properties.Count == 0)
                throw new InvalidOperationException("Deserialization produced no properties.");

            // Get JSON as string directly : no disk write
            var parser = new JsonDataParser(properties, upk.info);
            var json = parser.ReturnDataAsString();

            LoadFromJsonString(json, binPath);
            Log($"File loaded successfully!");
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
            Log($"Loading file: {Path.GetFileName(path)}");
            var json = File.ReadAllText(path);
            LoadFromJsonString(json, path);
            Log($"File loaded successfully!");
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

        // Extract game from metadata : must succeed before we try to build the tree
        _currentGame = GameMetadataExtractor.Extract(jobj);
        // Store raw string : re-parsed fresh on every save to avoid mutation issues
        _originalJson = json;

        // Detect envelope and remember the exact key casing
        var dataProp = jobj.Properties()
            .FirstOrDefault(p => p.Name.Equals("data", StringComparison.OrdinalIgnoreCase));
        _hasDataWrapper = dataProp != null;
        _dataKey        = dataProp?.Name ?? "data";

        var nodes = JsonToNodeTree.Convert(jobj, _currentGame);

        _rootNodes.Clear();
        AdvancedTree.SelectedNode = null;
        foreach (var n in nodes)
            _rootNodes.Add(new NodeViewModel(n, 0, OnChildrenChanged, MarkDirty));

        FilePath = sourcePath;
        IsDirty  = false;
        AdvancedTree.RebuildVisibleList();
        ManifestTabs = HasManifestTabs
            ? new Fields.ManifestTabsViewModel(ManifestRegistry.Get(_currentGame), RootNodes, MarkDirty)
            : null;
        ShellTabs = BuildShellTabs();
        SelectedShellTab = ShellTabs[0];
        StatusMessage = $"Loaded  {_rootNodes.Count} properties.";
    }

    /// <summary>Every manifest tab (if any) followed by the always-present Advanced tab.</summary>
    private List<IShellTab> BuildShellTabs()
    {
        var tabs = new List<IShellTab>();
        if (ManifestTabs != null)
            tabs.AddRange(ManifestTabs.Tabs);
        tabs.Add(AdvancedTree);
        return tabs;
    }

    /// <summary>Saves the current node tree to disk as JSON, preserving metadata.</summary>
    public void SaveToPath(string path)
    {
        try
        {
            var dataObj = NodeTreeToJson.Convert(_rootNodes.Select(v => v.BackingNode));

            JObject outputRoot;
            if (_hasDataWrapper && _originalJson != null)
            {
                // Re-parse original from stored raw string : guaranteed unmodified
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

            StatusMessage = "Serializing...";

            using var tempFile = new TempJsonFile(currentJson);
            EnvelopeMeta meta = JsonUtils.ReadMeta(tempFile.Path);
            var info = new PackageInfo();
            info.SetPackageName(meta.PackageName);
            info.SetGame(meta.Game);
            info.SetIsEncrypted(meta.IsEncrypted);
            info.SetSaveVersion(meta.SaveVersion);
            info.SetSaveMagic(meta.SaveMagic);

            string dataJson  = JsonUtils.ExtractDataObjectJson(tempFile.Path, "data");
            var cruncher     = new JsonDataCruncher(dataJson, info.game);
            var crunchedData = cruncher.ReadJsonFile();
            if (crunchedData == null)
                throw new InvalidOperationException("JSON could not be parsed into save data.");

            using var serializer = new Serializer(info, crunchedData);
            serializer.SerializeAndOutputData();

            StatusMessage = "Exported. .bin written to OUTPUT.";
            Log(".bin successfully written to OUTPUT!");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export failed: {ex.Message}";
            Log($"ERROR: {ex.Message}");
        }
    }

    public void MarkDirty() => IsDirty = true;

    /// <summary>Called by NodeViewModel when its children collection changes.</summary>
    private void OnChildrenChanged() => AdvancedTree.OnChildrenChanged();

    public void Log(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        LogEntries.Add($"[{timestamp}]  {message}");
    }

    public void ClearLog() => LogEntries.Clear();

    #region Command Stubs

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

    #endregion
}
