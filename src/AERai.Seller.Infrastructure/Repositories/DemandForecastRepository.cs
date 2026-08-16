using AERai.Seller.Application.Abstractions;
using AERai.Seller.Domain.Ai;
using Microsoft.EntityFrameworkCore;

namespace AERai.Seller.Infrastructure.Repositories;

public sealed class DemandForecastRepository(IDbContextFactory<SellerDbContext> dbContextFactory) : IDemandForecastRepository
{
    public async Task InsertAsync(IEnumerable<DemandForecast> forecasts, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        dbContext.DemandForecasts.AddRange(forecasts);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DemandForecast>> GetLatestPerSkuAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        // ComputedAt is a DateTimeOffset column, and grouping/Max over one doesn't translate on
        // the current EF Core Sqlite provider (same gotcha as OrderRepository's PurchaseDate
        // filtering) — materialize first and pick the latest per SKU client-side.
        var forecasts = await dbContext.DemandForecasts.AsNoTracking().ToListAsync(cancellationToken);

        return forecasts
            .GroupBy(f => f.Sku)
            .Select(g => g.OrderByDescending(f => f.ComputedAt).First())
            .ToList();
    }
}
