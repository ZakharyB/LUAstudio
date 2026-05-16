using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace LUAstudio.Converters;

public sealed class StringNullOrEmptyToVisibilityConverter : IValueConverter
{
    public static StringNullOrEmptyToVisibilityConverter Instance { get; } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is string s && !string.IsNullOrEmpty(s) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
