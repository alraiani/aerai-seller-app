using System.Globalization;
using AERai.Web.Application.Inventory;

namespace AERai.Web.UI.Models;

/// <summary>Formatting helpers for the Inventory pages (presentation only).</summary>
public static class InventoryFormat
{
    /// <summary>Width of the days-of-inventory cover bar, in SVG user units.</summary>
    public const int CoverBarWidth = 72;

    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");

    /// <summary>Status filters offered on the Inventory page, with their labels.</summary>
    public static IReadOnlyList<(StockStatusFilter Filter, string Label)> StatusFilters { get; } =
    [
        (StockStatusFilter.All, "Any status"),
        (StockStatusFilter.NeedsAction, "Needs restock"),
        (StockStatusFilter.OutOfStock, "Out of stock"),
        (StockStatusFilter.Healthy, "Healthy"),
        (StockStatusFilter.NoSalesData, "No sales data"),
        (StockStatusFilter.NotAtAmazon, "Not at Amazon yet"),
    ];

    /// <summary>Sorts offered on the Inventory page, with their labels.</summary>
    public static IReadOnlyList<(InventorySort Sort, string Label)> Sorts { get; } =
    [
        (InventorySort.Urgency, "Sort: Most urgent"),
        (InventorySort.DaysOfInventory, "Sort: Days of inventory"),
        (InventorySort.Sold30d, "Sort: Best sellers (30d)"),
        (InventorySort.Sold90d, "Sort: Best sellers (90d)"),
        (InventorySort.Available, "Sort: Most available"),
        (InventorySort.Sku, "Sort: SKU"),
    ];

    /// <summary>Pill class, short label, and explanation for a stock status.</summary>
    /// <param name="item">The SKU.</param>
    /// <returns>CSS classes, label, and tooltip text.</returns>
    public static (string CssClass, string Label, string Tip) Status(InventoryItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var due = item.Restock?.DaysUntilAction;
        return item.Status switch
        {
            StockStatus.OutOfStock => ("pill pill-bad", "Out of stock", string.Create(En, $"Nothing available at Amazon, and {item.UnitsSold30d:N0} sold in the last 30 days.")),
            StockStatus.RestockOverdue => ("pill pill-bad", "Overdue", $"The last day to {NextVerb(item)} was {-due} day{(due == -1 ? "" : "s")} ago."),
            StockStatus.RestockSoon => ("pill pill-warn", due == 0 ? "Due today" : "Due soon", due == 0 ? $"{Capitalize(NextVerb(item))} today." : $"{Capitalize(NextVerb(item))} within {due} day{(due == 1 ? "" : "s")}."),
            StockStatus.Healthy => ("pill pill-ok", "Healthy", item.Restock?.DaysUntilAction is { } later
                ? $"Next restock due in {later} days."
                : item.Restock is { } plan ? $"Enough stock until {ShortDate(plan.ProjectedStockout)}." : "Enough stock."),
            StockStatus.NoSalesData => ("pill pill-info", "No sales data", $"Needs {InventoryService.MinCoverageDays} days and {InventoryService.MinUnitsSold} units of sales history to estimate a sales rate."),
            _ => ("pill pill-info", "Not at Amazon", "Held at home only; nothing has been sent to Amazon yet."),
        };
    }

    /// <summary>The send-to-Amazon instruction, e.g. ("Send 40", "by Jun 11"), or <see langword="null"/> when nothing needs sending.</summary>
    /// <param name="plan">The plan.</param>
    /// <returns>The action and its deadline.</returns>
    public static (string Action, string When)? Send(RestockPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return plan is { SendToAmazon: > 0, SendBy: { } by }
            ? (string.Create(En, $"Send {plan.SendToAmazon:N0}"), $"by {ShortDate(by)}")
            : null;
    }

    /// <summary>Amazon's recommendation as a short line, e.g. "Amazon suggests 35 by Jun 9".</summary>
    /// <param name="quantity">Amazon's recommended units, or <see langword="null"/> when its report doesn't cover the SKU.</param>
    /// <param name="shipDate">Amazon's recommended ship date.</param>
    /// <returns>The line, or <see langword="null"/> when there is no report row.</returns>
    public static string? AmazonAdvice(int? quantity, DateOnly? shipDate) => quantity switch
    {
        null => null,
        0 => "Amazon: no send needed",
        { } units => string.Create(En, $"Amazon suggests {units:N0}{(shipDate is { } d ? $" by {ShortDate(d)}" : "")}"),
    };

    /// <summary>The supplier reorder as a short line, e.g. ("Order 270", "by Aug 17").</summary>
    /// <param name="plan">The plan.</param>
    /// <returns>The action and its deadline.</returns>
    public static (string Action, string When) Reorder(RestockPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return (string.Create(En, $"Order {plan.ReorderQuantity:N0}"), $"by {ShortDate(plan.ReorderBy)}");
    }

    /// <summary>Tooltip for the restock column: when stock runs out and which lead times were used.</summary>
    /// <param name="item">The SKU.</param>
    /// <returns>Plain text.</returns>
    public static string StepsTip(InventoryItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var t = item.LeadTimes;
        var stockout = item.Restock is { } plan
            ? $"Stock at Amazon runs out around {ShortDate(plan.ProjectedStockout)}; reorder from the supplier by {ShortDate(plan.ReorderBy)}. "
            : string.Empty;
        return $"{stockout}Lead times{(t.IsCustom ? "" : " (defaults)")}: supplier {t.SupplierLeadTimeDays} d, prep {t.PrepTimeDays} d, transit {t.TransitDays} d, safety {t.SafetyStockDays} d; restock covers {t.TargetStockDays} days of sales.";
    }

    /// <summary>Width of the filled part of the cover bar: days of inventory against the target cover.</summary>
    /// <param name="item">The SKU.</param>
    /// <returns>Width in SVG units (0 to <see cref="CoverBarWidth"/>).</returns>
    public static int CoverWidth(InventoryItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (item.DaysOfInventory is not { } days || item.LeadTimes.TargetStockDays <= 0)
        {
            return 0;
        }

        var share = Math.Clamp((double)days / item.LeadTimes.TargetStockDays, 0, 1);
        return (int)Math.Round(share * CoverBarWidth);
    }

    /// <summary>Token-colored class for the cover bar fill, by status.</summary>
    /// <param name="status">Stock status.</param>
    /// <returns>CSS class.</returns>
    public static string CoverClass(StockStatus status) => status switch
    {
        StockStatus.OutOfStock or StockStatus.RestockOverdue => "cover-bad",
        StockStatus.RestockSoon => "cover-warn",
        _ => "cover-ok",
    };

    /// <summary>Highlight for a deadline: overdue, due within the alert window, or neither.</summary>
    /// <param name="daysUntil">Days from today to the deadline (negative = overdue).</param>
    /// <param name="soonDays">The alert window in days.</param>
    /// <returns>CSS class, or empty.</returns>
    public static string DueClass(int? daysUntil, int soonDays) => daysUntil switch
    {
        < 0 => "action-late",
        { } d when d <= soonDays => "action-due",
        _ => string.Empty,
    };

    /// <summary>Short date for restock deadlines.</summary>
    /// <param name="date">The date.</param>
    /// <returns>e.g. "Oct 20".</returns>
    public static string ShortDate(DateOnly date) => date.ToString("MMM d", En);

    // Whichever deadline drives the status: a send when one is due first, otherwise the supplier reorder.
    private static string NextVerb(InventoryItem item) =>
        item.Restock is { SendBy: { } by } plan && by <= plan.ReorderBy ? "send home stock to Amazon" : "reorder from your supplier";

    private static string Capitalize(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
}
