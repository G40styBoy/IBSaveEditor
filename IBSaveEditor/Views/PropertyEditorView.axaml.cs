using System;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using IBSaveEditor.ViewModels;

namespace IBSaveEditor.Views;

public partial class PropertyEditorView : UserControl
{
    private const long INT_MAX  =  2_147_483_647L;
    private const long INT_MIN  = -2_147_483_648L;
    private const long BYTE_MAX = 255L;
    private const long BYTE_MIN = 0L;

    // Max box width before we stop expanding — slightly less than the right panel width
    private const int BOX_MAX_WIDTH = 480;

    private static readonly Regex NonNumericRegex = new(@"[^0-9\-]");
    private static readonly Regex NonDecimalRegex = new(@"[^0-9\-\.]");

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

    /// <summary>
    /// Populate inputs from view model
    /// </summary>
    private void PopulateFields()
    {
        var vm = _current;
        if (vm == null) return;

        if (vm.IsPrimitive)
        {
            try { IntBox.Text   = vm.TypeHint == "int"   ? Convert.ToInt64(vm.EditValue).ToString()  : string.Empty; } catch { IntBox.Text   = string.Empty; }
            try { ByteBox.Text  = vm.TypeHint == "byte"  ? Convert.ToInt64(vm.EditValue).ToString()  : string.Empty; } catch { ByteBox.Text  = string.Empty; }
            try { FloatBox.Text = vm.TypeHint == "float" ? Convert.ToDouble(vm.EditValue).ToString("G") : string.Empty; } catch { FloatBox.Text = string.Empty; }

            var strVal = vm.EditValue?.ToString() ?? string.Empty;
            StringBox.Text = vm.TypeHint == "string" ? strVal : string.Empty;
            NameBox.Text   = vm.TypeHint == "name"   ? strVal : string.Empty;
            BoolToggle.IsChecked = vm.TypeHint == "bool" && vm.EditValue is true;

            // Size numeric boxes to match initial content
            ResizeNumericBox(IntBox,   IntBox.Text);
            ResizeNumericBox(ByteBox,  ByteBox.Text);
            ResizeNumericBox(FloatBox, FloatBox.Text);

            // Size string boxes to match initial content
            ResizeStringBox(StringBox, StringBox.Text);
            ResizeStringBox(NameBox,   NameBox.Text);

            // Show fname label if string starts with _ini
            UpdateFNameLabel();
        }
        else if (vm.IsEnum)
        {
            EnumTypeBox.Text  = vm.EnumType;
            EnumValueBox.Text = vm.EnumValue;
            ResizeStringBox(EnumTypeBox,  EnumTypeBox.Text);
            ResizeStringBox(EnumValueBox, EnumValueBox.Text);
        }
    }

    /// <summary>
    /// FName detection
    /// </summary>
    private void UpdateFNameLabel()
    {
        if (FNameLabel == null || _current == null) return;
        FNameLabel.IsVisible = _current.IsIni;
    }

    private void OnCopyValueClick(object? sender, RoutedEventArgs e)
    {
        if (_current == null) return;
        var text = _current.EditValue?.ToString() ?? string.Empty;
        if (_current.IsEnum) text = _current.EnumValue;
        TopLevel.GetTopLevel(this)?.Clipboard?.SetTextAsync(text);
    }

    /// <summary>
    /// Numeric text changed: strip invalid chars + auto-size
    /// </summary>
    private void OnNumericTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox tb) return;

        bool isFloat = ReferenceEquals(tb, FloatBox);
        var regex    = isFloat ? NonDecimalRegex : NonNumericRegex;

        var text  = tb.Text ?? string.Empty;
        var clean = regex.Replace(text, string.Empty);

        // Allow a single leading minus only
        if (clean.StartsWith("-"))
            clean = "-" + clean.Substring(1).Replace("-", string.Empty);
        else
            clean = clean.Replace("-", string.Empty);

        if (clean != text)
        {
            var caret = tb.CaretIndex;
            tb.Text = clean;
            tb.CaretIndex = Math.Clamp(caret, 0, clean.Length);
            
            // TextChanged will fire again with clean value
            return; 
        }

        ResizeNumericBox(tb, clean);
    }

    /// <summary>
    /// String text changed: auto-size + fname detection
    /// </summary>
    private void OnStringTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox tb) return;
        var text = tb.Text ?? string.Empty;
        ResizeStringBox(tb, text);
        UpdateFNameLabel();
    }

    /// <summary>Sizes a numeric box to fit its content. ~9px per char at FontSize 12.</summary>
    private static void ResizeNumericBox(TextBox tb, string text)
    {
        var width = Math.Max(text.Length, 1) * 9 + 24;
        tb.Width  = Math.Clamp(width, 60, BOX_MAX_WIDTH);
    }

    /// <summary>Sizes a string box to fit its content, capped at panel width.</summary>
    private static void ResizeStringBox(TextBox tb, string text)
    {
        var width = Math.Max(text.Length, 1) * 9 + 24;
        tb.Width  = Math.Clamp(width, 60, BOX_MAX_WIDTH);
        // Prevent further input once the box is at max width
        tb.MaxLength = width >= BOX_MAX_WIDTH ? text.Length : 0; // 0 = unlimited
    }

    /// <summary>
    /// Write back on LostFocus
    /// </summary>
    private void OnIntBoxLostFocus(object? sender, RoutedEventArgs e)
    {
        if (_current == null) return;
        if (!long.TryParse(IntBox.Text, out var v)) return;
        v = Math.Clamp(v, INT_MIN, INT_MAX);
        IntBox.Text = v.ToString();
        _current.EditValue = v;
    }

    private void OnByteBoxLostFocus(object? sender, RoutedEventArgs e)
    {
        if (_current == null) return;
        if (!long.TryParse(ByteBox.Text, out var v)) return;
        v = Math.Clamp(v, BYTE_MIN, BYTE_MAX);
        ByteBox.Text = v.ToString();
        _current.EditValue = v;
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

#region Array
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

    private void OnCancelArrayAdd(object? sender, RoutedEventArgs e)
        => ArrayTypePickerPanel.IsVisible = false;

    private void OnRemoveItemClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is NodeViewModel item)
            _current?.RemoveArrayItem(item);
    }

    private void OnDuplicateItemClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is NodeViewModel item)
            _current?.DuplicateArrayItem(item);
    }
#endregion

#region Struct
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

    private void OnCancelStructAdd(object? sender, RoutedEventArgs e)
    {
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
#endregion
}