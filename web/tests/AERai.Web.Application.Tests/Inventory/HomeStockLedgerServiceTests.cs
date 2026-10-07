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

    private HomeStockLedgerService CreateService() => new(_repository, _clock, NullLogger<HomeStockLedgerService>.Instance);

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
}
