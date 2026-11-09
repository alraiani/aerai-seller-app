using System.Globalization;
using AERai.Web.Application.Profit;

namespace AERai.Web.UI.Models;

/// <summary>Display helpers for the profit &amp; loss pages (formatting only; every rule is in <see cref="ProfitService"/>).</summary>
public static class ProfitFormat
{
    private static readonly CultureInfo En = CultureInfo.InvariantCulture;

    /// <summary>A signed amount in the marketplace's currency, with a minus for costs.</summary>
    /// <param name="amount">Amount.</param>
    /// <param name="currency">ISO 4217 currency.</param>
    /// <returns>e.g. <c>$1,234.50</c> or <c>-$12.00</c>; a dash for zero.</returns>
    public static string Money(decimal amount, string currency) => amount == 0 ? "—" : SettlementFormat.Money(amount, currency);

    /// <summary>The CSS class for a profit figure: red below zero.</summary>
    /// <param name="profit">Profit.</param>
    /// <returns>A class, or empty.</returns>
    public static string ProfitClass(decimal profit) => profit < 0 ? "text-negative" : profit > 0 ? "text-positive" : "";

    /// <summary>A margin as a percentage.</summary>
    /// <param name="margin">Profit ÷ sales.</param>
    /// <returns>e.g. <c>23.4%</c>, or a dash without sales.</returns>
    public static string Margin(decimal? margin) => margin is { } m ? Percent(m) : "—";

    /// <summary>A share as a percentage with one decimal and no space before the sign.</summary>
    /// <param name="share">The share (0.25 is 25%).</param>
    /// <returns>e.g. <c>25.0%</c>.</returns>
    public static string Percent(decimal share) => (share * 100m).ToString("0.0", En) + "%";

    /// <summary>A period's label.</summary>
    /// <param name="start">Its first day.</param>
    /// <param name="granularity">Months or weeks.</param>
    /// <param name="full">Whether to spell the year (tables) or keep it short (chart axis).</param>
    /// <returns>e.g. <c>Sep 2026</c>, <c>Sep</c>, or <c>Week of Sep 28</c>.</returns>
    public static string Period(DateOnly start, ProfitGranularity granularity, bool full) => granularity == ProfitGranularity.Month
        ? start.ToString(full ? "MMM yyyy" : "MMM", En)
        : full ? "Week of " + start.ToString("MMM d", En) : start.ToString("MMM d", En);

    /// <summary>A unit count.</summary>
    /// <param name="units">Units.</param>
    /// <returns>e.g. <c>1,204</c>.</returns>
    public static string Count(int units) => units.ToString("N0", En);

    /// <summary>The label of a row of amounts not tied to a SKU.</summary>
    /// <param name="kind">What it holds.</param>
    /// <returns>A short label.</returns>
    public static string UnassignedLabel(ProfitUnassignedKind kind) => kind switch
    {
        ProfitUnassignedKind.AdvertisingNotSplit => "Ad charges with no sales to split by",
        _ => "Account-level amounts",
    };

    /// <summary>The explanation of a row of amounts not tied to a SKU.</summary>
    /// <param name="kind">What it holds.</param>
    /// <returns>A tooltip.</returns>
    public static string UnassignedTip(ProfitUnassignedKind kind) => kind switch
    {
        ProfitUnassignedKind.AdvertisingNotSplit => "Ad charges Amazon billed in a month with no sales to spread them by (before Ads API spend was synced).",
        _ => "Amounts Amazon charges the account rather than a product: the subscription, storage and other account fees, and reimbursements without a SKU. Turn on Fully loaded to spread them across SKUs by sales.",
    };

    /// <summary>The pill for a settlement against the ledger.</summary>
    /// <param name="state">Its state.</param>
    /// <returns>Classes and label.</returns>
    public static (string Css, string Label) TiePill(SettlementTieState state) => state switch
    {
        SettlementTieState.Ties => ("pill pill-ok", "Ties"),
        SettlementTieState.Differs => ("pill pill-warn", "Differs"),
        _ => ("pill pill-muted", "Before ledger"),
    };
}
