using System.Globalization;
using System.Windows.Data;

namespace AERai.Seller.Desktop.Converters;

/// <summary>Bridges the ViewModel's DateOnly to the WPF-UI CalendarDatePicker's DateTime? Date property.</summary>
public sealed class DateOnlyToDateTimeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is DateOnly date ? date.ToDateTime(TimeOnly.MinValue) : null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is DateTime dateTime ? DateOnly.FromDateTime(dateTime) : DateOnly.FromDateTime(DateTime.UtcNow);
}
