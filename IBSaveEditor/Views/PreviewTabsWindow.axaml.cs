using Avalonia.Controls;
using IBSaveEditor.ViewModels.Fields;

namespace IBSaveEditor.Views;

public partial class PreviewTabsWindow : Window
{
    public PreviewTabsWindow()
    {
        InitializeComponent();
    }

    public PreviewTabsWindow(ManifestPreviewViewModel vm) : this()
    {
        DataContext = vm;
        Title = $"{vm.Game} – Manifest Preview";
    }
}
