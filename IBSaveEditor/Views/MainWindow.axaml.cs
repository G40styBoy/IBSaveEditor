using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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

    private async void OnOpenClick(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open JSON Save",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("JSON Save Files") { Patterns = new[] { "*.json" } },
                new FilePickerFileType("All Files")       { Patterns = new[] { "*" } }
            }
        });
        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (path != null) ViewModel?.LoadFromPath(path);
    }

    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel?.FilePath != null)
        {
            ViewModel.SaveToPath(ViewModel.FilePath);
            return;
        }
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save JSON",
            SuggestedFileName = "save.json",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("JSON Files") { Patterns = new[] { "*.json" } }
            }
        });
        var path = file?.TryGetLocalPath();
        if (path != null) ViewModel?.SaveToPath(path);
    }

    private async void OnDeserializeClick(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select .bin save to deserialize",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Binary Save Files") { Patterns = new[] { "*.bin" } },
                new FilePickerFileType("All Files")         { Patterns = new[] { "*" } }
            }
        });
        var binPath = files.FirstOrDefault()?.TryGetLocalPath();
        if (binPath != null) ViewModel?.RunDeserialize(binPath);
    }

    private async void OnSerializeClick(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select .json save to serialize",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("JSON Save Files") { Patterns = new[] { "*.json" } },
                new FilePickerFileType("All Files")       { Patterns = new[] { "*" } }
            }
        });
        var jsonPath = files.FirstOrDefault()?.TryGetLocalPath();
        if (jsonPath != null) ViewModel?.RunSerialize(jsonPath);
    }

    private void OnClearLogClick(object? sender, RoutedEventArgs e)
        => ViewModel?.ClearLog();

    private async void OnAboutClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new AboutDialog();
        await dialog.ShowDialog(this);
    }
}
