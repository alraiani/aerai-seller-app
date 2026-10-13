namespace AERai.Web.Application.Settlements;

/// <summary>
/// Which country's marketplace a settlement came from, told by the currency it was paid in. Amazon lists
/// every North America payout with the United States, so the USD, CAD, and MXN settlements share one
/// list; this tag tells them apart. Display only.
/// </summary>
public static class SettlementOrigin
{
    private static readonly Dictionary<string, string> ByCurrency = new(StringComparer.OrdinalIgnoreCase)
    {
        ["USD"] = "US",
        ["CAD"] = "CA",
        ["MXN"] = "MX",
        ["GBP"] = "UK",
    };

    /// <summary>The marketplace code for a payout currency.</summary>
    /// <param name="currency">ISO 4217 currency the settlement was paid in.</param>
    /// <returns>e.g. <c>CA</c> for CAD; the currency code itself when it is not one of ours; empty without a currency.</returns>
    public static string FromCurrency(string? currency) =>
        string.IsNullOrWhiteSpace(currency) ? string.Empty
        : ByCurrency.TryGetValue(currency.Trim(), out var code) ? code
        : currency.Trim().ToUpperInvariant();
}
