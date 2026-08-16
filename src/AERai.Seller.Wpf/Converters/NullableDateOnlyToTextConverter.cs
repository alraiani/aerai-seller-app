using System.Globalization;
using System.Windows.Data;

namespace AERai.Seller.Desktop.Converters;

/// <summary>Formats a nullable DateOnly, showing "Insufficient data" when null (the sentinel ReplenishmentQueryService uses for SKUs without enough sales history).</summary>
public sealed class NullableDateOnlyToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is DateOnly date ? date.ToString("d", culture) : "Insufficient data";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
