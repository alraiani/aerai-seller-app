using System.Globalization;
using AERai.Web.Application.Settlements;
using AERai.Web.Domain.Reporting;

namespace AERai.Web.UI.Models;

/// <summary>
/// Presentation helpers for the Settlements pages: money in the marketplace's currency, signed-amount
/// classes, bucket pills, and the labels of their filters and sorts. Formatting only.
/// </summary>
public static class SettlementFormat
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-US");

    /// <summary>Bucket filter choices for the Lines page, "All" first.</summary>
    public static IReadOnlyList<(SettlementBucket? Bucket, string Label)> BucketFilters { get; } =
    [
        (null, "All amounts"),
        (SettlementBucket.Sales, "With sales"),
        (SettlementBucket.Fees, "With fees"),
        (SettlementBucket.Refunds, "With refunds"),
        (SettlementBucket.Other, "With other"),
    ];

    /// <summary>Sort choices for the Lines page.</summary>
    public static IReadOnlyList<(SettlementGroupSort Sort, string Label)> GroupSorts { get; } =
    [
        (SettlementGroupSort.Posted, "Posted date"),
        (SettlementGroupSort.Amount, "Amount"),
        (SettlementGroupSort.Order, "Order id"),
    ];

    /// <summary>Money with cents, in the settlement's currency.</summary>
    /// <param name="amount">Amount.</param>
    /// <param name="currency">ISO currency code; blank shows the number alone.</param>
    /// <returns>e.g. "$1,234.50" or "-CA$12.00".</returns>
    public static string Money(decimal amount, string? currency) =>
        string.IsNullOrEmpty(currency) ? amount.ToString("N2", Culture) : DashboardFormat.Price(amount, currency);

    /// <summary>The pill classes for a settlement's origin tag (CA red, MX green, others the info color).</summary>
    /// <param name="origin">The origin code from <see cref="SettlementOrigin.FromCurrency"/>, e.g. <c>CA</c>.</param>
    /// <returns>A CSS class list.</returns>
    public static string OriginPill(string? origin) => origin switch
    {
        "CA" => "pill pill-origin pill-origin-ca",
        "MX" => "pill pill-origin pill-origin-mx",
        _ => "pill pill-origin",
    };

    /// <summary>An exchange rate, to six significant decimals.</summary>
    /// <param name="rate">One unit of the paid currency in the marketplace's.</param>
    /// <returns>e.g. "0.7312" or "0.054540".</returns>
    public static string Rate(decimal rate) => rate.ToString(rate >= 1 ? "0.0000" : "0.######", Culture);

    /// <summary>Where a rate came from, in words.</summary>
    /// <param name="source">The view's <c>RateSource</c>: Market, Manual, Same, or <see langword="null"/>.</param>
    /// <returns>e.g. "daily rate".</returns>
    public static string RateSourceLabel(string? source) => source switch
    {
        "Manual" => "adjusted rate",
        "Market" => "daily rate",
        "Same" => "no conversion",
        _ => "no rate yet",
    };

    /// <summary>A tooltip explaining how a settlement was converted.</summary>
    /// <param name="settlement">The settlement.</param>
    /// <returns>e.g. "CA$1,234.56 × 0.7312 (daily rate) = $902.71".</returns>
    public static string RateTitle(SettlementSummary settlement)
    {
        ArgumentNullException.ThrowIfNull(settlement);
        return settlement.ExchangeRate is { } rate && settlement.HomeNet is { } net
            ? $"{Money(settlement.Net, settlement.Currency)} × {Rate(rate)} ({RateSourceLabel(settlement.RateSource)}) = {Money(net, settlement.HomeCurrency)}"
            : $"Paid in {settlement.Currency}; no exchange rate yet.";
    }

    /// <summary>The class that colors a signed amount: negative red, zero muted, positive plain.</summary>
    /// <param name="amount">Amount.</param>
    /// <returns>A CSS class.</returns>
    public static string AmountClass(decimal amount) => amount switch
    {
        < 0 => "amount-neg",
        0 => "amount-zero",
        _ => "amount-pos",
    };

    /// <summary>A share as a whole percentage.</summary>
    /// <param name="rate">0.153 = 15 %.</param>
    /// <returns>e.g. "15%".</returns>
    public static string Percent(decimal rate) => rate.ToString("P0", Culture);

    /// <summary>A settlement's period, as dates.</summary>
    /// <param name="start">Period start.</param>
    /// <param name="end">Period end.</param>
    /// <returns>e.g. "Sep 4 – Sep 18, 2026".</returns>
    public static string Period(DateTimeOffset? start, DateTimeOffset? end) => (start, end) switch
    {
        ({ } s, { } e) when s.Year == e.Year => $"{s.ToString("MMM d", Culture)} – {e.ToString("MMM d, yyyy", Culture)}",
        ({ } s, { } e) => $"{s.ToString("MMM d, yyyy", Culture)} – {e.ToString("MMM d, yyyy", Culture)}",
        (null, { } e) => $"until {e.ToString("MMM d, yyyy", Culture)}",
        ({ } s, null) => $"from {s.ToString("MMM d, yyyy", Culture)}",
        _ => "No period",
    };

    /// <summary>A posting time in UTC (the time zone the settlement file reports).</summary>
    /// <param name="posted">When the line posted.</param>
    /// <returns>e.g. "Sep 5, 14:02", or empty.</returns>
    public static string Posted(DateTimeOffset? posted) =>
        posted?.ToUniversalTime().ToString("MMM d, HH:mm", Culture) ?? string.Empty;

    /// <summary>
    /// The column a line counts toward, read from the bucket amounts <c>rpt.vw_SettlementLine</c>
    /// filled in (the rule itself lives only in the view).
    /// </summary>
    /// <param name="line">Settlement line.</param>
    /// <returns>The bucket, or <see langword="null"/> for a zero amount.</returns>
    public static SettlementBucket? BucketOf(SettlementLineDetail line)
    {
        ArgumentNullException.ThrowIfNull(line);
        return line switch
        {
            { Sales: not 0 } => SettlementBucket.Sales,
            { Fees: not 0 } => SettlementBucket.Fees,
            { Refunds: not 0 } => SettlementBucket.Refunds,
            { Other: not 0 } => SettlementBucket.Other,
            _ => null,
        };
    }

    /// <summary>The label of a bucket.</summary>
    /// <param name="bucket">Bucket.</param>
    /// <returns>e.g. "Fee".</returns>
    public static string BucketLabel(SettlementBucket bucket) => bucket switch
    {
        SettlementBucket.Sales => "Sale",
        SettlementBucket.Fees => "Fee",
        SettlementBucket.Refunds => "Refund",
        _ => "Other",
    };

    /// <summary>The pill class of a bucket.</summary>
    /// <param name="bucket">Bucket.</param>
    /// <returns>A CSS class list.</returns>
    public static string BucketPill(SettlementBucket bucket) => bucket switch
    {
        SettlementBucket.Sales => "pill pill-ok",
        SettlementBucket.Fees => "pill pill-warn",
        SettlementBucket.Refunds => "pill pill-bad",
        _ => "pill pill-info",
    };

    /// <summary>The arrow for a sortable header given the effective direction.</summary>
    /// <param name="isSorted">Whether the column is the current sort.</param>
    /// <param name="descending">Whether it currently runs high to low.</param>
    /// <returns>"↓", "↑", or empty.</returns>
    public static string Arrow(bool isSorted, bool descending) => !isSorted ? string.Empty : descending ? "↓" : "↑";
}
