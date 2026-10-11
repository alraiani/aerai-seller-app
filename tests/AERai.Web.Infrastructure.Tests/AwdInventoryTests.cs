using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Ingestion;
using AERai.Web.Domain.Core;
using AERai.Web.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AERai.Web.Infrastructure.Tests;

/// <summary>SQL-backed tests for AWD snapshots and how they appear in <c>rpt.vw_InventoryPosition</c>.</summary>
/// <remarks>The view reads each marketplace's latest AWD date, so each test uses its own marketplace.</remarks>
public sealed class AwdInventoryTests(SqlDatabaseFixture fixture) : IClassFixture<SqlDatabaseFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);

    private async Task ReplaceAsync(string marketplaceId, DateOnly date, params AwdInventoryItem[] items)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IAwdInventoryRepository>()
            .ReplaceSnapshotAsync(marketplaceId, date, items, Now, CancellationToken.None);
    }

    private async Task<List<Domain.Reporting.InventoryPosition>> PositionsAsync(string marketplaceId)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().InventoryPositions.AsNoTracking()
            .Where(p => p.MarketplaceId == marketplaceId)
            .ToListAsync();
    }

    [SqlFact]
    public async Task ReplaceSnapshotAsync_SameDateTwice_ReplacesRowsAndAddsUnknownSkusToCatalog()
    {
        var date = new DateOnly(2026, 10, 9);
        await ReplaceAsync(MarketplaceIds.UnitedStates, date, new AwdInventoryItem("T-AWD-1", 100, 20, 90, 10, 5), new AwdInventoryItem("T-AWD-2", 7, 0, 7, 0, 0));
        await ReplaceAsync(MarketplaceIds.UnitedStates, date, new AwdInventoryItem("T-AWD-1", 80, 0, 80, 0, 12));

        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rows = await db.AwdInventorySnapshots.AsNoTracking().Where(s => s.MarketplaceId == MarketplaceIds.UnitedStates).ToListAsync();
        var row = Assert.Single(rows);
        Assert.Equal(("T-AWD-1", 80, 0, 12), (row.Sku, row.OnHand, row.Inbound, row.Replenishment));
        Assert.True(await db.Products.AnyAsync(p => p.Sku == "T-AWD-2"));

        // An AWD-only SKU still has a position, with no FBA snapshot.
        var position = Assert.Single(await PositionsAsync(MarketplaceIds.UnitedStates), p => p.Sku == "T-AWD-1");
        Assert.Null(position.SnapshotDate);
        Assert.Equal((0, 80, 0, 12, 80), (position.AmazonTotal, position.AwdOnHand, position.AwdInbound, position.AwdReplenishment, position.OverallTotal));
        Assert.DoesNotContain(await PositionsAsync(MarketplaceIds.UnitedStates), p => p.Sku == "T-AWD-2");
    }

    [SqlFact]
    public async Task View_UsesLatestAwdDateAndKeepsFbaAndHomeStock()
    {
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Products.Add(new Product { Sku = "T-AWD-FBA", CreatedAt = Now, UpdatedAt = Now });
            await db.SaveChangesAsync();
            db.InventorySnapshots.Add(new InventorySnapshot
            {
                MarketplaceId = MarketplaceIds.Canada, Sku = "T-AWD-FBA", SnapshotDate = new DateOnly(2026, 10, 8), State = InventoryStates.Available, Quantity = 7,
            });
            db.HomeStocks.Add(new HomeStock { MarketplaceId = MarketplaceIds.Canada, Sku = "T-AWD-FBA", Quantity = 3, UpdatedAt = Now, UpdatedBy = "tests" });
            await db.SaveChangesAsync();
        }

        await ReplaceAsync(MarketplaceIds.Canada, new DateOnly(2026, 10, 7), new AwdInventoryItem("T-AWD-GONE", 50, 0, 50, 0, 0), new AwdInventoryItem("T-AWD-FBA", 1, 1, 1, 0, 0));
        await ReplaceAsync(MarketplaceIds.Canada, new DateOnly(2026, 10, 8), new AwdInventoryItem("T-AWD-FBA", 40, 10, 40, 0, 6));

        var positions = await PositionsAsync(MarketplaceIds.Canada);

        // A SKU missing from the latest AWD listing has left AWD; its older row does not linger.
        Assert.DoesNotContain(positions, p => p.Sku == "T-AWD-GONE");
        var position = Assert.Single(positions, p => p.Sku == "T-AWD-FBA");
        Assert.Equal(new DateOnly(2026, 10, 8), position.SnapshotDate);
        Assert.Equal((7, 3, 40, 10, 6), (position.Available, position.HomeStock, position.AwdOnHand, position.AwdInbound, position.AwdReplenishment));
        Assert.Equal(7 + 50 + 3, position.OverallTotal);
    }
}
