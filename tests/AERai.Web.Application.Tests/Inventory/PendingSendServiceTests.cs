using AERai.Web.Application.Inventory;
using AERai.Web.Application.Tests.Fakes;
using AERai.Web.Domain.Core;
using AERai.Web.Domain.Reporting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace AERai.Web.Application.Tests.Inventory;

public sealed class PendingSendServiceTests
{
    private const string Us = "ATVPDKIKX0DER";
    private const string User = "ops@example.com";
    private const string Sku = "FOB00BL";

    private static readonly DateTimeOffset Now = new(2026, 10, 12, 15, 0, 0, TimeSpan.Zero);

    private readonly FakePendingSendRepository _repository = new();
    private readonly FakeInventoryQueries _queries = new();
    private readonly FakeHomeStockLedgerRepository _ledgerRepository = new();
    private readonly FakeInventoryRefreshRequester _refresh = new();
    private readonly FakeStockAlertRefreshSignal _alerts = new();
    private readonly FakeTimeProvider _clock = new(Now);

    private PendingSendService CreateService()
    {
        var ledger = new HomeStockLedgerService(_ledgerRepository, _queries, _refresh, _alerts, _clock, NullLogger<HomeStockLedgerService>.Instance);
        return new(_repository, _queries, ledger, _refresh, _alerts, Options.Create(new InventoryOptions()), _clock, NullLogger<PendingSendService>.Instance);
    }

    private static PendingSend Send(long id, int units, DateTimeOffset at, int reconciled = 0, int? familyId = null, string? reference = null) =>
        new(id, Sku, null, familyId, null, at, at, User, units, reconciled, new AmazonBaseline(100, at.AddHours(-1)), reference, IsOpen: true);

    [Fact]
    public async Task ListAsync_FlagsSendsOlderThanTheExpiry()
    {
        _repository.Sends.Add(Send(1, 24, Now.AddDays(-8), familyId: 3));
        _repository.Sends.Add(Send(2, 10, Now.AddDays(-1), familyId: 4));

        var all = await CreateService().ListAsync(Us, null, CancellationToken.None);
        var family = await CreateService().ListAsync(Us, 4, CancellationToken.None);

        Assert.Equal([(1L, 8, true), (2L, 1, false)], all.Select(i => (i.Send.Id, i.AgeDays, i.Overdue)));
        Assert.Equal([2L], family.Select(i => i.Send.Id));
    }

    [Fact]
    public async Task ReconcileAsync_ClosesSendsTheSnapshotShowsAndAsksForAlerts()
    {
        _repository.Sends.Add(Send(1, 24, Now.AddDays(-1)));
        _queries.Positions.Add(new InventoryPosition { Sku = Sku, MarketplaceId = Us, SnapshotDate = new(2026, 10, 12), SnapshotReceivedAt = Now.AddHours(-1), Available = 100, InboundWorking = 24 });

        var updated = await CreateService().ReconcileAsync(Us, CancellationToken.None);

        Assert.Equal(1, updated);
        Assert.False(_repository.Sends[0].IsOpen);
        Assert.Equal(HomeStockMovement.SystemReconciler, _repository.ClosedBy[1]);
        Assert.Equal(1, _alerts.Requests);
    }

    [Fact]
    public async Task ReconcileAsync_NoNewSnapshot_ChangesNothing()
    {
        _repository.Sends.Add(Send(1, 24, Now.AddDays(-1)));
        _queries.Positions.Add(new InventoryPosition { Sku = Sku, MarketplaceId = Us, SnapshotDate = new(2026, 10, 11), SnapshotReceivedAt = Now.AddDays(-2), Available = 500 });

        Assert.Equal(0, await CreateService().ReconcileAsync(Us, CancellationToken.None));
        Assert.True(_repository.Sends[0].IsOpen);
        Assert.Equal(0, _alerts.Requests);
    }

