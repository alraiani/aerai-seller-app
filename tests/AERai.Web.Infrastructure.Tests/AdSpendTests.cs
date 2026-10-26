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

    [SqlFact]
    public async Task Queries_AddUpEachSkusDaysWithItsFamilyAndSalesInTheRange()
    {
        var day = new DateOnly(2026, 8, 10);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var family = new ProductFamily { Name = "T-AD Family" };
            db.ProductFamilies.Add(family);
            await db.SaveChangesAsync();
            db.Products.Add(new Product { Sku = "T-ADQ-1", Title = "Towel", FamilyId = family.Id, CreatedAt = Now, UpdatedAt = Now });
            await db.SaveChangesAsync();
        }

        await ReplaceAsync(MarketplaceIds.Canada, day, day.AddDays(1),
            Row(day, "T-ADQ-1", "1", 1.25m), Row(day.AddDays(1), "T-ADQ-1", "2", 2.00m), Row(day, "T-ADQ-2", "1", 0.50m));

        await using var queryScope = fixture.Services.CreateAsyncScope();
        var queries = queryScope.ServiceProvider.GetRequiredService<IAdvertisingQueries>();
        var rows = await queries.GetSkuSpendAsync(MarketplaceIds.Canada, day, day.AddDays(1), CancellationToken.None);
        var towel = Assert.Single(rows, r => r.Sku == "T-ADQ-1");
        Assert.Equal(("Towel", "T-AD Family", 2, 3.25m, 6L), (towel.Title, towel.Family, towel.Campaigns, towel.Metrics.Spend, towel.Metrics.Clicks));
        Assert.Null(Assert.Single(rows, r => r.Sku == "T-ADQ-2").Family);

        // Only the first day.
        var oneDay = await queries.GetSkuSpendAsync(MarketplaceIds.Canada, day, day, CancellationToken.None);
        Assert.Equal(1.25m, Assert.Single(oneDay, r => r.Sku == "T-ADQ-1").Metrics.Spend);
        var synced = await queries.GetSpendRangeAsync(MarketplaceIds.Canada, CancellationToken.None);
        Assert.NotNull(synced);
        Assert.True(synced.Value.First <= day);
        Assert.Equal(day.AddDays(1), synced.Value.Last);
    }
}
