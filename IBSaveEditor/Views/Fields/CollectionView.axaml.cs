using Avalonia.Controls;
using Avalonia.Interactivity;
using IBSaveEditor.ViewModels.Fields;

namespace IBSaveEditor.Views.Fields;

public partial class CollectionView : UserControl
{
    public CollectionView() => InitializeComponent();

    private void OnOpenAddPickerClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is CollectionViewModel vm) vm.OpenAddPicker();
    }

    private void OnCancelAddPickerClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is CollectionViewModel vm) vm.CancelAddPicker();
    }

    private void OnAddableCandidateClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: CollectionAddableRowViewModel candidate }) return;
        if (DataContext is CollectionViewModel vm) vm.ActivateRow(candidate);
    }

    private void OnAddRowClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is CollectionViewModel vm) vm.AddRow();
    }

    private void OnRemoveRowClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: CollectionRowViewModel row }) return;
        if (DataContext is not CollectionViewModel vm) return;

        if (vm.SupportsOwnedToggle) vm.DeactivateRow(row);
        else if (vm.SupportsStructuralAddRemove) vm.RemoveRow(row);
    }
}
