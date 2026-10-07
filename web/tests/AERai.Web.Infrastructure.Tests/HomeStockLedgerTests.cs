using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Application.Inventory;
using AERai.Web.Domain.Core;
using AERai.Web.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AERai.Web.Infrastructure.Tests;

/// <summary>SQL-backed tests that the home-stock ledger always adds up to the balance.</summary>
public sealed class HomeStockLedgerTests(SqlDatabaseFixture fixture) : IClassFixture<SqlDatabaseFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 15, 0, 0, TimeSpan.Zero);

    private async Task AddProductAsync(string sku)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Products.Add(new Product { Sku = sku, Title = $"Title {sku}", CreatedAt = Now, UpdatedAt = Now });
        await db.SaveChangesAsync();
    }

    private static HomeStockLedgerWrite Write(string sku, HomeStockMovementType type, int units) =>
        new(MarketplaceIds.UnitedStates, sku, type, units, Now, "PO-1", null, null, Now, "tests");

    private async Task<(int Balance, int LedgerSum, int Entries)> StateAsync(string sku)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var balance = await db.HomeStocks.Where(h => h.Sku == sku && h.MarketplaceId == MarketplaceIds.UnitedStates).SumAsync(h => h.Quantity);
        var entries = await db.HomeStockMovements.Where(m => m.Sku == sku && m.MarketplaceId == MarketplaceIds.UnitedStates).ToListAsync();
        return (balance, entries.Sum(e => e.Units), entries.Count);
    }

    [SqlFact]
    public async Task EveryKindOfWrite_KeepsTheLedgerEqualToTheBalance()
    {
        await AddProductAsync("T-LED-1");
        await using var scope = fixture.Services.CreateAsyncScope();
        var ledger = scope.ServiceProvider.GetRequiredService<IHomeStockLedgerRepository>();
        var items = scope.ServiceProvider.GetRequiredService<IInventoryItemRepository>();

        var (received, receivedId) = await ledger.RecordAsync(Write("T-LED-1", HomeStockMovementType.ReceivedFromSupplier, 100), CancellationToken.None);
        await ledger.RecordAsync(Write("T-LED-1", HomeStockMovementType.ShippedToAmazon, -40), CancellationToken.None);
        await items.SetHomeStockAsync(MarketplaceIds.UnitedStates, [new HomeStockEntry("T-LED-1", 55)], Now, "tests", CancellationToken.None);
        var (recount, _) = await ledger.RecordAsync(Write("T-LED-1", HomeStockMovementType.CountCorrection, 55), CancellationToken.None);

        Assert.Equal(HomeStockLedgerOutcome.Recorded, received);
        Assert.Equal(HomeStockLedgerOutcome.Unchanged, recount);
        Assert.Equal((55, 55, 3), await StateAsync("T-LED-1"));

        // Reversing the receipt would need 100 units back out, but only 55 are left.
        var (reverse, _) = await ledger.ReverseAsync(MarketplaceIds.UnitedStates, receivedId!.Value, Now, "tests", CancellationToken.None);
        Assert.Equal(HomeStockLedgerOutcome.WouldGoNegative, reverse);
    }

    [SqlFact]
    public async Task Reverse_UndoesOnceAndListsBothEntries()
    {
        await AddProductAsync("T-LED-2");
        await using var scope = fixture.Services.CreateAsyncScope();
        var ledger = scope.ServiceProvider.GetRequiredService<IHomeStockLedgerRepository>();

        var (_, id) = await ledger.RecordAsync(Write("T-LED-2", HomeStockMovementType.ReceivedFromSupplier, 20), CancellationToken.None);
        var (first, _) = await ledger.ReverseAsync(MarketplaceIds.UnitedStates, id!.Value, Now, "tests", CancellationToken.None);
        var (second, _) = await ledger.ReverseAsync(MarketplaceIds.UnitedStates, id.Value, Now, "tests", CancellationToken.None);

        Assert.Equal((HomeStockLedgerOutcome.Recorded, HomeStockLedgerOutcome.CannotReverse), (first, second));
        Assert.Equal((0, 0, 2), await StateAsync("T-LED-2"));

        var page = await ledger.ListAsync(MarketplaceIds.UnitedStates, new HomeStockLedgerFilter(Sku: "T-LED-2"), new PageRequest(), CancellationToken.None);
        Assert.Equal([(-20, 0), (20, 20)], page.Items.Select(e => (e.Units, e.BalanceAfter)));
        Assert.False(page.Items[1].CanReverse);
        Assert.Equal(page.Items[0].Id, page.Items[1].ReversedById);
    }

    [SqlFact]
    public async Task Record_ShippingMoreThanOnHand_LeavesNothingBehind()
    {
        await AddProductAsync("T-LED-3");
        await using var scope = fixture.Services.CreateAsyncScope();
        var ledger = scope.ServiceProvider.GetRequiredService<IHomeStockLedgerRepository>();

        var (outcome, _) = await ledger.RecordAsync(Write("T-LED-3", HomeStockMovementType.ShippedToAmazon, -1), CancellationToken.None);

        Assert.Equal(HomeStockLedgerOutcome.WouldGoNegative, outcome);
        Assert.Equal((0, 0, 0), await StateAsync("T-LED-3"));
    }
}
