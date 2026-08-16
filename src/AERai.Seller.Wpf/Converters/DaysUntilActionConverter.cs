using System.Globalization;
using System.Windows.Data;

namespace AERai.Seller.Desktop.Converters;

/// <summary>Formats DaysUntilActionNeeded, showing "—" for int.MaxValue (ReplenishmentPlanningService's insufficient-data sentinel) instead of a meaningless huge number.</summary>
public sealed class DaysUntilActionConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int days && days != int.MaxValue ? days.ToString(culture) : "—";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
