namespace AERai.Web.Domain.Core;

/// <summary>
/// The exchange rate that converts a settlement paid in another currency into its marketplace's
/// currency (e.g. a CAD or MXN payout listed under the United States, into USD).
/// </summary>
/// <remarks>
/// The market rate comes from a daily rates feed for the settlement's deposit date. An Operator can
/// override it to match the books exactly, either with a rate or with the amount that actually arrived
/// in the bank (<see cref="ManualHomeAmount"/>), which then becomes the converted payout as entered.
/// Settlements already in their marketplace's currency have no row: their rate is 1. Written only by
/// the exchange-rate service and the Operator's adjustment, never by promotion.
/// </remarks>
public sealed class SettlementExchangeRate
{
    /// <summary>The settlement (key).</summary>
    public required string SettlementId { get; set; }

    /// <summary>Currency the settlement was paid in (ISO 4217).</summary>
    public required string FromCurrency { get; set; }

    /// <summary>Currency it is converted into: its marketplace's currency.</summary>
    public required string ToCurrency { get; set; }

    /// <summary>Rate from the feed (one <see cref="FromCurrency"/> in <see cref="ToCurrency"/>), or <see langword="null"/> when the feed had none.</summary>
    public decimal? MarketRate { get; set; }

    /// <summary>The date the feed's rate is for (the business day on or before the deposit date).</summary>
    public DateOnly? MarketRateDate { get; set; }

    /// <summary>Where the market rate came from, e.g. <c>ECB</c>.</summary>
    public string? MarketSource { get; set; }

    /// <summary>When the market rate was fetched.</summary>
    public DateTimeOffset? MarketFetchedAt { get; set; }

    /// <summary>Operator's rate, which replaces <see cref="MarketRate"/> when set.</summary>
    public decimal? ManualRate { get; set; }

    /// <summary>The converted payout the Operator entered (what reached the bank), when the override was given as an amount.</summary>
    public decimal? ManualHomeAmount { get; set; }

    /// <summary>Email of the user who last set the override.</summary>
    public string? ManualBy { get; set; }

    /// <summary>When the override was last set.</summary>
    public DateTimeOffset? ManualAt { get; set; }

    /// <summary>The rate in effect: the override when set, else the market rate.</summary>
    public decimal? EffectiveRate => ManualRate ?? MarketRate;
}
