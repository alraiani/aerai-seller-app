namespace AERai.Web.Application.Inventory;

/// <summary>
/// Works out how much of each send pending to Amazon a new FBA snapshot shows. Pure and stateless.
/// </summary>
/// <remarks>
/// <para>
/// Only a snapshot received after a send was logged can show it, so later sends are left alone. Sends
/// are matched oldest first, and partly matched sends keep their remainder pending.
/// </para>
/// <para>
/// <b>With an FBA shipment id</b>: Amazon lists a shipment's units as inbound as soon as it exists in
/// Seller Central, so the snapshot's inbound units cover id'd sends. This catches the common case of the
/// shipment being created before the send is logged, when the units may already be in the baseline.
/// </para>
/// <para>
/// <b>Without an id</b> (delta rule): units that arrived since the oldest open send = sell-through stock
/// now − its baseline + units sold in between (sales take units out of sell-through stock). Units already
/// accounted for since then — matched units of other id-less sends and all units of id'd sends, whether
/// or not they've arrived — are taken off first, so a send is never matched with another's units; the
/// error, if any, keeps a send pending longer, never shorter. The rest matches open id-less sends.
/// </para>
/// <para>
/// Anything else that moves Amazon's numbers (returns, removals, damage) can leave a small remainder
/// pending; it is flagged after <see cref="InventoryOptions.PendingSendExpiryDays"/> for someone to check.
/// </para>
/// </remarks>
public static class PendingReconciliation
{
    /// <summary>Matches one SKU's sends against its latest snapshot.</summary>
    /// <param name="sends">
    /// The SKU's sends logged since its oldest open send, open or not (closed ones still tell which units
    /// are already accounted for).
    /// </param>
    /// <param name="stock">The SKU's latest FBA snapshot.</param>
    /// <param name="sold">The SKU's sales since its oldest open send's baseline.</param>
    /// <returns>The sends whose reconciled units changed, with their new totals.</returns>
    public static IReadOnlyList<PendingSendMatch> Match(IReadOnlyList<PendingSend> sends, AmazonStockReading stock, IReadOnlyList<UnitsSold> sold)
    {
        ArgumentNullException.ThrowIfNull(sends);
        ArgumentNullException.ThrowIfNull(stock);
        ArgumentNullException.ThrowIfNull(sold);

        // Only sends logged before the snapshot was received can be in it.
        var seen = sends
            .Where(s => s.CreatedAt < stock.ReceivedAt)
            .OrderBy(s => s.CreatedAt)
            .ThenBy(s => s.Id)
            .ToList();
        var reconciled = seen.ToDictionary(s => s.Id, s => s.ReconciledUnits);

        var inboundLeft = Math.Max(0, stock.Inbound);
        foreach (var send in seen.Where(s => s.IsOpen && s.HasShipmentId))
        {
            var take = Math.Min(send.Pending, inboundLeft);
            reconciled[send.Id] += take;
            inboundLeft -= take;
        }

        var idless = seen.Where(s => !s.HasShipmentId).ToList();
        if (idless.FirstOrDefault(s => s.IsOpen && s.AmazonBaseline is not null) is { AmazonBaseline: { } baseline } oldest)
        {
            var from = baseline.ReceivedAt ?? oldest.CreatedAt;
            var soldSince = sold.Where(s => s.PurchaseDate >= from && s.PurchaseDate < stock.ReceivedAt).Sum(s => s.Quantity);
            var arrived = stock.SellThroughStock - baseline.Units + soldSince;

            var since = seen.Where(s => s.CreatedAt >= oldest.CreatedAt).ToList();
            var accounted = since.Where(s => !s.HasShipmentId).Sum(s => s.ReconciledUnits) + since.Where(s => s.HasShipmentId).Sum(s => s.UnitsSent);

            var left = Math.Max(0, arrived - accounted);
            foreach (var send in idless.Where(s => s.IsOpen && s.CreatedAt >= oldest.CreatedAt))
            {
                var take = Math.Min(send.Pending, left);
                reconciled[send.Id] += take;
                left -= take;
            }
        }

        return seen
            .Where(s => s.IsOpen && reconciled[s.Id] != s.ReconciledUnits)
            .Select(s => new PendingSendMatch(s.Id, reconciled[s.Id], reconciled[s.Id] >= s.UnitsSent))
            .ToList();
    }
}
