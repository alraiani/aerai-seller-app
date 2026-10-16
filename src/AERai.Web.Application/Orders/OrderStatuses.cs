namespace AERai.Web.Application.Orders;

/// <summary>The order statuses the Orders page treats specially.</summary>
public static class OrderStatuses
{
    /// <summary>
    /// The spellings of a cancelled order. Both appear in Amazon data, and the sales views exclude both
    /// (<c>rpt.vw_SalesLine</c>), so the Orders totals do too.
    /// </summary>
    public static IReadOnlyList<string> CancelledSpellings { get; } = ["Cancelled", "Canceled"];

    /// <summary>Whether a status means the order was cancelled.</summary>
    /// <param name="status">Order status.</param>
    /// <returns><see langword="true"/> for either spelling of cancelled, ignoring case.</returns>
    public static bool IsCancelled(string? status) =>
        status is not null && CancelledSpellings.Contains(status, StringComparer.OrdinalIgnoreCase);
}
