using AERai.Web.Application.Inventory;
using AERai.Web.Application.Tests.Fakes;
using AERai.Web.Domain.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace AERai.Web.Application.Tests.Inventory;

public sealed class InboundShipmentServiceTests
{
    private const string Us = "ATVPDKIKX0DER";
    private const string User = "ops@example.com";
    private const string Fba = "FBA15ABCD";

    private readonly FakeInboundShipmentRepository _repository = new();
    private readonly FakeHomeStockLedgerRepository _ledgerRepository = new();
    private readonly FakeInventoryQueries _inventory = new();
    private readonly FakeInventoryRefreshRequester _refresh = new();
    private readonly FakeStockAlertRefreshSignal _alerts = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 28, 15, 0, 0, TimeSpan.Zero));

    public InboundShipmentServiceTests()
    {
        // Handkerchiefs ship from home; the mat comes straight from the supplier.
        _repository.Skus["HK-BL"] = new InboundSkuInfo("HK-BL", "Blue hanky", 1, "Handkerchiefs", true, 40);
        _repository.Skus["HK-PK"] = new InboundSkuInfo("HK-PK", "Pink hanky", 1, "Handkerchiefs", true, 8);
        _repository.Skus["MAT-1"] = new InboundSkuInfo("MAT-1", "Mat", 2, "Mats", false, 0);
        _ledgerRepository.Skus.UnionWith(["HK-BL", "HK-PK", "MAT-1"]);

        // What the ledger has logged under each reference, read from the fake ledger itself.
        _repository.Logged = reference => _ledgerRepository.Entries
            .Where(e => e.Type == HomeStockMovementType.ShippedToAmazon && e.Reference == reference)
            .GroupBy(e => e.Sku, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => -g.Sum(e => e.Units), StringComparer.OrdinalIgnoreCase);

        _repository.Shipments.Add(Shipment(1, Fba, ("HK-BL", 25), ("HK-PK", 10), ("MAT-1", 50)));
    }

    private InboundShipmentService CreateService()
    {
        var ledger = new HomeStockLedgerService(_ledgerRepository, _inventory, _refresh, _alerts, _clock, NullLogger<HomeStockLedgerService>.Instance);
        return new InboundShipmentService(_repository, ledger, _clock, NullLogger<InboundShipmentService>.Instance);
    }

    private static InboundShipment Shipment(long id, string fba, params (string Sku, int Quantity)[] items) => new()
    {
        Id = id,
        MarketplaceId = Us,
        ShipmentConfirmationId = fba,
        AmazonShipmentId = $"sh{id}",
        InboundPlanId = $"wf{id}",
        PlanName = $"Plan {id}",
        AmazonStatus = "WORKING",
        FirstSeenAt = new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero),
        Items = items.Select(i => new InboundShipmentItem { Sku = i.Sku, Quantity = i.Quantity }).ToList(),
    };

    private Task<AERai.Web.Application.Common.Result<string>> StockAsync(string sku, int units) =>
        new HomeStockLedgerService(_ledgerRepository, _inventory, _refresh, _alerts, _clock, NullLogger<HomeStockLedgerService>.Instance)
            .RecordAsync(Us, new HomeStockMovementInput(sku, HomeStockMovementType.ReceivedFromSupplier, units, null, null, null), User, CancellationToken.None);

    [Fact]
    public async Task ListAsync_SuggestsAmazonsUnitsUpToHomeStockAndOnlyForSkusFromHome()
    {
        var list = await CreateService().ListAsync(Us, new InboundShipmentFilter(), CancellationToken.None);

        var row = Assert.Single(list.Shipments);
        Assert.True(row.IsToReview);
        Assert.Equal(
            [("HK-BL", true, 25), ("HK-PK", true, 8), ("MAT-1", false, 0)],
            row.Lines.Select(l => (l.Sku, l.FromHome, l.Suggested)));
        Assert.True(row.Lines.Single(l => l.Sku == "HK-PK").ShortAtHome);
        Assert.Equal((1, 0, 0, 1), (list.ToReview, list.Logged, list.SetAside, list.All));
    }

    [Fact]
    public async Task ListAsync_ShipmentWithNoSkuFromHome_IsSetAsideNotToReview()
    {
        _repository.Shipments.Add(Shipment(2, "FBA15MATS", ("MAT-1", 40)));

        var service = CreateService();
        var toReview = await service.ListAsync(Us, new InboundShipmentFilter(), CancellationToken.None);
        var setAside = await service.ListAsync(Us, new InboundShipmentFilter(InboundShipmentView.SetAside), CancellationToken.None);
        var byFamily = await service.ListAsync(Us, new InboundShipmentFilter(InboundShipmentView.All, FamilyId: 2), CancellationToken.None);
        var bySearch = await service.ListAsync(Us, new InboundShipmentFilter(InboundShipmentView.All, Search: "pink hanky"), CancellationToken.None);

        Assert.Equal([1L], toReview.Shipments.Select(s => s.Id));
        Assert.Equal([2L], setAside.Shipments.Select(s => s.Id));
        Assert.Equal([1L, 2L], byFamily.Shipments.Select(s => s.Id));
        Assert.Equal([1L], bySearch.Shipments.Select(s => s.Id));
        Assert.Equal(1, await service.CountToReviewAsync(Us, CancellationToken.None));
    }

    [Fact]
    public async Task AcceptAsync_LogsTheEnteredUnitsAsShippedToAmazonUnderTheFbaIdAndMarksItLogged()
    {
        await StockAsync("HK-BL", 40);
        await StockAsync("HK-PK", 8);

        // The pink count was lowered to what came from home; the mat isn't from home, so its line is ignored.
        var result = await CreateService().AcceptAsync(Us, 1, [new("HK-BL", 40, 25), new("hk-pk", 8, 6), new("MAT-1", 0, 50)], User, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal((2, 31), (result.Value.Applied, result.Value.UnitsOut));
        var sends = _ledgerRepository.Entries.Where(e => e.Type == HomeStockMovementType.ShippedToAmazon).ToList();
        Assert.Equal([("HK-BL", -25, Fba), ("HK-PK", -6, Fba)], sends.Select(e => (e.Sku, e.Units, e.Reference)));
        Assert.Equal(15, _ledgerRepository.Balance(Us, "HK-BL"));
        Assert.Equal(InboundShipmentReview.Logged, _repository.Shipments[0].Review);
        Assert.Equal(User, _repository.Shipments[0].ReviewedBy);
        Assert.Single(_refresh.Requests);
    }

    [Fact]
    public async Task AcceptAsync_Twice_NeverLogsASkuTwice()
    {
        await StockAsync("HK-BL", 40);
        var service = CreateService();
        await service.AcceptAsync(Us, 1, [new("HK-BL", 40, 25)], User, CancellationToken.None);

        // A second submit (the first already moved it to Logged) is refused, and nothing more is written.
        _repository.Shipments[0].Review = InboundShipmentReview.ToReview;
        var again = await service.AcceptAsync(Us, 1, [new("HK-BL", 15, 15)], User, CancellationToken.None);

        Assert.True(again.IsFailure);
        Assert.Single(_ledgerRepository.Entries, e => e.Type == HomeStockMovementType.ShippedToAmazon);
        Assert.Equal(15, _ledgerRepository.Balance(Us, "HK-BL"));
    }

    [Fact]
    public async Task AcceptAsync_HomeStockChangedSinceThePageLoaded_SkipsItAndKeepsTheShipmentToReview()
    {
        await StockAsync("HK-BL", 40);
        await StockAsync("HK-PK", 8);

        // The page showed 30 blue at home; it is 40 now.
        var result = await CreateService().AcceptAsync(Us, 1, [new("HK-BL", 30, 25), new("HK-PK", 8, 8)], User, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(["HK-BL"], result.Value.Stale);
        Assert.Equal(1, result.Value.Applied);
        Assert.Equal(InboundShipmentReview.ToReview, _repository.Shipments[0].Review);

        // Accepting again only logs the blue: the pink is already logged under the id.
        var second = await CreateService().AcceptAsync(Us, 1, [new("HK-BL", 40, 25), new("HK-PK", 0, 8)], User, CancellationToken.None);
        Assert.Equal((1, 25), (second.Value.Applied, second.Value.UnitsOut));
        Assert.Equal(InboundShipmentReview.Logged, _repository.Shipments[0].Review);
    }

    [Fact]
    public async Task AcceptAsync_NothingEntered_FailsAndWritesNothing()
    {
        var result = await CreateService().AcceptAsync(Us, 1, [new("HK-BL", 40, 0), new("MAT-1", 0, 50)], User, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(_ledgerRepository.Entries);
        Assert.Equal(InboundShipmentReview.ToReview, _repository.Shipments[0].Review);
    }

    [Fact]
    public async Task DismissAndReopen_MoveOnlyFromTheExpectedState()
    {
        var service = CreateService();

        Assert.True((await service.ReopenAsync(Us, 1, User, CancellationToken.None)).IsFailure);
        Assert.True((await service.DismissAsync(Us, 1, User, CancellationToken.None)).IsSuccess);
        Assert.Equal(InboundShipmentReview.Dismissed, _repository.Shipments[0].Review);
        Assert.True((await service.AcceptAsync(Us, 1, [new("HK-BL", 0, 5)], User, CancellationToken.None)).IsFailure);
        Assert.True((await service.ReopenAsync(Us, 1, User, CancellationToken.None)).IsSuccess);
        Assert.Equal(InboundShipmentReview.ToReview, _repository.Shipments[0].Review);
        Assert.Empty(_ledgerRepository.Entries);
    }
}
