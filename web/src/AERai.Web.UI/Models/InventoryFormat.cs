using System.Globalization;

namespace AERai.Web.UI.Models;

/// <summary>Formatting helpers for the Inventory pages (presentation only).</summary>
public static class InventoryFormat
{
    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");

    /// <summary>Pill class and text for how soon a restock action is due.</summary>
    /// <param name="daysUntilAction">Days until the action (negative = overdue).</param>
    /// <param name="soonDays">Actions due within this many days are highlighted.</param>
    /// <returns>CSS classes and label, e.g. ("pill pill-bad", "3 days overdue").</returns>
    public static (string CssClass, string Text) ActionPill(int daysUntilAction, int soonDays) => daysUntilAction switch
    {
        < 0 => ("pill pill-bad", $"{-daysUntilAction} day{(daysUntilAction == -1 ? "" : "s")} overdue"),
        0 => ("pill pill-bad", "Due today"),
        _ when daysUntilAction <= soonDays => ("pill pill-warn", $"In {daysUntilAction} day{(daysUntilAction == 1 ? "" : "s")}"),
        _ => ("pill pill-ok", $"In {daysUntilAction} days"),
    };

    /// <summary>Short date for restock deadlines.</summary>
    /// <param name="date">The date.</param>
    /// <returns>e.g. "Oct 20".</returns>
    public static string ShortDate(DateOnly date) => date.ToString("MMM d", En);
}
