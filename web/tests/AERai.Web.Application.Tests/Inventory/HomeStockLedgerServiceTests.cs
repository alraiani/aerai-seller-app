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
}
