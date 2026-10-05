using AERai.Web.Application.Ingestion;
using AERai.Web.Application.Tests.Fakes;
using AERai.Web.Domain.Ingestion;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace AERai.Web.Application.Tests.Ingestion;

public sealed class SyncScheduleServiceTests
{
    private readonly FakeSyncScheduleRepository _repository = new();
    private readonly FakeManualRunChannel _channel = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero));

    private SyncScheduleService CreateService(bool canRun = true) =>
        new(_repository, _channel, new FakeConnection(canRun), _clock, NullLogger<SyncScheduleService>.Instance);

    private static SyncScheduleInput Input(
        ScheduleFrequency frequency = ScheduleFrequency.Interval, int? interval = 60, string zone = "America/New_York", int lookback = 7, bool enabled = true) =>
        new("Orders hourly", AmazonReportType.Orders, enabled, frequency, interval, new TimeOnly(6, 0), zone, lookback, AutoPromote: true);

    [Fact]
    public async Task CreateAsync_EnabledSchedule_ComputesNextRun()
    {
        var result = await CreateService().CreateAsync(Input(), "ops", CancellationToken.None);

        Assert.True(result.IsSuccess);
        var saved = _repository.Schedules[result.Value];
        Assert.Equal(_clock.GetUtcNow().AddHours(1), saved.NextRunAt);
        Assert.Null(saved.DailyTime); // Only the field that applies to the frequency is kept.
    }

    [Fact]
    public async Task CreateAsync_DisabledSchedule_HasNoNextRun()
    {
        var result = await CreateService().CreateAsync(Input(enabled: false), "ops", CancellationToken.None);

        Assert.Null(_repository.Schedules[result.Value].NextRunAt);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(20_000)]
    public void Validate_IntervalOutOfRange_Fails(int minutes) =>
        Assert.True(CreateService().Validate(Input(interval: minutes)).IsFailure);

    [Fact]
    public void Validate_UnknownTimeZone_Fails() =>
        Assert.True(CreateService().Validate(Input(zone: "Mars/Olympus_Mons")).IsFailure);

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    public void Validate_LookbackOutOfRange_Fails(int days) =>
        Assert.True(CreateService().Validate(Input(lookback: days)).IsFailure);

    [Fact]
    public async Task SetEnabledAsync_Disable_ClearsNextRun()
    {
        var id = (await CreateService().CreateAsync(Input(), "ops", CancellationToken.None)).Value;

        await CreateService().SetEnabledAsync(id, enabled: false, "ops", CancellationToken.None);

        Assert.False(_repository.Schedules[id].IsEnabled);
        Assert.Null(_repository.Schedules[id].NextRunAt);
    }

    [Fact]
    public async Task RunNowAsync_Connected_QueuesRequest()
    {
        var id = (await CreateService().CreateAsync(Input(), "ops", CancellationToken.None)).Value;

        var result = await CreateService().RunNowAsync(id, "ops", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new ManualRunRequest(id, "ops"), Assert.Single(_channel.Enqueued));
    }

    [Fact]
    public async Task BackfillAsync_Orders_QueuesRunWithDays()
    {
        var id = (await CreateService().CreateAsync(Input(), "ops", CancellationToken.None)).Value;

        var result = await CreateService().BackfillAsync(id, 30, "ops", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new ManualRunRequest(id, "ops", 30), Assert.Single(_channel.Enqueued));
    }

    [Fact]
    public async Task BackfillAsync_FbaInventory_FailsBecauseSnapshotsHaveNoHistory()
    {
        var input = Input() with { ReportType = AmazonReportType.FbaInventory };
        var id = (await CreateService().CreateAsync(input, "ops", CancellationToken.None)).Value;

        Assert.True((await CreateService().BackfillAsync(id, 30, "ops", CancellationToken.None)).IsFailure);
        Assert.Empty(_channel.Enqueued);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    public async Task BackfillAsync_DaysOutOfRange_Fails(int days)
    {
        var id = (await CreateService().CreateAsync(Input(), "ops", CancellationToken.None)).Value;

        Assert.True((await CreateService().BackfillAsync(id, days, "ops", CancellationToken.None)).IsFailure);
    }

    [Fact]
    public async Task RunNowAsync_NotConnected_FailsWithoutQueueing()
    {
        var id = (await CreateService().CreateAsync(Input(), "ops", CancellationToken.None)).Value;

        var result = await CreateService(canRun: false).RunNowAsync(id, "ops", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(_channel.Enqueued);
    }
}
