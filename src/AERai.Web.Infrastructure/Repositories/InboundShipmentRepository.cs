using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Ingestion;
using AERai.Web.Application.Inventory;
using AERai.Web.Domain.Core;
using AERai.Web.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AERai.Web.Infrastructure.Repositories;

/// <summary>EF Core implementation of <see cref="IInboundShipmentRepository"/>.</summary>
/// <param name="dbContext">Scoped database context.</param>
internal sealed class InboundShipmentRepository(AppDbContext dbContext) : IInboundShipmentRepository
{
    /// <summary>Values per <c>IN</c> list, well under SQL Server's 2,100-parameter limit.</summary>
    private const int LookupChunkSize = 1_000;

    /// <inheritdoc/>
    public async Task<InboundApplyResult> ApplyAsync(string marketplaceId, IReadOnlyCollection<AmazonInboundShipment> shipments, DateTimeOffset seenAt, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marketplaceId);
        ArgumentNullException.ThrowIfNull(shipments);

        var byId = shipments
            .GroupBy(s => s.ShipmentConfirmationId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Last(), StringComparer.OrdinalIgnoreCase);

        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            // On the marketplace's very first read, shipments that already left were sent before this sync existed
            // and were almost certainly taken off home stock by hand, so they are set aside instead of offered again.
            var firstRead = !await dbContext.InboundShipments.AnyAsync(s => s.MarketplaceId == marketplaceId, cancellationToken).ConfigureAwait(false);

            var stored = new Dictionary<string, InboundShipment>(StringComparer.OrdinalIgnoreCase);
            foreach (var chunk in byId.Keys.Chunk(LookupChunkSize))
            {
                var rows = await dbContext.InboundShipments
                    .Include(s => s.Items)
                    .Where(s => s.MarketplaceId == marketplaceId && chunk.Contains(s.ShipmentConfirmationId))
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
                foreach (var row in rows)
                {
                    stored[row.ShipmentConfirmationId] = row;
                }
            }

            var newIds = byId.Keys.Where(id => !stored.ContainsKey(id)).ToList();
            var logged = await LoggedReferencesAsync(marketplaceId, newIds, cancellationToken).ConfigureAwait(false);

