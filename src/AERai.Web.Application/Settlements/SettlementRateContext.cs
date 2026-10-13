using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Settlements;

/// <summary>What deciding on a settlement's exchange rate needs to know about it.</summary>
/// <param name="SettlementId">The settlement.</param>
/// <param name="Currency">Currency it was paid in, or <see langword="null"/> when the file had none.</param>
/// <param name="HomeCurrency">Its marketplace's currency.</param>
/// <param name="Net">The payout in <paramref name="Currency"/> (the sum of every line).</param>
/// <param name="RateDate">The day its rate is taken for, or <see langword="null"/> without a deposit date or period end.</param>
/// <param name="Rate">Its stored rate, or <see langword="null"/> when none was fetched or set.</param>
/// <param name="IsExported">Whether a QuickBooks file was already generated for it.</param>
public sealed record SettlementRateContext(
    string SettlementId,
    string? Currency,
    string HomeCurrency,
    decimal Net,
    DateOnly? RateDate,
    SettlementExchangeRate? Rate,
    bool IsExported)
{
    /// <summary>Whether the settlement is paid in another currency than its marketplace's, so it needs a rate.</summary>
    public bool NeedsConversion => Currency is not null && !string.Equals(Currency, HomeCurrency, StringComparison.OrdinalIgnoreCase);
}
