using AERai.Seller.Application.Abstractions;
using AERai.Seller.Domain.Ai;
using Microsoft.EntityFrameworkCore;

namespace AERai.Seller.Infrastructure.Repositories;

public sealed class ReplenishmentRecommendationRepository(IDbContextFactory<SellerDbContext> dbContextFactory)
    : IReplenishmentRecommendationRepository
{
    public async Task InsertAsync(IEnumerable<ReplenishmentRecommendation> recommendations, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        dbContext.ReplenishmentRecommendations.AddRange(recommendations);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ReplenishmentRecommendation>> GetLatestPerSkuAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        // ComputedAt is a DateTimeOffset column, and grouping/Max over one doesn't translate on
        // the current EF Core Sqlite provider (same gotcha as OrderRepository's PurchaseDate
        // filtering) — materialize first and pick the latest per SKU client-side.
        var recommendations = await dbContext.ReplenishmentRecommendations.AsNoTracking().ToListAsync(cancellationToken);

        return recommendations
            .GroupBy(r => r.Sku)
            .Select(g => g.OrderByDescending(r => r.ComputedAt).First())
            .ToList();
    }
}
