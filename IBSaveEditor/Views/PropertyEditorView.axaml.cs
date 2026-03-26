using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using IBSaveEditor.ViewModels;

namespace IBSaveEditor.Views;

public partial class PropertyEditorView : UserControl
{
    private NodeViewModel? _current;

    public PropertyEditorView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        _current = DataContext as NodeViewModel;

        if (ArrayTypePickerPanel  != null) ArrayTypePickerPanel.IsVisible  = false;
        if (StructTypePickerPanel != null) StructTypePickerPanel.IsVisible = false;

        PopulateFields();
    }

    // ── Populate all inputs from the VM without any two-way binding ───────────
    private void PopulateFields()
    {
        var vm = _current;
        if (vm == null) return;

        if (vm.IsPrimitive)
        {
            try { IntBox.Text   = vm.TypeHint == "int"   ? Convert.ToInt64(vm.EditValue).ToString() : string.Empty; } catch { IntBox.Text   = string.Empty; }
            try { ByteBox.Text  = vm.TypeHint == "byte"  ? Convert.ToInt64(vm.EditValue).ToString() : string.Empty; } catch { ByteBox.Text  = string.Empty; }
            try { FloatBox.Text = vm.TypeHint == "float" ? Convert.ToDouble(vm.EditValue).ToString("G") : string.Empty; } catch { FloatBox.Text = string.Empty; }
            StringBox.Text = vm.TypeHint == "string" ? vm.EditValue?.ToString() ?? string.Empty : string.Empty;
            NameBox.Text   = vm.TypeHint == "name"   ? vm.EditValue?.ToString() ?? string.Empty : string.Empty;
            BoolToggle.IsChecked = vm.TypeHint == "bool" && vm.EditValue is true;
        }
        else if (vm.IsEnum)
        {
            EnumTypeBox.Text  = vm.EnumType;
            EnumValueBox.Text = vm.EnumValue;
        }
    }

    // ── Write back on LostFocus ───────────────────────────────────────────────
    private void OnIntBoxLostFocus(object? sender, RoutedEventArgs e)
    {
        if (_current == null) return;
        if (long.TryParse(IntBox.Text, out var v))
            _current.EditValue = _current.TypeHint == "byte" ? (object)(long)Math.Clamp(v, 0, 255) : v;
    }

    private void OnFloatBoxLostFocus(object? sender, RoutedEventArgs e)
    {
        if (_current == null) return;
        if (double.TryParse(FloatBox.Text,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var v))
            _current.EditValue = v;
    }

    private void OnStringBoxLostFocus(object? sender, RoutedEventArgs e)
    {
        if (_current != null && sender is TextBox tb)
            _current.EditValue = tb.Text ?? string.Empty;
    }

    private void OnBoolToggleChanged(object? sender, RoutedEventArgs e)
    {
        if (_current != null)
            _current.EditValue = BoolToggle.IsChecked ?? false;
    }

    private void OnEnumTypeLostFocus(object? sender, RoutedEventArgs e)
    {
        if (_current != null) _current.EnumType = EnumTypeBox.Text ?? string.Empty;
    }

    private void OnEnumValueLostFocus(object? sender, RoutedEventArgs e)
    {
        if (_current != null) _current.EnumValue = EnumValueBox.Text ?? string.Empty;
    }

    private void OnNumericKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && sender is TextBox tb)
            TopLevel.GetTopLevel(tb)?.FocusManager?.ClearFocus();
    }

    private void OnAddItemClick(object? sender, RoutedEventArgs e)
    {
        if (_current == null) return;
        if (_current.Children.Count > 0)
            _current.AddItem();
        else
            ArrayTypePickerPanel.IsVisible = true;
    }

    private void OnArrayTypeChosen(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string typeHint && _current != null)
        {
            _current.AddItem(typeHint);
            ArrayTypePickerPanel.IsVisible = false;
        }
    }

    private void OnRemoveItemClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is NodeViewModel item)
            _current?.RemoveArrayItem(item);
    }

    private void OnAddMemberClick(object? sender, RoutedEventArgs e)
    {
        StructTypePickerPanel.IsVisible = true;
        StructMemberNameBox.Text = string.Empty;
        StructMemberNameBox.Focus();
    }

    private void OnStructTypeChosen(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string typeHint || _current == null) return;
        var name = StructMemberNameBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(name)) return;
        _current.AddMember(name, typeHint);
        StructTypePickerPanel.IsVisible = false;
        StructMemberNameBox.Text = string.Empty;
    }

    private void OnRemoveMemberClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is NodeViewModel member)
            _current?.RemoveStructMember(member);
    }

    private void OnDuplicateMemberClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is NodeViewModel member)
            _current?.DuplicateStructMember(member);
    }

    private void OnDuplicateItemClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is NodeViewModel item)
            _current?.DuplicateArrayItem(item);
    }
}
