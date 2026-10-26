using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Ingestion;
using AERai.Web.Domain.Core;
using AERai.Web.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AERai.Web.Infrastructure.Tests;

/// <summary>SQL-backed tests for <c>core.AdSpend</c> writes.</summary>
public sealed class AdSpendTests(SqlDatabaseFixture fixture) : IClassFixture<SqlDatabaseFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);

    private static AdSpendRow Row(DateOnly date, string sku, string campaignId, decimal cost, string? currency = null) =>
        new(date, sku, "B0TEST", campaignId, "Campaign " + campaignId, 100, 3, cost, 20m, 1, 1, currency);

    private async Task<int> ReplaceAsync(string marketplaceId, DateOnly start, DateOnly end, params AdSpendRow[] rows)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IAdSpendRepository>()
            .ReplaceRangeAsync(marketplaceId, start, end, rows, "USD", Now, CancellationToken.None);
    }

    [SqlFact]
    public async Task ReplaceRangeAsync_OverlappingRange_ReplacesOnlyItsDaysAndAddsUnknownSkusToCatalog()
    {
        var d1 = new DateOnly(2026, 9, 1);
        var d2 = new DateOnly(2026, 9, 2);
        var d3 = new DateOnly(2026, 9, 3);
        await ReplaceAsync(MarketplaceIds.UnitedStates, d1, d2, Row(d1, "T-AD-1", "1", 1.00m), Row(d2, "T-AD-1", "1", 2.00m), Row(d2, "T-AD-2", "2", 3.00m));

        // The second report covers days 2 and 3: day 2 is replaced (T-AD-2 no longer shows), day 1 is kept.
        var written = await ReplaceAsync(MarketplaceIds.UnitedStates, d2, d3, Row(d2, "t-ad-1", "1", 2.50m), Row(d3, "T-AD-3", "1", 4.00m, "CAD"));

        Assert.Equal(2, written);
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rows = await db.AdSpends.AsNoTracking()
            .Where(s => s.MarketplaceId == MarketplaceIds.UnitedStates && EF.Functions.Like(s.Sku, "T-AD-%"))
            .OrderBy(s => s.Date).ThenBy(s => s.Sku)
            .ToListAsync();
        Assert.Equal(
            [(d1, "T-AD-1", 1.00m, "USD"), (d2, "T-AD-1", 2.50m, "USD"), (d3, "T-AD-3", 4.00m, "CAD")],
            rows.Select(r => (r.Date, r.Sku, r.Cost, r.Currency)));
        Assert.True(await db.Products.AnyAsync(p => p.Sku == "T-AD-3" && p.Asin == "B0TEST"));
    }

    [SqlFact]
    public async Task ReplaceRangeAsync_RowOutsideTheRange_IsRefused()
    {
        var day = new DateOnly(2026, 9, 10);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            ReplaceAsync(MarketplaceIds.Canada, day, day, Row(day.AddDays(1), "T-AD-X", "1", 1m)));
    }
}
