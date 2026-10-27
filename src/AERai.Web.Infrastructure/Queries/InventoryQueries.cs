using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Inventory;
using AERai.Web.Domain.Reporting;
using AERai.Web.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AERai.Web.Infrastructure.Queries;

/// <summary>
/// EF Core implementation of <see cref="IInventoryQueries"/>. Returns rows only; velocity and days of
/// inventory are computed by <see cref="InventoryService"/>.
/// </summary>
/// <param name="dbContext">Scoped database context.</param>
internal sealed class InventoryQueries(AppDbContext dbContext) : IInventoryQueries
{
    /// <inheritdoc/>
    public async Task<IReadOnlyList<InventoryPosition>> GetPositionsAsync(string marketplaceId, CancellationToken cancellationToken) =>
        await dbContext.InventoryPositions
            .AsNoTracking()
            .Where(p => p.MarketplaceId == marketplaceId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<UnitsSold>> GetUnitsSoldAsync(string marketplaceId, DateTimeOffset since, CancellationToken cancellationToken) =>
        await dbContext.SalesLines
            .AsNoTracking()
            .Where(l => l.MarketplaceId == marketplaceId && l.PurchaseDate >= since)
            .Select(l => new UnitsSold(l.Sku, l.PurchaseDate, l.Quantity))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, LeadTimeSettings>> GetLeadTimesAsync(string marketplaceId, CancellationToken cancellationToken) =>
        await dbContext.LeadTimeProfiles
            .AsNoTracking()
            .Where(p => p.MarketplaceId == marketplaceId)
            .ToDictionaryAsync(
                p => p.Sku,
                p => new LeadTimeSettings(p.SupplierLeadTimeDays, p.PrepTimeDays, p.TransitDays, p.SafetyStockDays, p.TargetStockDays),
                StringComparer.Ordinal,
                cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MonthlyUnitsSold>> GetMonthlyUnitsSoldAsync(string marketplaceId, DateTimeOffset since, CancellationToken cancellationToken) =>
        await dbContext.SalesLines
            .AsNoTracking()
            .Where(l => l.MarketplaceId == marketplaceId && l.PurchaseDate >= since)
            .GroupBy(l => new { l.Sku, l.PurchaseDate.Year, l.PurchaseDate.Month })
            .Select(g => new MonthlyUnitsSold(g.Key.Sku, g.Key.Year, g.Key.Month, g.Sum(l => l.Quantity)))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<int, RestockRules>> GetFamilyRulesAsync(CancellationToken cancellationToken) =>
        await dbContext.ProductFamilies
            .AsNoTracking()
            .Where(f => f.SendMultiple != null || f.SendMultipleLarge != null || f.OrderMinimum != null || f.OrderMultiple != null)
            .ToDictionaryAsync(
                f => f.Id,
                f => new RestockRules(f.SendMultiple, f.SendMultipleLarge, f.SendLargeFrom, f.OrderMinimum, f.OrderMultiple),
                cancellationToken)
            .ConfigureAwait(false);
}
