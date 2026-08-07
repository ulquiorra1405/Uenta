using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace POS.Desktop.Converters;

/// <summary>true → Collapsed, false → Visible (inverso de BooleanToVisibilityConverter).</summary>
public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is Visibility.Collapsed;
}

/// <summary>
/// Compara el valor con el parámetro (enum o string) → bool. TwoWay: si el binding
/// devuelve true, escribe el parámetro como valor (útil para ToggleButton ↔ enum).
/// </summary>
public class EnumEqualsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null || parameter is null) return false;
        var param = parameter.ToString();
        return value.ToString() == param;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is true && parameter is not null)
        {
            var text = parameter.ToString();
            return Enum.Parse(targetType, text!);
        }
        return Binding.DoNothing;
    }
}
