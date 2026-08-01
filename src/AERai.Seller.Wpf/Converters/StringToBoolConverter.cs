using System.Globalization;
using System.Windows.Data;

namespace AERai.Seller.Desktop.Converters;

/// <summary>True when the bound string is non-null/non-empty — used to drive InfoBar.IsOpen from an error message.</summary>
public sealed class StringToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => !string.IsNullOrEmpty(value as string);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
