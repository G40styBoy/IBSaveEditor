using Avalonia.Controls;
using IBSaveEditor.ViewModels.Fields;

namespace IBSaveEditor.Views;

public partial class PreviewTabsWindow : Window
{
    public PreviewTabsWindow()
    {
        InitializeComponent();
    }

    public PreviewTabsWindow(ManifestTabsViewModel vm) : this()
    {
        DataContext = vm;
        Title = $"{vm.Game} – Manifest Preview";
    }
}
