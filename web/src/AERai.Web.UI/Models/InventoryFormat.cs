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

    /// <summary>The restock steps to show: the most urgent first, then the other (if any).</summary>
    /// <param name="plan">The plan.</param>
    /// <returns>Up to two lines, e.g. ("Order 428", "by Aug 17").</returns>
    public static IReadOnlyList<(string Action, string When)> Steps(RestockPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var steps = new List<(string, string, DateOnly)>(2);
        if (plan.OrderFromSupplier > 0)
        {
            steps.Add((string.Create(En, $"Order {plan.OrderFromSupplier:N0}"), $"by {ShortDate(plan.OrderBy)}", plan.OrderBy));
        }

        if (plan.SendFromHome > 0)
        {
            steps.Add((string.Create(En, $"Send {plan.SendFromHome:N0}"), $"by {ShortDate(plan.SendBy)}", plan.SendBy));
        }

        return steps.OrderBy(s => s.Item3).Select(s => (s.Item1, s.Item2)).ToList();
    }

    /// <summary>Tooltip for the restock column: when stock runs out and which lead times were used.</summary>
    /// <param name="item">The SKU.</param>
    /// <returns>Plain text.</returns>
    public static string StepsTip(InventoryItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var t = item.LeadTimes;
        var stockout = item.Restock is { } plan ? $"Stock runs out around {ShortDate(plan.ProjectedStockout)}. " : string.Empty;
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

    /// <summary>Short date for restock deadlines.</summary>
    /// <param name="date">The date.</param>
    /// <returns>e.g. "Oct 20".</returns>
    public static string ShortDate(DateOnly date) => date.ToString("MMM d", En);

    private static string NextVerb(InventoryItem item) =>
        item.Restock is { OrderFromSupplier: > 0 } ? "order from your supplier" : "send home stock to Amazon";

    private static string Capitalize(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
}
