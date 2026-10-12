using AERai.Web.Application.Inventory;
using AERai.Web.Application.Tests.Fakes;
using AERai.Web.Domain.Ingestion;
using Microsoft.Extensions.Logging.Abstractions;

namespace AERai.Web.Application.Tests.Inventory;

public sealed class InventoryRefreshRequesterTests
{
    private const string Us = "ATVPDKIKX0DER";
    private const string Ca = "A2EUQ1WTGCTBG2";

    private readonly FakeSyncScheduleRepository _schedules = new();
    private readonly FakeManualRunChannel _queue = new();

    public InventoryRefreshRequesterTests()
    {
        Add(1, AmazonReportType.FbaInventory, Us);
        Add(2, AmazonReportType.RestockRecommendations, Us);
        Add(3, AmazonReportType.Orders, Us);
        Add(4, AmazonReportType.FbaInventory, Ca);
        Add(5, AmazonReportType.FbaInventory, Us, deleted: true);
    }

    private void Add(int id, AmazonReportType type, string marketplaceId, bool deleted = false) =>
        _schedules.Schedules[id] = new SyncSchedule
        {
            Id = id,
            Name = $"s{id}",
            ReportType = type,
            MarketplaceId = marketplaceId,
            TimeZoneId = "UTC",
            UpdatedBy = "t",
            DeletedAt = deleted ? DateTimeOffset.UnixEpoch : null,
        };

    private InventoryRefreshRequester CreateRequester(bool connected = true) =>
        new(_schedules, _queue, new FakeConnection(connected), NullLogger<InventoryRefreshRequester>.Instance);

    [Fact]
    public async Task RequestAsync_QueuesTheMarketplacesInventoryAndRestockSchedules()
    {
        var queued = await CreateRequester().RequestAsync(Us, "ops@example.com", CancellationToken.None);

        Assert.Equal(2, queued);
        Assert.Equal([1, 2], _queue.Enqueued.Select(r => r.ScheduleId).Order());
        Assert.All(_queue.Enqueued, r => Assert.Equal("ops@example.com", r.RequestedBy));
    }

    [Fact]
    public async Task RequestAsync_AmazonNotConnected_QueuesNothing()
    {
        var queued = await CreateRequester(connected: false).RequestAsync(Us, "ops@example.com", CancellationToken.None);

        Assert.Equal(0, queued);
        Assert.Empty(_queue.Enqueued);
    }
}
