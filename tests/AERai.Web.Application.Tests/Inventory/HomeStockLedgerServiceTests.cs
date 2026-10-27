using AERai.Web.Application.Common;
using AERai.Web.Application.Inventory;
using AERai.Web.Application.Tests.Fakes;
using AERai.Web.Domain.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace AERai.Web.Application.Tests.Inventory;

public sealed class HomeStockLedgerServiceTests
{
    private const string Us = "ATVPDKIKX0DER";
    private const string User = "ops@example.com";

    private readonly FakeHomeStockLedgerRepository _repository = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 7, 15, 0, 0, TimeSpan.Zero));

    public HomeStockLedgerServiceTests()
    {
        _repository.Skus.Add("FOB00BL");
    }

    private readonly FakeStockAlertRefreshSignal _alerts = new();

    private readonly FakeInventoryQueries _inventory = new();

    private readonly FakeInventoryRefreshRequester _refresh = new();

    private HomeStockLedgerService CreateService() => new(_repository, _inventory, _refresh, _alerts, _clock, NullLogger<HomeStockLedgerService>.Instance);

    private Task<Result<string>> RecordAsync(HomeStockMovementType type, int quantity, string? note = null, DateTimeOffset? at = null) =>
        CreateService().RecordAsync(Us, new HomeStockMovementInput("FOB00BL", type, quantity, at, null, note), User, CancellationToken.None);

    [Fact]
    public async Task RecordAsync_ReceivedAddsAndShippedRemoves()
    {
        await RecordAsync(HomeStockMovementType.ReceivedFromSupplier, 100);
        var shipped = await RecordAsync(HomeStockMovementType.ShippedToAmazon, 40);

        Assert.True(shipped.IsSuccess, shipped.Error);
        Assert.Equal([100, -40], _repository.Entries.Select(e => e.Units));
        Assert.Equal(60, _repository.Balance(Us, "FOB00BL"));
    }

    [Fact]
    public async Task RecordAsync_CountCorrectionRecordsTheDifference()
    {
        await RecordAsync(HomeStockMovementType.ReceivedFromSupplier, 100);

        await RecordAsync(HomeStockMovementType.CountCorrection, 95);
        var same = await RecordAsync(HomeStockMovementType.CountCorrection, 95);

        Assert.Equal(-5, _repository.Entries[^1].Units);
        Assert.Equal(2, _repository.Entries.Count);
        Assert.Contains("nothing to record", same.Value, StringComparison.Ordinal);
        Assert.Equal(2, _alerts.Requests); // Only the two recorded movements ask for an alert refresh.
    }

    [Fact]
    public async Task RecordAsync_ShippingMoreThanOnHand_Fails()
    {
        await RecordAsync(HomeStockMovementType.ReceivedFromSupplier, 10);

        var result = await RecordAsync(HomeStockMovementType.ShippedToAmazon, 11);

        Assert.True(result.IsFailure);
        Assert.Contains("below zero", result.Error, StringComparison.Ordinal);
        Assert.Equal(10, _repository.Balance(Us, "FOB00BL"));
    }

    [Theory]
    [InlineData(HomeStockMovementType.ReceivedFromSupplier, 0)]
    [InlineData(HomeStockMovementType.ShippedToAmazon, -5)]
    [InlineData(HomeStockMovementType.CountCorrection, -1)]
    [InlineData(HomeStockMovementType.OpeningBalance, 5)]
    public async Task RecordAsync_InvalidTypeOrQuantity_Fails(HomeStockMovementType type, int quantity)
    {
        Assert.True((await RecordAsync(type, quantity, note: "x")).IsFailure);
        Assert.Empty(_repository.Entries);
    }

    [Fact]
    public async Task RecordAsync_OtherNeedsANoteAndKeepsItsSign()
    {
        Assert.True((await RecordAsync(HomeStockMovementType.Other, 5)).IsFailure);

        await RecordAsync(HomeStockMovementType.ReceivedFromSupplier, 10);
        var damaged = await RecordAsync(HomeStockMovementType.Other, -3, note: "Damaged");

        Assert.True(damaged.IsSuccess, damaged.Error);
        Assert.Equal(-3, _repository.Entries[^1].Units);
    }

    [Fact]
    public async Task RecordAsync_FutureDate_FailsAndBlankDateIsNow()
    {
        Assert.True((await RecordAsync(HomeStockMovementType.ReceivedFromSupplier, 5, at: _clock.GetUtcNow().AddDays(3))).IsFailure);

        await RecordAsync(HomeStockMovementType.ReceivedFromSupplier, 5);

        Assert.Equal(_clock.GetUtcNow(), _repository.Entries.Single().OccurredAt);
    }

    [Fact]
    public async Task RecordAsync_UnknownSku_Fails()
    {
        var result = await CreateService().RecordAsync(Us, new HomeStockMovementInput("NOPE", HomeStockMovementType.ReceivedFromSupplier, 5, null, null, null), User, CancellationToken.None);

        Assert.Equal("There is no SKU \"NOPE\".", result.Error);
    }

    [Fact]
    public async Task ReverseAsync_UndoesOnceAndOnlyOnce()
    {
        await RecordAsync(HomeStockMovementType.ReceivedFromSupplier, 30);
        var service = CreateService();

        var first = await service.ReverseAsync(Us, 1, User, CancellationToken.None);
        var second = await service.ReverseAsync(Us, 1, User, CancellationToken.None);
        var ofReversal = await service.ReverseAsync(Us, 2, User, CancellationToken.None);

        Assert.True(first.IsSuccess, first.Error);
        Assert.True(second.IsFailure);
        Assert.True(ofReversal.IsFailure);
        Assert.Equal(0, _repository.Balance(Us, "FOB00BL"));
    }

    [Fact]
    public async Task ReverseAsync_UnitsAlreadyShipped_Fails()
    {
        await RecordAsync(HomeStockMovementType.ReceivedFromSupplier, 30);
        await RecordAsync(HomeStockMovementType.ShippedToAmazon, 20);

        var result = await CreateService().ReverseAsync(Us, 1, User, CancellationToken.None);

        Assert.Contains("below zero", result.Error, StringComparison.Ordinal);
    }

    private static HomeStockCountOptions Options(HomeStockMovementType up = HomeStockMovementType.ReceivedFromSupplier, HomeStockMovementType down = HomeStockMovementType.ShippedToAmazon, string? note = null) =>
        new(up, down, "PO-77", note, null);

    [Fact]
    public async Task ApplyCountsAsync_LogsIncreasesAndDecreasesWithTheirTypes()
    {
        _repository.Skus.Add("FOB00PI");
        await RecordAsync(HomeStockMovementType.ReceivedFromSupplier, 50);

        var result = await CreateService().ApplyCountsAsync(Us, [new("FOB00BL", 50, 30), new("FOB00PI", 0, 25)], Options(), User, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal((2, 25, 20), (result.Value.Applied, result.Value.UnitsIn, result.Value.UnitsOut));
        Assert.Equal(
            [(HomeStockMovementType.ShippedToAmazon, -20, "PO-77"), (HomeStockMovementType.ReceivedFromSupplier, 25, "PO-77")],
            _repository.Entries.Skip(1).Select(e => (e.Type, e.Units, e.Reference)));
        Assert.Equal(30, _repository.Balance(Us, "FOB00BL"));
    }

    [Fact]
    public async Task ApplyCountsAsync_BalanceChangedSinceReview_SkipsItAsStale()
    {
        await RecordAsync(HomeStockMovementType.ReceivedFromSupplier, 10);

        // Reviewed when the balance was 0; it is 10 now.
        var result = await CreateService().ApplyCountsAsync(Us, [new("FOB00BL", 0, 40)], Options(), User, CancellationToken.None);

        Assert.Equal(0, result.Value.Applied);
        Assert.Equal(["FOB00BL"], result.Value.Stale);
        Assert.Equal(10, _repository.Balance(Us, "FOB00BL"));
    }

    [Fact]
    public async Task ApplyCountsAsync_OtherNeedsANoteAndWrongDirectionTypesFail()
    {
        var service = CreateService();
        IReadOnlyList<HomeStockCountChange> up = [new("FOB00BL", 0, 5)];

        Assert.True((await service.ApplyCountsAsync(Us, up, Options(up: HomeStockMovementType.Other), User, CancellationToken.None)).IsFailure);
        Assert.True((await service.ApplyCountsAsync(Us, up, Options(up: HomeStockMovementType.ShippedToAmazon), User, CancellationToken.None)).IsFailure);
        Assert.True((await service.ApplyCountsAsync(Us, up, Options(up: HomeStockMovementType.Other, note: "Found a box"), User, CancellationToken.None)).IsSuccess);
        Assert.True((await service.ApplyCountsAsync(Us, [], Options(), User, CancellationToken.None)).IsFailure);
    }

    private Task<Result<HomeStockCountResult>> ShipAsync(params HomeStockShipment[] shipments) =>
        CreateService().ShipToAmazonAsync(Us, shipments, " FBA15ABC ", User, CancellationToken.None);

    [Fact]
    public async Task ShipToAmazonAsync_SubtractsAndLogsShippedToAmazonWithTheShipmentId()
    {
        await RecordAsync(HomeStockMovementType.ReceivedFromSupplier, 100);

        var result = await ShipAsync(new HomeStockShipment("FOB00BL", 100, 40));

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal((1, 40), (result.Value.Applied, result.Value.UnitsOut));
        Assert.Equal((HomeStockMovementType.ShippedToAmazon, -40, "FBA15ABC"), (_repository.Entries[^1].Type, _repository.Entries[^1].Units, _repository.Entries[^1].Reference));
        Assert.Equal(60, _repository.Balance(Us, "FOB00BL"));
        Assert.Equal(2, _alerts.Requests); // The receipt and the shipment.
    }

    [Fact]
    public async Task ShipToAmazonAsync_MoreThanAtHomeOrNothing_FailsAndWritesNothing()
    {
        await RecordAsync(HomeStockMovementType.ReceivedFromSupplier, 10);

        var over = await ShipAsync(new HomeStockShipment("FOB00BL", 10, 11));
        var zero = await ShipAsync(new HomeStockShipment("FOB00BL", 10, 0));
        var none = await ShipAsync();

        Assert.Contains("than the 10 at home", over.Error, StringComparison.Ordinal);
        Assert.True(zero.IsFailure);
        Assert.True(none.IsFailure);
        Assert.Single(_repository.Entries);
    }

    [Fact]
    public async Task ShipToAmazonAsync_BalanceChangedSinceEntered_SkipsItAsStale()
    {
        await RecordAsync(HomeStockMovementType.ReceivedFromSupplier, 30);

        // The amount was entered when the page showed 20; it is 30 now.
        var result = await ShipAsync(new HomeStockShipment("FOB00BL", 20, 5));

        Assert.Equal(0, result.Value.Applied);
        Assert.Equal(["FOB00BL"], result.Value.Stale);
        Assert.Equal(30, _repository.Balance(Us, "FOB00BL"));
        Assert.Equal(1, _alerts.Requests); // Only the receipt; nothing was shipped.
    }

    [Fact]
    public void Payload_RoundTripsAndRejectsBadLines()
    {
        var text = HomeStockCountPayload.Write([new("FOB00BL", 50, 30), new("FOB00PI", 0, 25)]);

        Assert.Equal([("FOB00BL", 50, 30), ("FOB00PI", 0, 25)], HomeStockCountPayload.Read(text).Value.Select(c => (c.Sku, c.Current, c.New)));
        Assert.True(HomeStockCountPayload.Read("FOB00BL\t-1\t5").IsFailure);
        Assert.True(HomeStockCountPayload.Read("FOB00BL\t5").IsFailure);
        Assert.True(HomeStockCountPayload.Read("FOB00BL\t0\t2000000").IsFailure);
        Assert.True(HomeStockCountPayload.Read("").IsFailure);
    }

    [Fact]
    public async Task ShipToAmazonAsync_StoresAmazonsStockAndQueuesASync()
    {
        var snapshotAt = _clock.GetUtcNow().AddHours(-3);
        _inventory.Positions.Add(new Domain.Reporting.InventoryPosition { Sku = "FOB00BL", MarketplaceId = Us, SnapshotReceivedAt = snapshotAt, Available = 30, InboundShipped = 5, ReservedCustomerOrder = 4 });
        await RecordAsync(HomeStockMovementType.ReceivedFromSupplier, 100);

        var result = await CreateService().ShipToAmazonAsync(Us, [new HomeStockShipment("FOB00BL", 100, 40)], "FBA15K7Y3ZQX", User, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(new AmazonBaseline(35, snapshotAt), _repository.Entries[^1].AmazonBaseline); // Sell-through stock: customer orders excluded.
        Assert.Equal([Us], _refresh.Requests);
    }

    [Fact]
    public async Task RecordAsync_OnlySendsStoreABaselineAndQueueASync()
    {
        await RecordAsync(HomeStockMovementType.ReceivedFromSupplier, 100);
        await RecordAsync(HomeStockMovementType.ShippedToAmazon, 10);

        Assert.Null(_repository.Entries[0].AmazonBaseline);
        Assert.Equal(new AmazonBaseline(0, null), _repository.Entries[1].AmazonBaseline); // Nothing at Amazon yet.
        Assert.Single(_refresh.Requests);
    }

    [Fact]
    public async Task GetActionsAsync_AsksForAPageOfActionsCappedAtMaxGroupRows()
    {
        await CreateService().GetActionsAsync(Us, new HomeStockLedgerFilter(), new PageRequest(2, 20, "FOB"), CancellationToken.None);

        Assert.Equal((new PageRequest(2, 20, "FOB"), HomeStockLedgerService.MaxGroupRows), _repository.LastActionsCall);
    }

    private HomeStockLedgerEntry Entry(long id, HomeStockMovementType type, int units, long? reverses = null, long? reversedBy = null) =>
        new(id, "FOB00BL", null, null, _clock.GetUtcNow(), type, units, 5, null, null, reverses, reversedBy, _clock.GetUtcNow(), User);

    [Fact]
    public void HomeStockLedgerAction_NetsUnitsAndKnowsWhenEntriesAreCappedOrUndone()
    {
        var now = _clock.GetUtcNow();
        var open = new HomeStockLedgerAction(now, User, [HomeStockMovementType.CountCorrection], 2, 1, 12, -5, 0, now, now, null,
            [Entry(1, HomeStockMovementType.CountCorrection, 12)]);
        var undone = open with { Entries = [Entry(1, HomeStockMovementType.CountCorrection, 12, reversedBy: 3)] };
        var undo = open with { EntryCount = 1, ReversalCount = 1, Entries = [Entry(3, HomeStockMovementType.CountCorrection, -12, reverses: 1)] };

        Assert.Equal((7, true, true, false, false), (open.NetUnits, open.IsCapped, open.CanUndo, open.IsUndone, open.IsUndo));
        Assert.Equal((false, true), (undone.CanUndo, undone.IsUndone));
        Assert.Equal((true, false, false), (undo.IsUndo, undo.CanUndo, undo.IsUndone));
    }

    [Fact]
    public async Task UndoActionAsync_ReversesEveryEntryTheSaveWroteAsOneAction()
    {
        _repository.Skus.Add("FOB00GR");
        await CreateService().ApplyCountsAsync(
            Us, [new HomeStockCountChange("FOB00BL", 0, 40), new HomeStockCountChange("FOB00GR", 0, 10)], Options(), User, CancellationToken.None);
        _clock.Advance(TimeSpan.FromMinutes(5));
        await RecordAsync(HomeStockMovementType.ReceivedFromSupplier, 7);
        _clock.Advance(TimeSpan.FromMinutes(5));

        var result = await CreateService().UndoActionAsync(Us, 2, "boss@example.com", CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Contains("2 entries reversed", result.Value, StringComparison.Ordinal);
        Assert.Equal((7, 0), (_repository.Balance(Us, "FOB00BL"), _repository.Balance(Us, "FOB00GR")));
        var reversals = _repository.Entries.Skip(3).ToList();
        Assert.Equal([1L, 2L], reversals.Select(e => e.ReversesId ?? 0));
        Assert.Single(reversals.Select(e => e.CreatedAt).Distinct()); // The undo is one action too.
    }

    [Fact]
    public async Task UndoActionAsync_OneSkuWouldGoNegative_UndoesNothing()
    {
        _repository.Skus.Add("FOB00GR");
        await CreateService().ApplyCountsAsync(
            Us, [new HomeStockCountChange("FOB00BL", 0, 40), new HomeStockCountChange("FOB00GR", 0, 10)], Options(), User, CancellationToken.None);
        _clock.Advance(TimeSpan.FromMinutes(5));
        await CreateService().RecordAsync(Us, new HomeStockMovementInput("FOB00GR", HomeStockMovementType.ShippedToAmazon, 8, null, null, null), User, CancellationToken.None);

        var result = await CreateService().UndoActionAsync(Us, 1, User, CancellationToken.None);

        Assert.Contains("FOB00GR", result.Error, StringComparison.Ordinal);
        Assert.Equal(3, _repository.Entries.Count);
        Assert.Equal(40, _repository.Balance(Us, "FOB00BL"));
    }

    [Fact]
    public async Task UndoActionAsync_AlreadyUndone_Fails()
    {
        await RecordAsync(HomeStockMovementType.ReceivedFromSupplier, 30);
        var service = CreateService();

        await service.UndoActionAsync(Us, 1, User, CancellationToken.None);
        var again = await service.UndoActionAsync(Us, 1, User, CancellationToken.None);

        Assert.Contains("can be undone", again.Error, StringComparison.Ordinal);
        Assert.Equal(0, _repository.Balance(Us, "FOB00BL"));
    }

    [Fact]
    public async Task ReverseManyAsync_NothingChosen_Fails()
    {
        var result = await CreateService().ReverseManyAsync(Us, [], User, CancellationToken.None);

        Assert.Equal("Choose the entries to reverse.", result.Error);
    }

    private Task<Result<string>> CorrectAsync(HomeStockMovementType type, int units, string? note = null) =>
        CreateService().CorrectAsync(Us, new HomeStockCorrectionInput(7, type, units, note), User, CancellationToken.None);

    [Theory]
    [InlineData(HomeStockMovementType.ReceivedFromSupplier, -12, 12)]
    [InlineData(HomeStockMovementType.ShippedToAmazon, 5, -5)]
    [InlineData(HomeStockMovementType.CountCorrection, -3, -3)]
    public async Task CorrectAsync_GivesReceivedAndShippedTheirOwnSign(HomeStockMovementType type, int typed, int recorded)
    {
        var result = await CorrectAsync(type, typed, " recount ");

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal((7L, type, recorded, "recount"), (_repository.LastCorrection?.Id, _repository.LastCorrection?.Type, _repository.LastCorrection?.Units, _repository.LastCorrection?.Note));
        Assert.True(_alerts.Requests > 0);
    }

    [Theory]
    [InlineData(HomeStockMovementType.OpeningBalance, 5, "x")]
    [InlineData(HomeStockMovementType.CountCorrection, 0, "x")]
    [InlineData(HomeStockMovementType.Other, 4, null)]
    [InlineData(HomeStockMovementType.ReceivedFromSupplier, int.MinValue, "x")]
    public async Task CorrectAsync_RefusesWhatCantBeLogged(HomeStockMovementType type, int units, string? note)
    {
        var result = await CorrectAsync(type, units, note);

        Assert.True(result.IsFailure);
        Assert.Null(_repository.LastCorrection);
    }

    [Fact]
    public async Task CorrectAsync_SendAmazonAlreadyShows_ExplainsWhy()
    {
        _repository.CorrectOutcome = HomeStockLedgerOutcome.AlreadyAtAmazon;

        var result = await CorrectAsync(HomeStockMovementType.ShippedToAmazon, 2);

        Assert.True(result.IsFailure);
        Assert.Contains("Amazon already shows", result.Error, StringComparison.Ordinal);
    }

    private HomeStockLedgerEntry Listed(long id, HomeStockMovementType type, int units, string? note = null)
    {
        var entry = new HomeStockLedgerEntry(id, "FOB00BL", null, null, _clock.GetUtcNow(), type, units, 50, null, note, null, null, _clock.GetUtcNow(), User);
        _repository.Listed.Add(entry);
        return entry;
    }

    [Fact]
    public async Task CorrectReasonAsync_NoteOnly_KeepsEachEntrysTypeAndUnits()
    {
        Listed(1, HomeStockMovementType.ReceivedFromSupplier, 20);
        Listed(2, HomeStockMovementType.ShippedToAmazon, -5);
        Listed(3, HomeStockMovementType.Other, -2, "audit");

        var result = await CreateService().CorrectReasonAsync(Us, [1, 2, 3], null, " audit ", User, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(new HomeStockBulkCorrectionResult(2, 1, []), result.Value with { Skipped = [] });
        Assert.Empty(result.Value.Skipped);
        Assert.Equal(
            [(1L, HomeStockMovementType.ReceivedFromSupplier, 20, "audit"), (2L, HomeStockMovementType.ShippedToAmazon, -5, "audit"), (3L, HomeStockMovementType.Other, -2, "audit")],
            _repository.Corrections);
    }

    [Fact]
    public async Task CorrectReasonAsync_NewType_TakesItsDirectionAndSkipsWhatCantChange()
    {
        Listed(1, HomeStockMovementType.ReceivedFromSupplier, 20);
        Listed(2, HomeStockMovementType.CountCorrection, -4);

        var toShipped = await CreateService().CorrectReasonAsync(Us, [1, 2], HomeStockMovementType.ShippedToAmazon, null, User, CancellationToken.None);
        var toOther = await CreateService().CorrectReasonAsync(Us, [2, 404], HomeStockMovementType.Other, null, User, CancellationToken.None);

        Assert.Equal([(1L, HomeStockMovementType.ShippedToAmazon, -20, (string?)null), (2L, HomeStockMovementType.ShippedToAmazon, -4, null)], _repository.Corrections);
        Assert.Equal(2, toShipped.Value.Updated);
        Assert.Equal(0, toOther.Value.Updated);
        Assert.Collection(
            toOther.Value.Skipped,
            s => Assert.StartsWith("#2 FOB00BL: Add a note", s, StringComparison.Ordinal),
            s => Assert.StartsWith("#404:", s, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false, HomeStockMovementType.Other, "x")]
    [InlineData(true, null, " ")]
    public async Task CorrectReasonAsync_NothingToDo_Fails(bool withIds, HomeStockMovementType? type, string? note)
    {
        var result = await CreateService().CorrectReasonAsync(Us, withIds ? [1L] : [], type, note, User, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(_repository.Corrections);
    }
}