            int added = 0, toReview = 0, alreadyLogged = 0, setAside = 0, cancelled = 0;
            foreach (var shipment in byId.Values)
            {
                if (!stored.TryGetValue(shipment.ShipmentConfirmationId, out var row))
                {
                    row = new InboundShipment
                    {
                        MarketplaceId = marketplaceId,
                        ShipmentConfirmationId = shipment.ShipmentConfirmationId,
                        AmazonShipmentId = shipment.AmazonShipmentId,
                        InboundPlanId = shipment.InboundPlanId,
                        AmazonStatus = shipment.Status,
                        FirstSeenAt = seenAt,
                    };
                    (row.Review, row.ReviewedAt, row.ReviewedBy) = logged.Contains(shipment.ShipmentConfirmationId)
                        ? (InboundShipmentReview.AlreadyLogged, seenAt, InboundShipment.SystemReviewer)
                        : shipment.IsCancelled
                            ? (InboundShipmentReview.CancelledByAmazon, seenAt, InboundShipment.SystemReviewer)
                            : firstRead && shipment.HasLeft
                                ? (InboundShipmentReview.Dismissed, seenAt, InboundShipment.SystemReviewer)
                                : (InboundShipmentReview.ToReview, (DateTimeOffset?)null, (string?)null);
                    dbContext.InboundShipments.Add(row);
                    added++;
                    switch (row.Review)
                    {
                        case InboundShipmentReview.ToReview: toReview++; break;
                        case InboundShipmentReview.AlreadyLogged: alreadyLogged++; break;
                        case InboundShipmentReview.Dismissed: setAside++; break;
                    }
                }
                else if (row.Review == InboundShipmentReview.ToReview && shipment.IsCancelled)
                {
                    row.Review = InboundShipmentReview.CancelledByAmazon;
                    row.ReviewedAt = seenAt;
                    row.ReviewedBy = InboundShipment.SystemReviewer;
                    cancelled++;
                }

                row.AmazonShipmentId = shipment.AmazonShipmentId;
                row.InboundPlanId = shipment.InboundPlanId;
                row.PlanName = Truncate(shipment.PlanName, 256);
                row.PlanCreatedAt = shipment.PlanCreatedAt;
                row.Name = Truncate(shipment.Name, 256);
                row.AmazonStatus = Truncate(shipment.Status, 32) ?? string.Empty;
                row.LastSeenAt = seenAt;
                ReplaceItems(row, shipment.Items);
            }

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new InboundApplyResult(byId.Count, added, toReview, alreadyLogged, setAside, cancelled);
        }).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<InboundShipment>> ListAsync(string marketplaceId, int limit, CancellationToken cancellationToken) =>
        await dbContext.InboundShipments
            .AsNoTracking()
            .Include(s => s.Items)
            .Where(s => s.MarketplaceId == marketplaceId)
            .OrderByDescending(s => s.PlanCreatedAt ?? s.FirstSeenAt)
            .ThenByDescending(s => s.Id)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public Task<InboundShipment?> GetAsync(string marketplaceId, long id, CancellationToken cancellationToken) =>
        dbContext.InboundShipments
            .AsNoTracking()
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.MarketplaceId == marketplaceId && s.Id == id, cancellationToken);

    /// <inheritdoc/>
    public async Task<bool> SetReviewAsync(string marketplaceId, long id, InboundShipmentReview expected, InboundShipmentReview review, DateTimeOffset at, string by, CancellationToken cancellationToken)
    {
        // A conditional update, so two users acting on one shipment at once can't both move it.
        var changed = await dbContext.InboundShipments
            .Where(s => s.MarketplaceId == marketplaceId && s.Id == id && s.Review == expected)
            .ExecuteUpdateAsync(u => u
                .SetProperty(s => s.Review, review)
                .SetProperty(s => s.ReviewedAt, at)
                .SetProperty(s => s.ReviewedBy, by), cancellationToken)
            .ConfigureAwait(false);
        return changed == 1;
    }

    /// <inheritdoc/>
    public Task<int> CountToReviewAsync(string marketplaceId, CancellationToken cancellationToken) =>
        dbContext.InboundShipments
            .Where(s => s.MarketplaceId == marketplaceId && s.Review == InboundShipmentReview.ToReview)
            .Where(s => s.Items.Any(i => dbContext.Products.Any(p => p.Sku == i.Sku
                && dbContext.ProductFamilies.Any(f => f.Id == p.FamilyId && f.ShipsFromHomeStock))))
            .CountAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, InboundSkuInfo>> GetSkuInfoAsync(string marketplaceId, IReadOnlyCollection<string> skus, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(skus);

        var info = new Dictionary<string, InboundSkuInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var chunk in skus.Distinct(StringComparer.OrdinalIgnoreCase).Chunk(LookupChunkSize))
        {
            var rows = await (
                    from p in dbContext.Products.AsNoTracking()
                    where chunk.Contains(p.Sku)
                    join f in dbContext.ProductFamilies on p.FamilyId equals f.Id into families
                    from f in families.DefaultIfEmpty()
                    join h in dbContext.HomeStocks.Where(h => h.MarketplaceId == marketplaceId) on p.Sku equals h.Sku into stock
                    from h in stock.DefaultIfEmpty()
                    select new InboundSkuInfo(
                        p.Sku,
                        p.Title,
                        p.FamilyId,
                        f == null ? null : f.Name,
                        f != null && f.ShipsFromHomeStock,
                        h == null ? 0 : h.Quantity))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            foreach (var row in rows)
            {
                info[row.Sku] = row;
            }
        }

        return info;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>>> GetLoggedUnitsAsync(string marketplaceId, IReadOnlyCollection<string> references, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(references);

        var result = new Dictionary<string, IReadOnlyDictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
        foreach (var chunk in references.Distinct(StringComparer.OrdinalIgnoreCase).Chunk(LookupChunkSize))
        {
            // A reversal keeps its original's type and reference with the opposite units, so the sum is what is still
            // logged; an edit to another movement type leaves only the reversal here, which cancels the original.
            var rows = await dbContext.HomeStockMovements
                .AsNoTracking()
                .Where(m => m.MarketplaceId == marketplaceId && m.Type == HomeStockMovementType.ShippedToAmazon
                    && m.Reference != null && chunk.Contains(m.Reference))
                .GroupBy(m => new { Reference = m.Reference!, m.Sku }) // Non-null: filtered just above.
                .Select(g => new { g.Key.Reference, g.Key.Sku, Units = -g.Sum(m => m.Units) })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            foreach (var reference in rows.Where(r => r.Units > 0).GroupBy(r => r.Reference, StringComparer.OrdinalIgnoreCase))
            {
                result[reference.Key] = reference
                    .GroupBy(r => r.Sku, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.Sum(r => r.Units), StringComparer.OrdinalIgnoreCase);
            }
        }

        return result;
    }

    /// <summary>The FBA ids among <paramref name="references"/> that a send in the ledger still carries.</summary>
    private async Task<HashSet<string>> LoggedReferencesAsync(string marketplaceId, IReadOnlyCollection<string> references, CancellationToken cancellationToken)
    {
        var logged = await GetLoggedUnitsAsync(marketplaceId, references, cancellationToken).ConfigureAwait(false);
        return new HashSet<string>(logged.Keys, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Makes the stored SKUs match Amazon's, keeping unchanged rows.</summary>
    private void ReplaceItems(InboundShipment row, IReadOnlyList<AmazonInboundItem> items)
    {
        var wanted = items
            .GroupBy(i => i.Sku, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity), StringComparer.OrdinalIgnoreCase);
        foreach (var gone in row.Items.Where(i => !wanted.ContainsKey(i.Sku)).ToList())
        {
            row.Items.Remove(gone);
            dbContext.InboundShipmentItems.Remove(gone);
        }

        foreach (var (sku, quantity) in wanted)
        {
            var item = row.Items.FirstOrDefault(i => string.Equals(i.Sku, sku, StringComparison.OrdinalIgnoreCase));
            if (item is null)
            {
                row.Items.Add(new InboundShipmentItem { Sku = Truncate(sku, 64)!, Quantity = quantity }); // Non-null: SKUs are never blank.
            }
            else
            {
                item.Quantity = quantity;
            }
        }
    }

    private static string? Truncate(string? value, int length) =>
        value is null ? null : value.Length <= length ? value : value[..length];
}
