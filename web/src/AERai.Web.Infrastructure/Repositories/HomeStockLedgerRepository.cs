using System.Data;
using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Application.Inventory;
using AERai.Web.Domain.Core;
using AERai.Web.Infrastructure.Persistence;
using AERai.Web.Infrastructure.Queries;
using Microsoft.EntityFrameworkCore;

namespace AERai.Web.Infrastructure.Repositories;

/// <summary>EF Core implementation of <see cref="IHomeStockLedgerRepository"/>.</summary>
/// <param name="dbContext">Scoped database context.</param>
internal sealed class HomeStockLedgerRepository(AppDbContext dbContext) : IHomeStockLedgerRepository
{
    /// <inheritdoc/>
    public Task<(HomeStockLedgerOutcome Outcome, long? Id)> RecordAsync(HomeStockLedgerWrite write, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(write);

        return InTransactionAsync(async () =>
        {
            if (!await dbContext.Products.AnyAsync(p => p.Sku == write.Sku, cancellationToken).ConfigureAwait(false))
            {
                return (HomeStockLedgerOutcome.NotFound, null);
            }

            var row = await BalanceRowAsync(write.MarketplaceId, write.Sku, cancellationToken).ConfigureAwait(false);
            var current = row?.Quantity ?? 0;

            // A recount arrives as the counted total; the ledger records the difference.
            var change = write.Type == HomeStockMovementType.CountCorrection ? write.Units - current : write.Units;
            if (change == 0)
            {
                return (HomeStockLedgerOutcome.Unchanged, null);
            }

            if (current + change < 0)
            {
                return (HomeStockLedgerOutcome.WouldGoNegative, null);
            }

            var movement = HomeStockBalance.Apply(dbContext, row, write with { Units = change });
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return (HomeStockLedgerOutcome.Recorded, movement.Id);
        });
    }

