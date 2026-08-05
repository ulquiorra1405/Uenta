using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace POS.Desktop.Converters;

/// <summary>Convierte string null/vacío en Visible, cualquier otro valor en Collapsed.</summary>
public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
