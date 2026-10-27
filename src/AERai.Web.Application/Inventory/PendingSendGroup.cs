namespace AERai.Web.Application.Inventory;

/// <summary>
/// The open sends logged together: one Worksheet save, count-sheet upload, or logged movement. Every entry
/// one save writes shares its logged time and user, so those two identify the send.
/// </summary>
/// <param name="LoggedAt">When the send was logged.</param>
/// <param name="LoggedBy">Who logged it.</param>
/// <param name="Items">Its open sends (one per SKU), in listing order.</param>
public sealed record PendingSendGroup(DateTimeOffset LoggedAt, string LoggedBy, IReadOnlyList<PendingSendItem> Items)
{
    /// <summary>When the units went out (the date entered with the send).</summary>
    public DateTimeOffset SentAt => Items.Min(i => i.Send.OccurredAt);

    /// <summary>Units sent across the group.</summary>
    public int UnitsSent => Items.Sum(i => i.Send.UnitsSent);

    /// <summary>Units Amazon's numbers don't show yet.</summary>
    public int Pending => Items.Sum(i => i.Send.Pending);

    /// <summary>Whole days since it was logged.</summary>
    public int AgeDays => Items.Max(i => i.AgeDays);

    /// <summary>Whether any of its sends is flagged as overdue.</summary>
    public bool Overdue => Items.Any(i => i.Overdue);

    /// <summary>Distinct references (FBA shipment ids or other) on its sends.</summary>
    public IReadOnlyList<string> References => [.. Items.Select(i => i.Send.Reference).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase)];

    /// <summary>Its sends without a reference, which can still take a shipment id.</summary>
    public IReadOnlyList<PendingSendItem> NeedsShipmentId => [.. Items.Where(i => i.Send.Reference is null)];

    /// <summary>
    /// Groups open sends by the send they were logged in, the newest send first (what was just shipped is what
    /// people look for); inside a group the sends keep their listing order.
    /// </summary>
    /// <param name="items">The open sends.</param>
    /// <returns>One group per send.</returns>
    public static IReadOnlyList<PendingSendGroup> GroupBySend(IEnumerable<PendingSendItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return [.. items
            .GroupBy(i => (i.Send.CreatedAt, i.Send.CreatedBy))
            .Select(g => new PendingSendGroup(g.Key.CreatedAt, g.Key.CreatedBy, [.. g]))
            .OrderByDescending(g => g.LoggedAt)
            .ThenBy(g => g.LoggedBy, StringComparer.Ordinal)];
    }
}
