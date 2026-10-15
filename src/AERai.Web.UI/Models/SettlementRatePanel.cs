using AERai.Web.Application.Settlements;
using AERai.Web.Domain.Reporting;

namespace AERai.Web.UI.Models;

/// <summary>
/// What the settlement pages' conversion banner shows for a settlement paid in another currency: the
/// conversion, the currency switch, and (on the summary, for Operators) the form that adjusts the rate.
/// </summary>
/// <param name="Settlement">The settlement.</param>
/// <param name="Amounts">Which currency the page shows.</param>
/// <param name="HomeLink">This page in the marketplace's currency.</param>
/// <param name="OriginalLink">This page in the currency it was paid in.</param>
/// <param name="Rate">Its stored rate details, when the page shows them (the summary); otherwise <see langword="null"/>.</param>
/// <param name="CanEdit">Whether to show the adjust form.</param>
public sealed record SettlementRatePanel(
    SettlementSummary Settlement,
    SettlementAmounts Amounts,
    string HomeLink,
    string OriginalLink,
    SettlementRateContext? Rate = null,
    bool CanEdit = false)
{
    /// <summary>Whether the settlement was paid in another currency (otherwise there is no banner).</summary>
    public bool IsForeign => Settlement.Currency is not null && !string.Equals(Settlement.Currency, Settlement.HomeCurrency, StringComparison.OrdinalIgnoreCase);
}
