using System.Globalization;

namespace AERai.Web.Application.Products;

/// <summary>
/// Reads a cost typed on the page or in a sheet. Accepts plain numbers in US format with an optional
/// currency symbol or code ("4.5", "$1,204.50", "CA$ 3", "£2.10"), because people paste costs from
/// other sheets.
/// </summary>
public static class ProductCostText
{
    private static readonly string[] Prefixes = ["CA$", "C$", "MX$", "US$", "USD", "CAD", "GBP", "$", "£"];

    /// <summary>Parses a cost.</summary>
    /// <param name="text">The text; blank means no cost.</param>
    /// <param name="cost">The cost, rounded to cents, or <see langword="null"/> when blank.</param>
    /// <returns><see langword="false"/> when the text is not blank and not a number from 0 to <see cref="ProductService.MaxCostOfGoods"/>.</returns>
    public static bool TryParse(string? text, out decimal? cost)
    {
        cost = null;
        var value = text?.Trim() ?? string.Empty;
        if (value.Length == 0)
        {
            return true;
        }

        foreach (var prefix in Prefixes)
        {
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                value = value[prefix.Length..].TrimStart();
                break;
            }
        }

        if (!decimal.TryParse(value, NumberStyles.AllowThousands | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsed)
            || parsed > ProductService.MaxCostOfGoods)
        {
            return false;
        }

        cost = Round(parsed);
        return true;
    }

    /// <summary>Rounds a cost to cents the way it is stored, so the saved value matches what the user sees.</summary>
    /// <param name="cost">Cost.</param>
    /// <returns>The cost rounded half away from zero to two decimals.</returns>
    public static decimal Round(decimal cost) => Math.Round(cost, 2, MidpointRounding.AwayFromZero);

    /// <summary>Writes a cost the way <see cref="TryParse"/> reads it back ("4.50"; empty when not set).</summary>
    /// <param name="cost">Cost, or <see langword="null"/>.</param>
    /// <returns>Invariant text with two decimals and no grouping.</returns>
    public static string Format(decimal? cost) => cost?.ToString("0.00", CultureInfo.InvariantCulture) ?? string.Empty;
}
