using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace IBSaveEditor.ViewModels;

public class TypeHintEqualityConverter : IValueConverter
{
    private readonly string _target;
    public TypeHintEqualityConverter(string target) => _target = target;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string s && s.Equals(_target, StringComparison.OrdinalIgnoreCase);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Converts any numeric EditValue (long, int, double, float, decimal…) to a
/// display string, and converts the edited string back to the correct CLR type.
/// This avoids all the decimal?/null issues with NumericUpDown.
/// </summary>
public class NumericToStringConverter : IValueConverter
{
    public static readonly NumericToStringConverter Int   = new("int");
    public static readonly NumericToStringConverter Float = new("float");
    public static readonly NumericToStringConverter Byte  = new("byte");

    private readonly string _mode;
    private NumericToStringConverter(string mode) => _mode = mode;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null) return string.Empty;
        try
        {
            return _mode == "float"
                ? System.Convert.ToDouble(value).ToString("G", CultureInfo.InvariantCulture)
                : System.Convert.ToInt64(value).ToString(CultureInfo.InvariantCulture);
        }
        catch { return string.Empty; }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var s = value?.ToString() ?? string.Empty;
        return _mode switch
        {
            "float" => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
                           ? (object)d : null,
            "byte"  => byte.TryParse(s, out var b)   ? (object)b : null,
            _       => long.TryParse(s, out var l)    ? (object)l : null
        };
    }
}

public class ObjectToBoolConverter : IValueConverter
{
    public static readonly ObjectToBoolConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b) return b;
        if (value is string s && bool.TryParse(s, out var parsed)) return parsed;
        try { return System.Convert.ToBoolean(value); }
        catch { return null; }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? b : (object?)null;
}

public static class TypeHintConverters
{
    public static readonly TypeHintEqualityConverter IsInt    = new("int");
    public static readonly TypeHintEqualityConverter IsFloat  = new("float");
    public static readonly TypeHintEqualityConverter IsBool   = new("bool");
    public static readonly TypeHintEqualityConverter IsString = new("string");
    public static readonly TypeHintEqualityConverter IsByte   = new("byte");
    public static readonly TypeHintEqualityConverter IsName   = new("name");
}