    /// <inheritdoc/>
    public Task<(HomeStockLedgerOutcome Outcome, long? Id)> ReverseAsync(string marketplaceId, long id, DateTimeOffset createdAt, string createdBy, CancellationToken cancellationToken) =>
        InTransactionAsync(async () =>
        {
            var original = await dbContext.HomeStockMovements.AsNoTracking()
                .SingleOrDefaultAsync(m => m.Id == id && m.MarketplaceId == marketplaceId, cancellationToken)
                .ConfigureAwait(false);
            if (original is null)
            {
                return (HomeStockLedgerOutcome.NotFound, null);
            }

            var alreadyReversed = await dbContext.HomeStockMovements.AnyAsync(m => m.ReversesId == id, cancellationToken).ConfigureAwait(false);
            if (alreadyReversed || original.ReversesId is not null || original.Type == HomeStockMovementType.OpeningBalance)
            {
                return (HomeStockLedgerOutcome.CannotReverse, null);
            }

            var row = await BalanceRowAsync(marketplaceId, original.Sku, cancellationToken).ConfigureAwait(false);
            if ((row?.Quantity ?? 0) - original.Units < 0)
            {
                return (HomeStockLedgerOutcome.WouldGoNegative, null);
            }

            var reversal = HomeStockBalance.Apply(dbContext, row, new HomeStockLedgerWrite(
                marketplaceId, original.Sku, original.Type, -original.Units, createdAt, original.Reference, $"Reverses entry #{original.Id}", original.Id, createdAt, createdBy));
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return (HomeStockLedgerOutcome.Recorded, (long?)reversal.Id);
        });

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<HomeStockCountChange> Applied, IReadOnlyList<string> Stale)> ApplyCountsAsync(
        string marketplaceId,
        IReadOnlyList<HomeStockCountChange> changes,
        HomeStockMovementType increaseType,
        HomeStockMovementType decreaseType,
        HomeStockLedgerWrite template,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(changes);
        ArgumentNullException.ThrowIfNull(template);

        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);

            var known = new HashSet<string>(StringComparer.Ordinal);
            var balances = new Dictionary<string, HomeStock>(StringComparer.Ordinal);

            // Chunked so a large sheet stays well under SQL Server's 2,100-parameter limit.
            foreach (var chunk in changes.Select(c => c.Sku).Chunk(1000))
            {
                known.UnionWith(await dbContext.Products.Where(p => chunk.Contains(p.Sku)).Select(p => p.Sku).ToListAsync(cancellationToken).ConfigureAwait(false));
                foreach (var row in await dbContext.HomeStocks.Where(h => h.MarketplaceId == marketplaceId && chunk.Contains(h.Sku)).ToListAsync(cancellationToken).ConfigureAwait(false))
                {
                    balances[row.Sku] = row;
                }
            }

            var applied = new List<HomeStockCountChange>();
            var stale = new List<string>();
            foreach (var change in changes)
            {
                balances.TryGetValue(change.Sku, out var row);

                // The reviewed difference only holds if nobody changed the balance since the review.
                if (!known.Contains(change.Sku) || (row?.Quantity ?? 0) != change.Current || change.Difference == 0)
                {
                    stale.Add(change.Sku);
                    continue;
                }

                var type = change.Difference > 0 ? increaseType : decreaseType;
                HomeStockBalance.Apply(dbContext, row, template with { Sku = change.Sku, Type = type, Units = change.Difference });
                applied.Add(change);
            }

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return ((IReadOnlyList<HomeStockCountChange>)applied, (IReadOnlyList<string>)stale);
        }).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<PagedResult<HomeStockLedgerEntry>> ListAsync(string marketplaceId, HomeStockLedgerFilter filter, PageRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(request);

        var query =
            from m in dbContext.HomeStockMovements.AsNoTracking()
            where m.MarketplaceId == marketplaceId
            join p in dbContext.Products on m.Sku equals p.Sku
            select new { m, p };

        if (filter.Sku is { } sku)
        {
            query = query.Where(x => x.m.Sku == sku);
        }

        if (filter.FamilyId is { } familyId)
        {
            query = query.Where(x => x.p.FamilyId == familyId);
        }

        if (filter.Type is { } type)
        {
            query = query.Where(x => x.m.Type == type);
        }

        if (filter.From is { } from)
        {
            query = query.Where(x => x.m.OccurredAt >= from);
        }

        if (filter.To is { } to)
        {
            query = query.Where(x => x.m.OccurredAt < to);
        }

        if (request.SafeSearch is { } search)
        {
            query = query.Where(x => x.m.Sku.Contains(search)
                || (x.p.Title != null && x.p.Title.Contains(search))
                || (x.m.Reference != null && x.m.Reference.Contains(search))
                || (x.m.Note != null && x.m.Note.Contains(search)));
        }

        // Newest logged first: BalanceAfter follows logging order, so this keeps balances reading in sequence.
        return await query
            .OrderByDescending(x => x.m.Id)
            .Select(x => new HomeStockLedgerEntry(
                x.m.Id,
                x.m.Sku,
                x.p.Title,
                x.p.Color,
                x.m.OccurredAt,
                x.m.Type,
                x.m.Units,
                x.m.BalanceAfter,
                x.m.Reference,
                x.m.Note,
                x.m.ReversesId,
                dbContext.HomeStockMovements.Where(r => r.ReversesId == x.m.Id).Select(r => (long?)r.Id).FirstOrDefault(),
                x.m.CreatedAt,
                x.m.CreatedBy))
            .ToPagedResultAsync(request, cancellationToken)
            .ConfigureAwait(false);
    }

    private Task<HomeStock?> BalanceRowAsync(string marketplaceId, string sku, CancellationToken cancellationToken) =>
        dbContext.HomeStocks.SingleOrDefaultAsync(h => h.MarketplaceId == marketplaceId && h.Sku == sku, cancellationToken);

    /// <summary>
    /// Runs a ledger write in a serializable transaction (each entry is computed from the balance it
    /// reads, so concurrent writers must not interleave), retried as a whole on transient failures.
    /// </summary>
    private Task<(HomeStockLedgerOutcome Outcome, long? Id)> InTransactionAsync(Func<Task<(HomeStockLedgerOutcome Outcome, long? Id)>> work)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable).ConfigureAwait(false);
            var result = await work().ConfigureAwait(false);
            await transaction.CommitAsync().ConfigureAwait(false);
            return result;
        });
    }
}
