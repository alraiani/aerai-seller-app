using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Inventory;
using AERai.Web.Domain.Core;
using AERai.Web.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AERai.Web.Infrastructure.Tests;

/// <summary>SQL-backed tests for families, home stock, and how they appear in the inventory view.</summary>
public sealed class InventoryItemTests(SqlDatabaseFixture fixture) : IClassFixture<SqlDatabaseFixture>
{
    private async Task AddProductAsync(string sku)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Products.Add(new Product { Sku = sku, Title = $"Title {sku}", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
    }

    [SqlFact]
    public async Task HomeOnlySku_AppearsInViewWithFamilyAndNoSnapshot()
    {
        await AddProductAsync("T-HOME-1");
        await using var scope = fixture.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IInventoryItemRepository>();

        var familyId = await repository.GetOrCreateFamilyAsync("Test Mats", CancellationToken.None);
        Assert.Equal(familyId, await repository.GetOrCreateFamilyAsync("test mats", CancellationToken.None)); // case-insensitive
        await repository.SetFamilyAsync("T-HOME-1", familyId, CancellationToken.None);
        await repository.SetHomeStockAsync(MarketplaceIds.UnitedStates, [new HomeStockEntry("T-HOME-1", 25)], DateTimeOffset.UtcNow, "tests", CancellationToken.None);

        var position = await scope.ServiceProvider.GetRequiredService<AppDbContext>().InventoryPositions.AsNoTracking()
            .SingleAsync(p => p.Sku == "T-HOME-1" && p.MarketplaceId == MarketplaceIds.UnitedStates);

        Assert.Null(position.SnapshotDate);
        Assert.Equal((25, 0, "Test Mats"), (position.HomeStock, position.Available, position.Family));
    }

    [SqlFact]
    public async Task SetHomeStockAsync_UpdatesAndZeroRemovesThePosition()
    {
        await AddProductAsync("T-HOME-2");
        await using var scope = fixture.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IInventoryItemRepository>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await repository.SetHomeStockAsync(MarketplaceIds.Canada, [new HomeStockEntry("T-HOME-2", 5)], DateTimeOffset.UtcNow, "tests", CancellationToken.None);
        await repository.SetHomeStockAsync(MarketplaceIds.Canada, [new HomeStockEntry("T-HOME-2", 8)], DateTimeOffset.UtcNow, "tests", CancellationToken.None);
        Assert.Equal(8, (await repository.GetAsync("T-HOME-2", MarketplaceIds.Canada, CancellationToken.None))!.HomeStock);
        Assert.Equal(0, (await repository.GetAsync("T-HOME-2", MarketplaceIds.UnitedStates, CancellationToken.None))!.HomeStock);

        await repository.SetHomeStockAsync(MarketplaceIds.Canada, [new HomeStockEntry("T-HOME-2", 0)], DateTimeOffset.UtcNow, "tests", CancellationToken.None);

        Assert.False(await db.HomeStocks.AnyAsync(h => h.Sku == "T-HOME-2"));
        Assert.False(await db.InventoryPositions.AnyAsync(p => p.Sku == "T-HOME-2"));
    }

    [SqlFact]
    public async Task DeleteUnusedFamiliesAsync_KeepsFamiliesInUse()
    {
        await AddProductAsync("T-FAM-1");
        await using var scope = fixture.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IInventoryItemRepository>();
        var used = await repository.GetOrCreateFamilyAsync("Used family", CancellationToken.None);
        await repository.GetOrCreateFamilyAsync("Orphan family", CancellationToken.None);
        await repository.SetFamilyAsync("T-FAM-1", used, CancellationToken.None);

        await repository.DeleteUnusedFamiliesAsync(CancellationToken.None);

        var names = (await repository.ListFamiliesAsync(CancellationToken.None)).Select(f => f.Name).ToList();
        Assert.Contains("Used family", names);
        Assert.DoesNotContain("Orphan family", names);
    }

    [SqlFact]
    public async Task SetLeadTimesAsync_RoundTripsPerMarketplaceAndBlankDeletes()
    {
        await AddProductAsync("T-LEAD-1");
        await using var scope = fixture.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IInventoryItemRepository>();
        var queries = scope.ServiceProvider.GetRequiredService<IInventoryQueries>();
        var custom = new LeadTimeSettings(45, null, 12, 7, 120);

        await repository.SetLeadTimesAsync("T-LEAD-1", MarketplaceIds.Canada, custom, DateTimeOffset.UtcNow, "tests", CancellationToken.None);
        await repository.SetLeadTimesAsync("T-LEAD-1", MarketplaceIds.Canada, custom with { PrepTimeDays = 3 }, DateTimeOffset.UtcNow, "tests", CancellationToken.None);

        Assert.Equal(custom with { PrepTimeDays = 3 }, (await queries.GetLeadTimesAsync(MarketplaceIds.Canada, CancellationToken.None))["T-LEAD-1"]);
        Assert.Equal(LeadTimeSettings.None, (await repository.GetAsync("T-LEAD-1", MarketplaceIds.UnitedStates, CancellationToken.None))!.LeadTimes);

        await repository.SetLeadTimesAsync("T-LEAD-1", MarketplaceIds.Canada, LeadTimeSettings.None, DateTimeOffset.UtcNow, "tests", CancellationToken.None);

        Assert.False((await queries.GetLeadTimesAsync(MarketplaceIds.Canada, CancellationToken.None)).ContainsKey("T-LEAD-1"));
    }
}
