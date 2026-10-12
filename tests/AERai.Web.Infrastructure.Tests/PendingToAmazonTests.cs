using System.Text;
using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Imports;
using AERai.Web.Application.Inventory;
using AERai.Web.Domain.Core;
using AERai.Web.Domain.Reporting;
using AERai.Web.Domain.Staging;
using AERai.Web.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AERai.Web.Infrastructure.Tests;

/// <summary>
/// SQL-backed tests of sends pending to Amazon: the inventory view counts them until a promoted FBA
/// snapshot shows the units, and reversed sends never count.
/// </summary>
public sealed class PendingToAmazonTests(SqlDatabaseFixture fixture) : IClassFixture<SqlDatabaseFixture>
{
    private const string User = "tests@aeraigroup.com";

    private const string MyiHeader =
        "sku\tasin\tproduct-name\tafn-fulfillable-quantity\tafn-unsellable-quantity\tafn-reserved-quantity\tafn-inbound-working-quantity\tafn-inbound-shipped-quantity\tafn-inbound-receiving-quantity\n";

    private async Task PromoteFbaAsync(string rows)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var bytes = Encoding.UTF8.GetBytes(MyiHeader + rows);
        var staged = await scope.ServiceProvider.GetRequiredService<IStagingImportService>()
            .ImportAsync(new ImportFileCommand(ImportSource.FbaInventory, MarketplaceIds.UnitedStates, "myi.tsv", bytes.Length, new MemoryStream(bytes), User), CancellationToken.None);
        Assert.True(staged.IsSuccess, staged.Error);

        var promoted = await scope.ServiceProvider.GetRequiredService<IPromotionService>().PromoteAsync(staged.Value.BatchId, CancellationToken.None);
        Assert.True(promoted.IsSuccess, promoted.Error);
    }

    private async Task AddProductAsync(string sku)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Products.Add(new Product { Sku = sku, Title = $"Title {sku}", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
    }

    private async Task ReceiveAsync(string sku, int units)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IHomeStockLedgerService>()
            .RecordAsync(MarketplaceIds.UnitedStates, new HomeStockMovementInput(sku, HomeStockMovementType.ReceivedFromSupplier, units, null, null, null), User, CancellationToken.None);
        Assert.True(result.IsSuccess, result.Error);
    }

    private async Task SendAsync(string sku, int current, int units)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IHomeStockLedgerService>()
            .ShipToAmazonAsync(MarketplaceIds.UnitedStates, [new HomeStockShipment(sku, current, units)], null, User, CancellationToken.None);
        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(1, result.Value.Applied);
    }

    private async Task<InventoryPosition> PositionAsync(string sku)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.InventoryPositions.AsNoTracking().SingleAsync(p => p.Sku == sku && p.MarketplaceId == MarketplaceIds.UnitedStates);
    }

    private async Task<HomeStockMovement> LastSendAsync(string sku)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.HomeStockMovements.AsNoTracking()
            .Where(m => m.Sku == sku && m.Type == HomeStockMovementType.ShippedToAmazon && m.Units < 0)
            .OrderByDescending(m => m.Id)
            .FirstAsync();
    }

    [SqlFact]
    public async Task Send_CountsAsPendingUntilAPromotedSnapshotShowsIt()
    {
        await PromoteFbaAsync("T-PND-1\tB0PND00001\tWidget\t40\t0\t0\t0\t0\t0\n");
        await ReceiveAsync("T-PND-1", 100);

        await SendAsync("T-PND-1", 100, 24);

        var sent = await LastSendAsync("T-PND-1");
        var pending = await PositionAsync("T-PND-1");
        Assert.Equal((40, (int?)40), (pending.SellThroughStock, sent.AmazonBaselineUnits));
        Assert.Equal((24, 76, 140), (pending.PendingToAmazon, pending.HomeStock, pending.OverallTotal));
        Assert.NotNull(pending.OldestPendingAt);

        // Amazon now shows the 24 units in transit.
        await PromoteFbaAsync("T-PND-1\tB0PND00001\tWidget\t40\t0\t0\t0\t24\t0\n");

        var cleared = await PositionAsync("T-PND-1");
        var closed = await LastSendAsync("T-PND-1");
        Assert.Equal((0, 64, 140), (cleared.PendingToAmazon, cleared.SellThroughStock, cleared.OverallTotal));
        Assert.Equal((24, HomeStockMovement.SystemReconciler), (closed.ReconciledUnits, closed.ReconciledBy));
        Assert.NotNull(closed.ReconciledAt);
    }

    [SqlFact]
    public async Task Send_AmazonShowsPartOfIt_KeepsTheRestPending()
    {
        await PromoteFbaAsync("T-PND-2\tB0PND00002\tWidget\t10\t0\t0\t0\t0\t0\n");
        await ReceiveAsync("T-PND-2", 50);
        await SendAsync("T-PND-2", 50, 30);

        await PromoteFbaAsync("T-PND-2\tB0PND00002\tWidget\t10\t0\t0\t0\t20\t0\n");

        Assert.Equal(10, (await PositionAsync("T-PND-2")).PendingToAmazon);
        Assert.Equal((20, (DateTimeOffset?)null), ((await LastSendAsync("T-PND-2")).ReconciledUnits, (await LastSendAsync("T-PND-2")).ReconciledAt));
    }

    [SqlFact]
    public async Task Send_Cancelled_IsNoLongerPendingAndUnitsComeHome()
    {
        await AddProductAsync("T-PND-3");
        await ReceiveAsync("T-PND-3", 20);
        await SendAsync("T-PND-3", 20, 20);
        var sent = await LastSendAsync("T-PND-3");

        // With nothing at home and nothing at Amazon, the pending send alone keeps the SKU in the view.
        Assert.Equal((20, 0), ((await PositionAsync("T-PND-3")).PendingToAmazon, (await PositionAsync("T-PND-3")).HomeStock));

        await using var scope = fixture.Services.CreateAsyncScope();
        var cancelled = await scope.ServiceProvider.GetRequiredService<IPendingSendService>().CancelAsync(MarketplaceIds.UnitedStates, sent.Id, User, CancellationToken.None);

        Assert.True(cancelled.IsSuccess, cancelled.Error);
        var position = await PositionAsync("T-PND-3");
        Assert.Equal((0, 20), (position.PendingToAmazon, position.HomeStock));
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<IPendingSendService>().ListAsync(MarketplaceIds.UnitedStates, null, CancellationToken.None));
    }
}
