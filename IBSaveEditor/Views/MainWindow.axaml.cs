using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.ReactiveUI;
using Avalonia.VisualTree;
using IBSaveEditor.ViewModels;

namespace IBSaveEditor.Views;

public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        var listBox = this.FindControl<ListBox>("NodeListBox");
        if (listBox != null)
            listBox.AddHandler(TappedEvent, OnNodeTapped, handledEventsToo: true);

        // Auto-scroll log when entries are added
        if (ViewModel != null)
            ViewModel.LogEntries.CollectionChanged += (_, _) =>
            {
                var scroller = this.FindControl<ScrollViewer>("LogScroller");
                scroller?.ScrollToEnd();
            };
    }

    private void OnNodeTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is not Control c) return;
        bool tappedGlyph = c.FindAncestorOfType<TextBlock>()?.Name == "ExpanderGlyph"
                        || (c is TextBlock tb && tb.Name == "ExpanderGlyph");
        var item = c.FindAncestorOfType<ListBoxItem>();
        if (item?.DataContext is not NodeViewModel vm) return;
        if (tappedGlyph && vm.HasChildren)
        {
            ViewModel?.ToggleExpand(vm);
            e.Handled = true;
        }
    }

    /// <summary>
    /// // Open .bin or .json file
    /// </summary>
    private async void OnOpenClick(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Save File",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Save Files") { Patterns = new[] { "*.bin", "*.json" } },
                new FilePickerFileType("Binary Save") { Patterns = new[] { "*.bin" } },
                new FilePickerFileType("JSON Save")   { Patterns = new[] { "*.json" } },
                new FilePickerFileType("All Files")   { Patterns = new[] { "*" } }
            }
        });

        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (path == null) return;

        if (Path.GetExtension(path).Equals(".bin", StringComparison.OrdinalIgnoreCase))
            ViewModel?.LoadFromBin(path);
        else
            ViewModel?.LoadFromPath(path);
    }

    /// <summary>
    /// Save JSON state
    /// </summary>
    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel?.FilePath != null &&
            !Path.GetExtension(ViewModel.FilePath).Equals(".bin", StringComparison.OrdinalIgnoreCase))
        {
            // Already have a .json path : save in place
            ViewModel.SaveToPath(ViewModel.FilePath);
            return;
        }

        // Opened from .bin or no file : prompt for a .json save location
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save JSON",
            SuggestedFileName = Path.GetFileNameWithoutExtension(ViewModel?.FilePath ?? "save") + ".json",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("JSON Files") { Patterns = new[] { "*.json" } }
            }
        });
        var path = file?.TryGetLocalPath();
        if (path != null) ViewModel?.SaveToPath(path);
    }

    private void OnExportClick(object? sender, RoutedEventArgs e)
        => ViewModel?.ExportToBin();

    private void OnClearLogClick(object? sender, RoutedEventArgs e)
        => ViewModel?.ClearLog();

    private async void OnAboutClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new AboutDialog();
        await dialog.ShowDialog(this);
    }
}
