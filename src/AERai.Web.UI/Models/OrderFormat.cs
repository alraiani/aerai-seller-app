using System.Globalization;
using AERai.Web.Application.Orders;

namespace AERai.Web.UI.Models;

/// <summary>Presentation helpers for the Orders page: local purchase times and status pills. Formatting only.</summary>
public static class OrderFormat
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-US");

    /// <summary>A purchase time in the marketplace's local time.</summary>
    /// <param name="purchased">When the order was placed.</param>
    /// <param name="zone">The marketplace's time zone.</param>
    /// <returns>e.g. "Oct 14, 2026 3:05 PM".</returns>
    public static string Purchased(DateTimeOffset purchased, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTime(purchased, zone).ToString("MMM d, yyyy h:mm tt", Culture);

    /// <summary>The pill for an order status: shipped is good, waiting is a warning, cancelled or unfulfillable is bad.</summary>
    /// <param name="status">Order status.</param>
    /// <returns>CSS classes.</returns>
    public static string StatusPill(string status)
    {
        if (OrderStatuses.IsCancelled(status) || status.Equals("Unfulfillable", StringComparison.OrdinalIgnoreCase))
        {
            return "pill pill-bad";
        }

        if (status.StartsWith("Shipped", StringComparison.OrdinalIgnoreCase) || status.Equals("Delivered", StringComparison.OrdinalIgnoreCase))
        {
            return "pill pill-ok";
        }

        return status.StartsWith("Pending", StringComparison.OrdinalIgnoreCase)
            || status.Equals("Unshipped", StringComparison.OrdinalIgnoreCase)
            || status.Equals("Shipping", StringComparison.OrdinalIgnoreCase)
            || status.Equals("Partially Shipped", StringComparison.OrdinalIgnoreCase)
            ? "pill pill-warn"
            : "pill pill-info";
    }
}
