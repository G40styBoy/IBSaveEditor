using Avalonia.Controls;
using Avalonia.Interactivity;
using IBSaveEditor.ViewModels.Fields;

namespace IBSaveEditor.Views.Fields;

public partial class FieldRowView : UserControl
{
    public FieldRowView() => InitializeComponent();

    private FieldViewModel? Vm => DataContext as FieldViewModel;

    private void OnOpenCatalogPicker(object? sender, RoutedEventArgs e) => Vm?.OpenCatalogPicker();

    private void OnCancelCatalogPicker(object? sender, RoutedEventArgs e) => Vm?.CancelCatalogPicker();

    private void OnCatalogPickerCandidateClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: CatalogPickerItem item })
            Vm?.SelectCatalogPickerCandidate(item);
    }
}
