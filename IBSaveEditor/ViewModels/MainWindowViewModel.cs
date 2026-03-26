using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReactiveUI;
using IBSaveEditor.Services;

namespace IBSaveEditor.ViewModels;

public class MainWindowViewModel : ReactiveObject
{
    private string?        _filePath;
    private NodeViewModel? _selectedNode;
    private string         _statusMessage = "No file loaded.";
    private bool           _isDirty;

    private readonly ObservableCollection<NodeViewModel> _rootNodes = new();

    // Store the raw original JSON string so we can re-parse it clean on save
    private string? _originalJson;
    private bool    _hasDataWrapper;
    private string  _dataKey = "data"; // preserves original casing

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

    public void ToggleExpand(NodeViewModel node)
    {
        if (!node.HasChildren) return;
        var previousSelection = SelectedNode;
        node.IsExpanded = !node.IsExpanded;
        RebuildVisibleList();
        SelectedNode = previousSelection;
    }

    public void LoadFromPath(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            var jobj = JObject.Parse(json);

            // Store raw string — re-parsed fresh on every save to avoid mutation issues
            _originalJson = json;

            // Detect envelope and remember the exact key casing
            var dataProp = jobj.Properties()
                .FirstOrDefault(p => p.Name.Equals("data", StringComparison.OrdinalIgnoreCase));
            _hasDataWrapper = dataProp != null;
            _dataKey        = dataProp?.Name ?? "data";

            var nodes = JsonToNodeTree.Convert(jobj);

            _rootNodes.Clear();
            SelectedNode = null;
            foreach (var n in nodes)
                _rootNodes.Add(new NodeViewModel(n, 0, OnChildrenChanged));

            FilePath = path;
            IsDirty  = false;
            RebuildVisibleList();
            StatusMessage = $"Loaded  {_rootNodes.Count} properties.";
            Log($"Opened {Path.GetFileName(path)}");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            Log($"ERROR opening file: {ex.Message}");
        }
    }

    public void SaveToPath(string path)
    {
        try
        {
            foreach (var n in _rootNodes) n.CommitToNode();

            // Build the serialized data section from current node tree
            var dataObj = NodeTreeToJson.Convert(_rootNodes.Select(v => v.BackingNode));

            JObject outputRoot;
            if (_hasDataWrapper && _originalJson != null)
            {
                // Re-parse original from the stored raw string — guaranteed unmodified
                outputRoot = JObject.Parse(_originalJson);
                // Replace only the data section, leave metadata and any other keys alone
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

    public void MarkDirty() => IsDirty = true;

    private void OnChildrenChanged()
    {
        var previousSelection = SelectedNode;
        RebuildVisibleList();
        SelectedNode = previousSelection;
    }

    private void RebuildVisibleList()
    {
        VisibleNodes.Clear();
        foreach (var root in _rootNodes)
            AppendVisible(root);
    }

    private void AppendVisible(NodeViewModel node)
    {
        VisibleNodes.Add(node);
        if (node.IsExpanded)
            foreach (var child in node.Children)
                AppendVisible(child);
    }

    public void Log(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        LogEntries.Add($"[{timestamp}]  {message}");
    }

    public void ClearLog() => LogEntries.Clear();

    public void RunDeserialize(string binPath)
    {
        FilePaths.ValidateOutputDirectory();
        try
        {   
            Log($"Deserializing: {Path.GetFileName(binPath)}");
            StatusMessage = "Deserializing...";

            using var upk = new UnrealPackage(binPath);
            Log("Package loaded.");
            var properties = upk.DeserializeUPK();
            if (properties == null || properties.Count == 0)
                throw new InvalidOperationException("Deserialization produced no properties.");
            Log($"Deserialized {properties.Count} properties.");

            using var parser = new JsonDataParser(properties, upk.info);
            parser.WriteDataToFile();

            StatusMessage = "Deserialized. JSON written to OUTPUT.";
            Log("JSON written to OUTPUT. Done.");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Deserialize failed: {ex.Message}";
            Log($"ERROR: {ex.Message}");
        }
    }

    public void RunSerialize(string jsonPath)
    {
        FilePaths.ValidateOutputDirectory();
        try
        {
            Log($"Serializing: {Path.GetFileName(jsonPath)}");
            StatusMessage = "Serializing...";

            EnvelopeMeta meta = JsonUtils.ReadMeta(jsonPath);
            var info = new PackageInfo();
            info.SetPackageName(meta.PackageName);
            info.SetGame(meta.Game);
            info.SetIsEncrypted(meta.IsEncrypted);
            info.SetSaveVersion(meta.SaveVersion);
            info.SetSaveMagic(meta.SaveMagic);
            Log($"Metadata loaded: {meta.PackageName} / {meta.Game}");

            string dataJson  = JsonUtils.ExtractDataObjectJson(jsonPath, "data");
            var cruncher     = new JsonDataCruncher(dataJson, info.game);
            var crunchedData = cruncher.ReadJsonFile();
            if (crunchedData == null)
                throw new InvalidOperationException("JSON could not be parsed into save data.");
            Log("JSON crunched successfully.");

            using var serializer = new Serializer(info, crunchedData);
            serializer.SerializeAndOutputData();

            StatusMessage = "Serialized. .bin written to OUTPUT.";
            Log(".bin written to OUTPUT. Done.");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Serialize failed: {ex.Message}";
            Log($"ERROR: {ex.Message}");
        }
    }

    // Stubs
    private Task OpenFileAsync()  => Task.CompletedTask;
    private Task SaveFileAsync()  { if (FilePath != null) SaveToPath(FilePath); return Task.CompletedTask; }
    private Task ReloadAsync()    { if (FilePath != null) LoadFromPath(FilePath); return Task.CompletedTask; }
}
