using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace POS.Desktop.Converters;

/// <summary>Visible cuando la colección tiene elementos; Collapsed si está vacía.
/// Acepta ICollection o un int (Count), como `RefundLines.Count`.</summary>
public class CollectionNotEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var count = value switch
        {
            ICollection c => c.Count,
            int i => i,
            _ => 0
        };
        return count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Visible cuando la colección está vacía; Collapsed si tiene elementos.
/// Acepta ICollection o un int (Count), como `RefundLines.Count`.</summary>
public class CollectionEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var count = value switch
        {
            ICollection c => c.Count,
            int i => i,
            _ => 0
        };
        return count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}