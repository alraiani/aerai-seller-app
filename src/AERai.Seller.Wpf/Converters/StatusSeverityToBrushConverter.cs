using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using AERai.Seller.Presentation.Messaging;

namespace AERai.Seller.Desktop.Converters;

public sealed class StatusSeverityToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value switch
        {
            StatusSeverity.Success => Brushes.SeaGreen,
            StatusSeverity.Warning => Brushes.DarkOrange,
            StatusSeverity.Error => Brushes.IndianRed,
            _ => Brushes.Gray,
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
