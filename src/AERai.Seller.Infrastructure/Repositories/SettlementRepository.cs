using AERai.Seller.Application.Abstractions;
using AERai.Seller.Domain.Staging;
using Microsoft.EntityFrameworkCore;

namespace AERai.Seller.Infrastructure.Repositories;

public sealed class SettlementRepository(IDbContextFactory<SellerDbContext> dbContextFactory) : ISettlementRepository
{
    public async Task UpsertAsync(
        IEnumerable<SettlementReport> settlements,
        IEnumerable<SettlementLineItem> lineItems,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var settlementList = settlements.ToList();
        var lineItemList = lineItems.ToList();

        foreach (var settlement in settlementList)
        {
            var exists = await dbContext.SettlementReports.AnyAsync(s => s.SettlementId == settlement.SettlementId, cancellationToken);
            if (exists)
            {
                dbContext.SettlementReports.Update(settlement);
            }
            else
            {
                dbContext.SettlementReports.Add(settlement);
            }
        }

        // Re-syncing a settlement replaces its line items wholesale rather than trying to diff them —
        // Amazon can amend a settlement's contents while it's still the most recent one, and there's
        // no natural per-line-item key to upsert against.
        var settlementIds = settlementList.Select(s => s.SettlementId).ToList();
        var existingLineItems = await dbContext.SettlementLineItems
            .Where(l => settlementIds.Contains(l.SettlementId))
            .ToListAsync(cancellationToken);
        dbContext.SettlementLineItems.RemoveRange(existingLineItems);
        dbContext.SettlementLineItems.AddRange(lineItemList);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SettlementReport>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.SettlementReports.AsNoTracking().ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SettlementLineItem>> GetLineItemsAsync(string settlementId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.SettlementLineItems
            .AsNoTracking()
            .Where(l => l.SettlementId == settlementId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<(string AmountType, string AmountDescription)>> GetDistinctCategoriesAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var pairs = await dbContext.SettlementLineItems
            .AsNoTracking()
            .Select(l => new { l.AmountType, l.AmountDescription })
            .Distinct()
            .ToListAsync(cancellationToken);

        return pairs.Select(p => (p.AmountType, p.AmountDescription)).ToList();
    }
}
