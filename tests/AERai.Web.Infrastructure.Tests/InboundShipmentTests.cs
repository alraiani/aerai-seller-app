using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Ingestion;
using AERai.Web.Application.Inventory;
using AERai.Web.Domain.Core;
using AERai.Web.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AERai.Web.Infrastructure.Tests;

/// <summary>SQL-backed tests of Amazon inbound shipments: how the sync records them and how a review logs one.</summary>
/// <remarks>A marketplace's first read sets already-shipped shipments aside, so each test uses its own marketplace.</remarks>
public sealed class InboundShipmentTests(SqlDatabaseFixture fixture) : IClassFixture<SqlDatabaseFixture>
{
    private const string User = "tests@aeraigroup.com";

    private static readonly DateTimeOffset Now = new(2026, 10, 28, 15, 0, 0, TimeSpan.Zero);

    private static AmazonInboundShipment Shipment(string fba, string status, params (string Sku, int Quantity)[] items) =>
        new(fba, $"sh-{fba}", $"wf-{fba}", $"Plan {fba}", Now.AddDays(-1), null, status, items.Select(i => new AmazonInboundItem(i.Sku, i.Quantity)).ToList());

    private async Task<InboundApplyResult> ApplyAsync(string marketplaceId, params AmazonInboundShipment[] shipments)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IInboundShipmentRepository>().ApplyAsync(marketplaceId, shipments, Now, CancellationToken.None);
    }

    private async Task<List<InboundShipment>> StoredAsync(string marketplaceId)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().InboundShipments.AsNoTracking().Include(s => s.Items)
            .Where(s => s.MarketplaceId == marketplaceId)
            .OrderBy(s => s.ShipmentConfirmationId)
            .ToListAsync();
    }

    /// <summary>Adds SKUs in a new family that ships from home stock, each with units received at home.</summary>
    private async Task AddHomeSkusAsync(string marketplaceId, string family, params (string Sku, int AtHome)[] skus)
    {
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = new ProductFamily { Name = family, ShipsFromHomeStock = true };
            db.ProductFamilies.Add(row);
            await db.SaveChangesAsync();
            db.Products.AddRange(skus.Select(s => new Product { Sku = s.Sku, Title = $"Title {s.Sku}", FamilyId = row.Id, CreatedAt = Now, UpdatedAt = Now }));
            await db.SaveChangesAsync();
        }

        foreach (var (sku, atHome) in skus.Where(s => s.AtHome > 0))
        {
            await using var scope = fixture.Services.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<IHomeStockLedgerService>()
                .RecordAsync(marketplaceId, new HomeStockMovementInput(sku, HomeStockMovementType.ReceivedFromSupplier, atHome, null, null, null), User, CancellationToken.None);
            Assert.True(result.IsSuccess, result.Error);
        }
    }

    [SqlFact]
    public async Task ApplyAsync_FirstRead_SetsAsideShipmentsThatAlreadyLeftAndOffersTheRest()
    {
        var first = await ApplyAsync(MarketplaceIds.UnitedKingdom,
            Shipment("FBA-UK-1", "WORKING", ("T-UK-1", 10)),
            Shipment("FBA-UK-2", "IN_TRANSIT", ("T-UK-1", 20)));
        var stored = await StoredAsync(MarketplaceIds.UnitedKingdom);

        Assert.Equal((2, 2, 1, 1), (first.Shipments, first.New, first.NewToReview, first.SetAside));
        Assert.Equal(
            [("FBA-UK-1", InboundShipmentReview.ToReview, (string?)null), ("FBA-UK-2", InboundShipmentReview.Dismissed, InboundShipment.SystemReviewer)],
            stored.Select(s => (s.ShipmentConfirmationId, s.Review, s.ReviewedBy)));

        // Later reads offer even a shipment that has already left, refresh known ones, and replace their SKUs.
        var later = await ApplyAsync(MarketplaceIds.UnitedKingdom,
            Shipment("FBA-UK-1", "READY_TO_SHIP", ("T-UK-1", 12), ("T-UK-2", 4)),
            Shipment("FBA-UK-3", "SHIPPED", ("T-UK-2", 5)));
        stored = await StoredAsync(MarketplaceIds.UnitedKingdom);

        Assert.Equal((1, 1, 0), (later.New, later.NewToReview, later.SetAside));
        Assert.Equal(InboundShipmentReview.ToReview, stored.Single(s => s.ShipmentConfirmationId == "FBA-UK-3").Review);
        var updated = stored.Single(s => s.ShipmentConfirmationId == "FBA-UK-1");
        Assert.Equal("READY_TO_SHIP", updated.AmazonStatus);
        Assert.Equal([("T-UK-1", 12), ("T-UK-2", 4)], updated.Items.OrderBy(i => i.Sku).Select(i => (i.Sku, i.Quantity)));
    }

    [SqlFact]
    public async Task ApplyAsync_LoggedByHandCancelledAndCount_FollowTheLedgerAndTheFamilySwitch()
    {
        await AddHomeSkusAsync(MarketplaceIds.UnitedStates, "T-INB Hankies", ("T-INB-1", 50));

        // Logged by hand on the Worksheet with the FBA id, before the sync ever saw the shipment.
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var sent = await scope.ServiceProvider.GetRequiredService<IHomeStockLedgerService>()
                .ShipToAmazonAsync(MarketplaceIds.UnitedStates, [new HomeStockShipment("T-INB-1", 50, 20)], "FBA-T1", User, CancellationToken.None);
            Assert.Equal(1, sent.Value.Applied);
        }

        var result = await ApplyAsync(MarketplaceIds.UnitedStates,
            Shipment("FBA-T1", "SHIPPED", ("T-INB-1", 20)),
            Shipment("FBA-T2", "WORKING", ("T-INB-1", 10)),
            Shipment("FBA-T3", "WORKING", ("T-INB-NOFAMILY", 10)));

        Assert.Equal((3, 1, 2), (result.New, result.AlreadyLogged, result.NewToReview));
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IInboundShipmentRepository>();

            // FBA-T3 holds no SKU from home stock, so it isn't counted.
            Assert.Equal(1, await repository.CountToReviewAsync(MarketplaceIds.UnitedStates, CancellationToken.None));
            var logged = await repository.GetLoggedUnitsAsync(MarketplaceIds.UnitedStates, ["FBA-T1", "FBA-T2"], CancellationToken.None);
            Assert.Equal(20, Assert.Single(logged["fba-t1"]).Value);
            Assert.False(logged.ContainsKey("FBA-T2"));
            var info = await repository.GetSkuInfoAsync(MarketplaceIds.UnitedStates, ["t-inb-1", "T-INB-NOFAMILY"], CancellationToken.None);
            Assert.Equal(("T-INB-1", true, 30, "T-INB Hankies"), (info["T-INB-1"].Sku, info["T-INB-1"].ShipsFromHomeStock, info["T-INB-1"].HomeStock, info["T-INB-1"].FamilyName));
            Assert.False(info.ContainsKey("T-INB-NOFAMILY"));
        }

        var cancelled = await ApplyAsync(MarketplaceIds.UnitedStates, Shipment("FBA-T2", "CANCELLED", ("T-INB-1", 10)));
        var stored = await StoredAsync(MarketplaceIds.UnitedStates);

        Assert.Equal(1, cancelled.Cancelled);
        Assert.Equal(
            [InboundShipmentReview.AlreadyLogged, InboundShipmentReview.CancelledByAmazon, InboundShipmentReview.ToReview],
            stored.Where(s => s.ShipmentConfirmationId.StartsWith("FBA-T", StringComparison.Ordinal)).Select(s => s.Review));
    }

    [SqlFact]
    public async Task AcceptAsync_LogsTheSendUnderTheFbaIdSoItAwaitsAmazon()
    {
        await AddHomeSkusAsync(MarketplaceIds.Canada, "T-ACC Hankies", ("T-ACC-1", 40), ("T-ACC-2", 15));
        await ApplyAsync(MarketplaceIds.Canada, Shipment("FBA-CA-1", "WORKING", ("T-ACC-1", 25), ("T-ACC-2", 15)));

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IInboundShipmentService>();
            var row = Assert.Single((await service.ListAsync(MarketplaceIds.Canada, new InboundShipmentFilter(), CancellationToken.None)).Shipments);
            Assert.Equal([25, 15], row.Lines.Select(l => l.Suggested));

            var result = await service.AcceptAsync(MarketplaceIds.Canada, row.Id, row.Lines.Select(l => new HomeStockShipment(l.Sku, l.HomeStock, l.Suggested)).ToList(), User, CancellationToken.None);

            Assert.True(result.IsSuccess, result.Error);
            Assert.Equal((2, 40), (result.Value.Applied, result.Value.UnitsOut));
        }

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var sends = await db.HomeStockMovements.AsNoTracking()
                .Where(m => m.MarketplaceId == MarketplaceIds.Canada && m.Type == HomeStockMovementType.ShippedToAmazon)
                .OrderBy(m => m.Sku)
                .Select(m => new { m.Sku, m.Units, m.Reference })
                .ToListAsync();
            Assert.Equal([("T-ACC-1", -25, "FBA-CA-1"), ("T-ACC-2", -15, "FBA-CA-1")], sends.Select(s => (s.Sku, s.Units, s.Reference!)));
            Assert.Equal(15, await db.HomeStocks.Where(h => h.MarketplaceId == MarketplaceIds.Canada && h.Sku == "T-ACC-1").Select(h => h.Quantity).SingleAsync());
            Assert.Equal(InboundShipmentReview.Logged, (await StoredAsync(MarketplaceIds.Canada)).Single().Review);

            var pending = await db.InventoryPositions.AsNoTracking().SingleAsync(p => p.MarketplaceId == MarketplaceIds.Canada && p.Sku == "T-ACC-1");
            Assert.Equal(25, pending.PendingToAmazon);
        }
    }
}
