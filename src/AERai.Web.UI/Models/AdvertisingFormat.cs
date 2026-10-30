using System.Globalization;

namespace AERai.Web.UI.Models;

/// <summary>How the Advertising page writes its ratios. Formatting only; the figures come from <c>AdMetrics</c>.</summary>
public static class AdvertisingFormat
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-US");

    /// <summary>A ratio as a percentage with one decimal, or a dash when it can't be worked out.</summary>
    /// <param name="ratio">The ratio (0.123 = 12.3%), or <see langword="null"/>.</param>
    /// <returns>e.g. "12.3%" or "—".</returns>
    public static string Percent(decimal? ratio) => ratio is { } r ? r.ToString("0.0%", Culture) : "—";

    /// <summary>A count with thousands separators.</summary>
    /// <param name="count">Count.</param>
    /// <returns>e.g. "12,345".</returns>
    public static string Count(long count) => count.ToString("N0", Culture);

    /// <summary>
    /// The pill class for an advertising cost of sales: at or under 25% is healthy, up to 50% worth watching,
    /// above that the ads cost more than they bring in at most margins.
    /// </summary>
    /// <param name="acos">ACoS, or <see langword="null"/>.</param>
    /// <returns>CSS classes, or empty when there is no ratio.</returns>
    public static string AcosClass(decimal? acos) => acos switch
    {
        null => string.Empty,
        <= 0.25m => "text-positive",
        <= 0.50m => "text-warn",
        _ => "amount-neg",
    };

    /// <summary>The words for <see cref="AcosClass"/>, so a costly ACoS isn't told by color alone.</summary>
    /// <param name="acos">ACoS as a fraction, or <see langword="null"/>.</param>
    /// <returns>"high" over 25%, "very high" over 50%, else <see langword="null"/>.</returns>
    public static string? AcosLabel(decimal? acos) => acos switch
    {
        null or <= 0.25m => null,
        <= 0.50m => "high",
        _ => "very high",
    };
}
