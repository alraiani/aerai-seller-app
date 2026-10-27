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

            // Units Amazon already shows did go out, so the send can't be taken back.
            if (original.ReconciledUnits > 0)
            {
                return (HomeStockLedgerOutcome.AlreadyAtAmazon, null);
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
    public Task<(HomeStockLedgerOutcome Outcome, int Reversed, IReadOnlyList<string> BlockingSkus)> ReverseActionAsync(
        string marketplaceId, long entryId, DateTimeOffset createdAt, string createdBy, CancellationToken cancellationToken) =>
        ReverseAllAsync(async () =>
        {
            var anchor = await dbContext.HomeStockMovements.AsNoTracking()
                .Where(m => m.Id == entryId && m.MarketplaceId == marketplaceId)
                .Select(m => new { m.CreatedAt, m.CreatedBy })
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            return anchor is null ? null
                : dbContext.HomeStockMovements.Where(m => m.MarketplaceId == marketplaceId && m.CreatedAt == anchor.CreatedAt && m.CreatedBy == anchor.CreatedBy);
        }, marketplaceId, createdAt, createdBy, cancellationToken);

    /// <inheritdoc/>
    public Task<(HomeStockLedgerOutcome Outcome, int Reversed, IReadOnlyList<string> BlockingSkus)> ReverseEntriesAsync(
        string marketplaceId, IReadOnlyCollection<long> ids, DateTimeOffset createdAt, string createdBy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);

        return ReverseAllAsync(
            () => Task.FromResult<IQueryable<HomeStockMovement>?>(dbContext.HomeStockMovements.Where(m => m.MarketplaceId == marketplaceId && ids.Contains(m.Id))),
            marketplaceId, createdAt, createdBy, cancellationToken);
    }

    /// <summary>
    /// Reverses every entry in a scope that can still be reversed, in one serializable transaction that commits only
    /// when all of them could be, every reversal logged at <paramref name="createdAt"/>.
    /// </summary>
    private async Task<(HomeStockLedgerOutcome Outcome, int Reversed, IReadOnlyList<string> BlockingSkus)> ReverseAllAsync(
        Func<Task<IQueryable<HomeStockMovement>?>> scope, string marketplaceId, DateTimeOffset createdAt, string createdBy, CancellationToken cancellationToken)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);
            var candidates = await scope().ConfigureAwait(false);
            var result = candidates is null
                ? (HomeStockLedgerOutcome.NotFound, 0, (IReadOnlyList<string>)[])
                : await ReverseScopeAsync(candidates, marketplaceId, createdAt, createdBy, cancellationToken).ConfigureAwait(false);

            // Nothing is written unless every entry could be reversed, so a refused undo leaves the action whole.
            if (result.Item1 == HomeStockLedgerOutcome.Recorded)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }

            return result;
        }).ConfigureAwait(false);
    }

    private async Task<(HomeStockLedgerOutcome Outcome, int Reversed, IReadOnlyList<string> BlockingSkus)> ReverseScopeAsync(
        IQueryable<HomeStockMovement> candidates, string marketplaceId, DateTimeOffset createdAt, string createdBy, CancellationToken cancellationToken)
    {
        // The entries that can still be reversed: not reversals, not reversed (an edited original is reversed, so
        // only its corrected copy counts), not opening balances.
        var entries = await candidates.AsNoTracking()
            .Where(m => m.ReversesId == null
                && m.Type != HomeStockMovementType.OpeningBalance
                && !dbContext.HomeStockMovements.Any(r => r.ReversesId == m.Id))
            .OrderBy(m => m.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (entries.Count == 0)
        {
            return (HomeStockLedgerOutcome.CannotReverse, 0, []);
        }

        var atAmazon = entries.Where(m => m.ReconciledUnits > 0).Select(m => m.Sku).Distinct(StringComparer.Ordinal).ToList();
        if (atAmazon.Count > 0)
        {
            return (HomeStockLedgerOutcome.AlreadyAtAmazon, 0, atAmazon);
        }

        var skus = entries.Select(m => m.Sku).Distinct(StringComparer.Ordinal).ToList();
        var balances = new Dictionary<string, HomeStock>(StringComparer.Ordinal);

        // Chunked so a large count sheet stays well under SQL Server's 2,100-parameter limit.
        foreach (var chunk in skus.Chunk(1000))
        {
            foreach (var row in await dbContext.HomeStocks.AsNoTracking().Where(h => h.MarketplaceId == marketplaceId && chunk.Contains(h.Sku)).ToListAsync(cancellationToken).ConfigureAwait(false))
            {
                balances[row.Sku] = row;
            }
        }

        // Checked per SKU on the net change, since one action can move a SKU more than once.
        var negative = entries
            .GroupBy(m => m.Sku, StringComparer.Ordinal)
            .Where(g => (balances.GetValueOrDefault(g.Key)?.Quantity ?? 0) - g.Sum(m => m.Units) < 0)
            .Select(g => g.Key)
            .ToList();
        if (negative.Count > 0)
        {
            return (HomeStockLedgerOutcome.WouldGoNegative, 0, negative);
        }

        // One SKU can appear more than once and a balance of zero removes its row, so each reversal reads the
        // balance it applies to. Units taken out are put back before units added are taken out, so a SKU's
        // balance never dips below zero part way (only the net was checked).
        foreach (var original in entries.OrderBy(m => m.Units).ThenBy(m => m.Id))
        {
            var row = await BalanceRowAsync(marketplaceId, original.Sku, cancellationToken).ConfigureAwait(false);
            HomeStockBalance.Apply(dbContext, row, new HomeStockLedgerWrite(
                marketplaceId, original.Sku, original.Type, -original.Units, createdAt, original.Reference, $"Reverses entry #{original.Id}", original.Id, createdAt, createdBy));
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return (HomeStockLedgerOutcome.Recorded, entries.Count, []);
    }

    /// <inheritdoc/>
    public Task<(HomeStockLedgerOutcome Outcome, long? Id)> CorrectAsync(
        string marketplaceId,
        long id,
        HomeStockMovementType type,
        int units,
        string? note,
        IReadOnlyDictionary<string, AmazonBaseline> amazonBaselines,
        DateTimeOffset createdAt,
        string createdBy,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(amazonBaselines);

        return InTransactionAsync(async () =>
        {
            var original = await dbContext.HomeStockMovements.AsNoTracking()
                .SingleOrDefaultAsync(m => m.Id == id && m.MarketplaceId == marketplaceId, cancellationToken)
                .ConfigureAwait(false);
            if (original is null)
            {
                return (HomeStockLedgerOutcome.NotFound, null);
            }

            // An edited entry is reversed, so "already reversed" also covers "already edited".
            var alreadyReversed = await dbContext.HomeStockMovements.AnyAsync(m => m.ReversesId == id, cancellationToken).ConfigureAwait(false);
            if (alreadyReversed || original.ReversesId is not null || original.Type == HomeStockMovementType.OpeningBalance)
            {
                return (HomeStockLedgerOutcome.CannotReverse, null);
            }

            if (original.Type == type && original.Units == units && original.Note == note)
            {
                return (HomeStockLedgerOutcome.Unchanged, null);
            }

            // Units Amazon already shows did go out: the send can't shrink below them or become something else.
            var wasSend = original.Type == HomeStockMovementType.ShippedToAmazon;
            var isSend = type == HomeStockMovementType.ShippedToAmazon;
            if (wasSend && original.ReconciledUnits > 0 && (!isSend || -units < original.ReconciledUnits))
            {
                return (HomeStockLedgerOutcome.AlreadyAtAmazon, null);
            }

            var current = (await BalanceRowAsync(marketplaceId, original.Sku, cancellationToken).ConfigureAwait(false))?.Quantity ?? 0;
            if (current - original.Units + units < 0)
            {
                return (HomeStockLedgerOutcome.WouldGoNegative, null);
            }

            // A send keeps the baseline it was logged with; an entry that becomes a send is measured from now.
            var baseline = !isSend ? null
                : wasSend ? new AmazonBaseline(original.AmazonBaselineUnits ?? 0, original.AmazonBaselineAt)
                : amazonBaselines.GetValueOrDefault(original.Sku) ?? AmazonBaseline.Of(null);
            var reversal = new HomeStockLedgerWrite(
                marketplaceId, original.Sku, original.Type, -original.Units, createdAt, original.Reference, $"Edited: reverses entry #{original.Id}", original.Id, createdAt, createdBy);

            // The copy keeps when the units moved and who logged them, so a send stays in its group on Awaiting Amazon.
            // An entry that only now becomes a send is logged as of now instead: its baseline is today's, and the
            // reconciler compares Amazon's numbers from the send's logged time.
            var newSend = isSend && !wasSend;
            var copy = new HomeStockLedgerWrite(
                marketplaceId, original.Sku, type, units, original.OccurredAt, original.Reference, note, null,
                newSend ? createdAt : original.CreatedAt, newSend ? createdBy : original.CreatedBy, baseline, original.Id);

            // The reversal goes first, so the copy's balance reads in logging order. When taking the original's units
            // out first would dip below zero (some of them have since gone out), the copy goes first instead, and the
            // two balances are swapped afterwards: the lists hide the reversal, so the copy must show the final count.
            var reversalFirst = current - original.Units >= 0;
            HomeStockMovement? corrected = null;
            HomeStockMovement? reversed = null;
            foreach (var write in reversalFirst ? new[] { reversal, copy } : [copy, reversal])
            {
                var row = await BalanceRowAsync(marketplaceId, original.Sku, cancellationToken).ConfigureAwait(false);
                var movement = HomeStockBalance.Apply(dbContext, row, write);
                if (ReferenceEquals(write, copy))
                {
                    corrected = movement;
                }
                else
                {
                    reversed = movement;
                }

                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            if (!reversalFirst && corrected is not null && reversed is not null)
            {
                (corrected.BalanceAfter, reversed.BalanceAfter) = (reversed.BalanceAfter, corrected.BalanceAfter);
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            if (wasSend && isSend && corrected is not null)
            {
                // Amazon's progress on the send carries over; it closes once Amazon shows every unit. A reconcile that
                // matched the original but writes after this commit lands on the reversed original instead; the send
                // then stays pending until the next reconcile matches the copy, so it errs on the safe side.
                corrected.ReconciledUnits = original.ReconciledUnits;
                var closed = original.ReconciledUnits >= -units;
                corrected.ReconciledAt = closed ? original.ReconciledAt ?? createdAt : null;
                corrected.ReconciledBy = closed ? original.ReconciledBy ?? createdBy : null;
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            return (HomeStockLedgerOutcome.Recorded, corrected?.Id);
        });
    }

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<HomeStockCountChange> Applied, IReadOnlyList<string> Stale)> ApplyCountsAsync(
        string marketplaceId,
        IReadOnlyList<HomeStockCountChange> changes,
        HomeStockMovementType increaseType,
        HomeStockMovementType decreaseType,
        HomeStockLedgerWrite template,
        IReadOnlyDictionary<string, AmazonBaseline> amazonBaselines,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(changes);
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(amazonBaselines);

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
                var baseline = type == HomeStockMovementType.ShippedToAmazon ? amazonBaselines.GetValueOrDefault(change.Sku) ?? AmazonBaseline.Of(null) : null;
                HomeStockBalance.Apply(dbContext, row, template with { Sku = change.Sku, Type = type, Units = change.Difference, AmazonBaseline = baseline });
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

        // Newest logged first: BalanceAfter follows logging order, so this keeps balances reading in sequence.
        return await ToEntries(Filtered(marketplaceId, filter, request.SafeSearch).OrderByDescending(x => x.Movement.Id))
            .ToPagedResultAsync(request, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<PagedResult<HomeStockLedgerAction>> ListActionsAsync(string marketplaceId, HomeStockLedgerFilter filter, PageRequest request, int maxRows, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxRows);

        var filtered = Filtered(marketplaceId, filter, request.SafeSearch);

        // Type is stored as text, so the kinds in an action are counted one by one rather than taken as a min/max.
        var actions = filtered
            .GroupBy(x => new { x.Movement.CreatedAt, x.Movement.CreatedBy })
            .Select(g => new
            {
                g.Key.CreatedAt,
                g.Key.CreatedBy,
                Count = g.Count(),
                Skus = g.Select(x => x.Movement.Sku).Distinct().Count(),
                In = g.Sum(x => x.Movement.Units > 0 ? x.Movement.Units : 0),
                Out = g.Sum(x => x.Movement.Units < 0 ? x.Movement.Units : 0),
                Reversals = g.Count(x => x.Movement.ReversesId != null),
                Shipped = g.Count(x => x.Movement.Type == HomeStockMovementType.ShippedToAmazon),
                Received = g.Count(x => x.Movement.Type == HomeStockMovementType.ReceivedFromSupplier),
                Counted = g.Count(x => x.Movement.Type == HomeStockMovementType.CountCorrection),
                Other = g.Count(x => x.Movement.Type == HomeStockMovementType.Other),
                Opening = g.Count(x => x.Movement.Type == HomeStockMovementType.OpeningBalance),
                From = g.Min(x => x.Movement.OccurredAt),
                To = g.Max(x => x.Movement.OccurredAt),
                Reference = g.Max(x => x.Movement.Reference),
            });

        var total = await actions.CountAsync(cancellationToken).ConfigureAwait(false);
        var page = await actions
            .OrderByDescending(a => a.CreatedAt)
            .ThenBy(a => a.CreatedBy)
            .Skip(request.Skip)
            .Take(request.SafePageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // One capped query per action (at most a page of them), so a count sheet of thousands of rows never loads in full.
        var entries = new Dictionary<(DateTimeOffset, string), List<HomeStockLedgerEntry>>();
        foreach (var action in page)
        {
            entries[(action.CreatedAt, action.CreatedBy)] = await ToEntries(filtered
                    .Where(x => x.Movement.CreatedAt == action.CreatedAt && x.Movement.CreatedBy == action.CreatedBy)
                    .OrderByDescending(x => x.Movement.Id)
                    .Take(maxRows))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        var items = page.Select(a =>
        {
            var types = new List<HomeStockMovementType>(2);
            AddIf(types, a.Shipped, HomeStockMovementType.ShippedToAmazon);
            AddIf(types, a.Received, HomeStockMovementType.ReceivedFromSupplier);
            AddIf(types, a.Counted, HomeStockMovementType.CountCorrection);
            AddIf(types, a.Other, HomeStockMovementType.Other);
            AddIf(types, a.Opening, HomeStockMovementType.OpeningBalance);
            return new HomeStockLedgerAction(a.CreatedAt, a.CreatedBy, types, a.Count, a.Skus, a.In, a.Out, a.Reversals, a.From, a.To, a.Reference, entries[(a.CreatedAt, a.CreatedBy)]);
        }).ToList();

        return new PagedResult<HomeStockLedgerAction>(items, total, request.SafePage, request.SafePageSize);

        static void AddIf(List<HomeStockMovementType> types, int count, HomeStockMovementType type)
        {
            if (count > 0)
            {
                types.Add(type);
            }
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<HomeStockLedgerEntry>> GetEntriesAsync(string marketplaceId, IReadOnlyCollection<long> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var rows =
            from m in dbContext.HomeStockMovements.AsNoTracking()
            where m.MarketplaceId == marketplaceId && ids.Contains(m.Id)
            join p in dbContext.Products on m.Sku equals p.Sku
            select new LedgerRow { Movement = m, Product = p };
        return await ToEntries(rows).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    private IQueryable<LedgerRow> Filtered(string marketplaceId, HomeStockLedgerFilter filter, string? search)
    {
        // An edit leaves the original and its reversal behind as history; the page lists only the corrected copy.
        var query =
            from m in dbContext.HomeStockMovements.AsNoTracking()
            where m.MarketplaceId == marketplaceId
                && !dbContext.HomeStockMovements.Any(c => c.CorrectsId == m.Id)
                && !(m.ReversesId != null && dbContext.HomeStockMovements.Any(c => c.CorrectsId == m.ReversesId))
            join p in dbContext.Products on m.Sku equals p.Sku
            select new LedgerRow { Movement = m, Product = p };

        if (filter.Sku is { } sku)
        {
            query = query.Where(x => x.Movement.Sku == sku);
        }

        if (filter.FamilyId is { } familyId)
        {
            query = query.Where(x => x.Product.FamilyId == familyId);
        }

        if (filter.Type is { } type)
        {
            query = query.Where(x => x.Movement.Type == type);
        }

        if (filter.From is { } from)
        {
            query = query.Where(x => x.Movement.OccurredAt >= from);
        }

        if (filter.To is { } to)
        {
            query = query.Where(x => x.Movement.OccurredAt < to);
        }

        if (search is not null)
        {
            query = query.Where(x => x.Movement.Sku.Contains(search)
                || (x.Product.Title != null && x.Product.Title.Contains(search))
                || (x.Movement.Reference != null && x.Movement.Reference.Contains(search))
                || (x.Movement.Note != null && x.Movement.Note.Contains(search)));
        }

        return query;
    }

    private IQueryable<HomeStockLedgerEntry> ToEntries(IQueryable<LedgerRow> rows) =>
        rows.Select(x => new HomeStockLedgerEntry(
            x.Movement.Id,
            x.Movement.Sku,
            x.Product.Title,
            x.Product.Color,
            x.Movement.OccurredAt,
            x.Movement.Type,
            x.Movement.Units,
            x.Movement.BalanceAfter,
            x.Movement.Reference,
            x.Movement.Note,
            x.Movement.ReversesId,
            dbContext.HomeStockMovements.Where(r => r.ReversesId == x.Movement.Id).Select(r => (long?)r.Id).FirstOrDefault(),
            x.Movement.CreatedAt,
            x.Movement.CreatedBy,
            x.Movement.CorrectsId == null ? null : (
                from was in dbContext.HomeStockMovements
                where was.Id == x.Movement.CorrectsId
                from undo in dbContext.HomeStockMovements
                where undo.ReversesId == was.Id
                select new HomeStockLedgerEdit(undo.CreatedAt, undo.CreatedBy, was.Type, was.Units, was.Note)).FirstOrDefault()));

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

    /// <summary>A ledger entry with its product, so filters and the projection can be composed.</summary>
    private sealed class LedgerRow
    {
        public required HomeStockMovement Movement { get; init; }

        public required Product Product { get; init; }
    }
}
