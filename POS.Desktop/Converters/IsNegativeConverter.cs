using System.Globalization;
using System.Windows.Data;

namespace POS.Desktop.Converters;

/// <summary>Convierte un número en true si es menor que cero (stock negativo).</summary>
public class IsNegativeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value switch
        {
            decimal d => d < 0,
            int i => i < 0,
            double db => db < 0,
            float f => f < 0,
            _ => false
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}