    [Fact]
    public async Task MarkReconciledAsync_ClosesItAsTheUser()
    {
        _repository.Sends.Add(Send(1, 24, Now.AddDays(-9), reconciled: 20));

        var result = await CreateService().MarkReconciledAsync(Us, 1, User, CancellationToken.None);
        var again = await CreateService().MarkReconciledAsync(Us, 1, User, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal((24, false), (_repository.Sends[0].ReconciledUnits, _repository.Sends[0].IsOpen));
        Assert.Equal(User, _repository.ClosedBy[1]);
        Assert.True(again.IsFailure);
    }

    [Fact]
    public async Task CancelAsync_ReversesTheSendSoTheUnitsComeHome()
    {
        _ledgerRepository.Skus.Add(Sku);
        await _ledgerRepository.RecordAsync(new(Us, Sku, HomeStockMovementType.ReceivedFromSupplier, 50, Now, null, null, null, Now, User), CancellationToken.None);
        await _ledgerRepository.RecordAsync(new(Us, Sku, HomeStockMovementType.ShippedToAmazon, -24, Now, null, null, null, Now, User), CancellationToken.None);
        _repository.Sends.Add(Send(2, 24, Now));

        var result = await CreateService().CancelAsync(Us, 2, User, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(50, _ledgerRepository.Balance(Us, Sku));
        Assert.Equal(2, _ledgerRepository.Entries[^1].ReversesId);
    }

    [Fact]
    public async Task CancelAsync_PartlyAtAmazon_Fails()
    {
        _repository.Sends.Add(Send(1, 24, Now.AddDays(-9), reconciled: 5));

        var result = await CreateService().CancelAsync(Us, 1, User, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("already shows 5", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SetShipmentIdAsync_AddsItOnceAndQueuesASync()
    {
        _repository.Sends.Add(Send(1, 24, Now.AddDays(-1)));

        var wrong = await CreateService().SetShipmentIdAsync(Us, 1, "PO-1234", User, CancellationToken.None);
        var added = await CreateService().SetShipmentIdAsync(Us, 1, "  FBA15K7Y3ZQX ", User, CancellationToken.None);
        var twice = await CreateService().SetShipmentIdAsync(Us, 1, "FBA99", User, CancellationToken.None);

        Assert.True(wrong.IsFailure);
        Assert.True(added.IsSuccess, added.Error);
        Assert.True(twice.IsFailure);
        Assert.Equal("FBA15K7Y3ZQX", _repository.Sends[0].Reference);
        Assert.Equal([Us], _refresh.Requests);
    }

    [Fact]
    public async Task SetShipmentIdsAsync_AddsTheIdToEveryTickedSendWithoutOneAndQueuesOneSync()
    {
        _repository.Sends.Add(Send(1, 24, Now.AddDays(-1)));
        _repository.Sends.Add(Send(2, 10, Now.AddDays(-1)));
        _repository.Sends.Add(Send(3, 5, Now.AddDays(-1), reference: "FBA00OTHER"));
        _repository.Sends.Add(Send(4, 7, Now.AddDays(-1)));

        var result = await CreateService().SetShipmentIdsAsync(Us, [1, 2, 3, 2], " FBA15K7Y3ZQX ", User, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal((2, 1), result.Value);
        Assert.Equal(["FBA15K7Y3ZQX", "FBA15K7Y3ZQX", "FBA00OTHER", null], _repository.Sends.Select(s => s.Reference));
        Assert.Equal([Us], _refresh.Requests);
    }

    [Fact]
    public async Task SetShipmentIdsAsync_NothingTickedOrABadId_SavesNothing()
    {
        _repository.Sends.Add(Send(1, 24, Now.AddDays(-1)));

        var none = await CreateService().SetShipmentIdsAsync(Us, [], "FBA15K7Y3ZQX", User, CancellationToken.None);
        var bad = await CreateService().SetShipmentIdsAsync(Us, [1], "PO-1234", User, CancellationToken.None);
        var tooMany = await CreateService().SetShipmentIdsAsync(Us, Enumerable.Range(1, PendingSendService.MaxShipmentIdSends + 1).Select(i => (long)i).ToList(), "FBA15K7Y3ZQX", User, CancellationToken.None);

        Assert.True(none.IsFailure);
        Assert.True(bad.IsFailure);
        Assert.True(tooMany.IsFailure);
        Assert.Null(_repository.Sends[0].Reference);
        Assert.Empty(_refresh.Requests);
    }
}
