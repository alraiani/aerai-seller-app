using AERai.Web.Application.Ingestion;
using AERai.Web.Application.Tests.Fakes;
using AERai.Web.Domain.Core;
using AERai.Web.Domain.Ingestion;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace AERai.Web.Application.Tests.Ingestion;

public sealed class SyncScheduleServiceTests
{
    private readonly FakeSyncScheduleRepository _repository = new();
    private readonly FakeManualRunChannel _channel = new();
    private readonly FakeSyncSettingsRepository _settings = new();
    private readonly FakeIdentityService _identity = new();
    private readonly FakeMarketplaceQueries _marketplaces = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero));

    private SyncScheduleService CreateService(bool canRun = true) =>
        new(_repository, _settings, _channel, new FakeConnection(canRun), _identity, _marketplaces, _clock, NullLogger<SyncScheduleService>.Instance);

    private static SyncScheduleInput Input(
        ScheduleFrequency frequency = ScheduleFrequency.Interval, int? interval = 60, string zone = "America/New_York", int lookback = 7, bool enabled = true,
        string marketplaceId = MarketplaceIds.UnitedStates) =>
        new("Orders hourly", AmazonReportType.Orders, marketplaceId, enabled, frequency, interval, new TimeOnly(6, 0), zone, lookback, AutoPromote: true);

    [Fact]
    public async Task CreateAsync_EnabledSchedule_ComputesNextRun()
    {
        var result = await CreateService().CreateAsync(Input(), "ops", CancellationToken.None);

        Assert.True(result.IsSuccess);
        var saved = _repository.Schedules[result.Value];
        Assert.Equal(_clock.GetUtcNow().AddHours(1), saved.NextRunAt);
        Assert.Null(saved.DailyTime); // Only the field that applies to the frequency is kept.
    }

    [Theory]
    [InlineData(MarketplaceIds.UnitedKingdom)] // seeded inactive
    [InlineData("NOT-A-MARKETPLACE")]
    public async Task CreateAsync_InactiveOrUnknownMarketplace_Fails(string marketplaceId)
    {
        var result = await CreateService().CreateAsync(Input(marketplaceId: marketplaceId), "ops", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(_repository.Schedules);
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
    public async Task ScheduleChanges_WakeTheScheduler()
    {
        var service = CreateService();
        var id = (await service.CreateAsync(Input(), "ops", CancellationToken.None)).Value;
        await service.UpdateAsync(id, Input() with { Notes = "edited" }, "ops", CancellationToken.None);
        await service.SetEnabledAsync(id, enabled: false, "ops", CancellationToken.None);
        await service.SetPausedAsync(paused: true, "ops", CancellationToken.None);
        await service.DeleteAsync(id, "ops", CancellationToken.None);
        await service.RestoreAsync(id, "ops", CancellationToken.None);

        // The scheduler sleeps until the next due slot, so every change to when schedules run must wake it.
        Assert.Equal(6, _channel.Wakes);
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
    public async Task BackfillAsync_Orders_QueuesRunWithWindow()
    {
        var id = (await CreateService().CreateAsync(Input(), "ops", CancellationToken.None)).Value;
        var window = BackfillWindow.LastDays(30, _clock.GetUtcNow());

        var result = await CreateService().BackfillAsync(id, window, "ops", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new ManualRunRequest(id, "ops", window), Assert.Single(_channel.Enqueued));
    }

    [Fact]
    public async Task BackfillAsync_DateRangeInThePast_QueuesThatRange()
    {
        var id = (await CreateService().CreateAsync(Input(), "ops", CancellationToken.None)).Value;
        var window = new BackfillWindow(_clock.GetUtcNow().AddDays(-40), _clock.GetUtcNow().AddDays(-20));

        var result = await CreateService().BackfillAsync(id, window, "ops", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(window, Assert.Single(_channel.Enqueued).Backfill);
    }

    [Fact]
    public async Task BackfillAsync_FbaInventory_FailsBecauseSnapshotsHaveNoHistory()
    {
        var input = Input() with { ReportType = AmazonReportType.FbaInventory };
        var id = (await CreateService().CreateAsync(input, "ops", CancellationToken.None)).Value;

        Assert.True((await CreateService().BackfillAsync(id, BackfillWindow.LastDays(30, _clock.GetUtcNow()), "ops", CancellationToken.None)).IsFailure);
        Assert.Empty(_channel.Enqueued);
    }

    [Theory]
    [InlineData(-31, 0)]   // Longer than Amazon's 30-day limit.
    [InlineData(-5, -6)]   // End before start.
    [InlineData(-1, 1)]    // Ends tomorrow.
    public async Task BackfillAsync_InvalidWindow_Fails(int startDays, int endDays)
    {
        var id = (await CreateService().CreateAsync(Input(), "ops", CancellationToken.None)).Value;
        var now = _clock.GetUtcNow();

        var result = await CreateService().BackfillAsync(id, new BackfillWindow(now.AddDays(startDays), now.AddDays(endDays)), "ops", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(_channel.Enqueued);
    }

    [Fact]
    public async Task CreateAsync_DuplicateName_Fails()
    {
        await CreateService().CreateAsync(Input(), "ops", CancellationToken.None);

        var result = await CreateService().CreateAsync(Input() with { Name = "ORDERS HOURLY" }, "ops", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Single(_repository.Schedules);
    }

    [Fact]
    public async Task CreateAsync_UnknownOwner_Fails()
    {
        var result = await CreateService().CreateAsync(Input() with { OwnerEmail = "nobody@example.com" }, "ops", CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task CreateAsync_NotesAndKnownOwner_AreSavedTrimmed()
    {
        _identity.Users.Add(new("1", "pat@example.com", "Pat", ["Operator"], IsLockedOut: false));

        var result = await CreateService().CreateAsync(Input() with { Notes = "  feeds the P&L  ", OwnerEmail = "pat@example.com" }, "ops", CancellationToken.None);

        var saved = _repository.Schedules[result.Value];
        Assert.Equal("feeds the P&L", saved.Notes);
        Assert.Equal("pat@example.com", saved.OwnerEmail);
    }

    [Fact]
    public async Task CreateAsync_NotesTooLong_Fails()
    {
        var result = await CreateService().CreateAsync(Input() with { Notes = new string('x', SyncScheduleService.MaxNotesLength + 1) }, "ops", CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task DeleteAsync_ExistingSchedule_HidesItAndStopsItRunning()
    {
        var id = (await CreateService().CreateAsync(Input(), "ops", CancellationToken.None)).Value;

        var result = await CreateService().DeleteAsync(id, "ops", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(await _repository.GetAsync(id, CancellationToken.None));
        Assert.Empty(await _repository.GetDueAsync(_clock.GetUtcNow().AddDays(1), CancellationToken.None));
        Assert.Equal("ops", _repository.Schedules[id].DeletedBy);
    }

    [Fact]
    public async Task DeleteAsync_Unknown_Fails()
    {
        Assert.True((await CreateService().DeleteAsync(99, "ops", CancellationToken.None)).IsFailure);
    }

    [Fact]
    public async Task DeleteAsync_FreesTheNameForANewSchedule()
    {
        var id = (await CreateService().CreateAsync(Input(), "ops", CancellationToken.None)).Value;
        await CreateService().DeleteAsync(id, "ops", CancellationToken.None);

        Assert.True((await CreateService().CreateAsync(Input(), "ops", CancellationToken.None)).IsSuccess);
    }

    [Fact]
    public async Task RestoreAsync_DeletedSchedule_ComesBackTurnedOff()
    {
        var id = (await CreateService().CreateAsync(Input(), "ops", CancellationToken.None)).Value;
        await CreateService().DeleteAsync(id, "ops", CancellationToken.None);

        var result = await CreateService().RestoreAsync(id, "ops", CancellationToken.None);

        Assert.True(result.IsSuccess);
        var restored = await _repository.GetAsync(id, CancellationToken.None);
        Assert.NotNull(restored);
        Assert.False(restored.IsEnabled);
        Assert.Null(restored.NextRunAt);
    }

    [Fact]
    public async Task RestoreAsync_NameTakenSinceDeletion_Fails()
    {
        var id = (await CreateService().CreateAsync(Input(), "ops", CancellationToken.None)).Value;
        await CreateService().DeleteAsync(id, "ops", CancellationToken.None);
        await CreateService().CreateAsync(Input(), "ops", CancellationToken.None);

        Assert.True((await CreateService().RestoreAsync(id, "ops", CancellationToken.None)).IsFailure);
        Assert.True(_repository.Schedules[id].IsDeleted);
    }

    [Fact]
    public async Task RestoreAsync_ActiveSchedule_Fails()
    {
        var id = (await CreateService().CreateAsync(Input(), "ops", CancellationToken.None)).Value;

        Assert.True((await CreateService().RestoreAsync(id, "ops", CancellationToken.None)).IsFailure);
    }

    [Fact]
    public async Task SetPausedAsync_RecordsWhoAndWhen_WithoutTouchingSchedules()
    {
        var id = (await CreateService().CreateAsync(Input(), "ops", CancellationToken.None)).Value;

        await CreateService().SetPausedAsync(paused: true, "ops", CancellationToken.None);

        Assert.True(_settings.Settings.IsPaused);
        Assert.Equal("ops", _settings.Settings.PausedBy);
        Assert.Equal(_clock.GetUtcNow(), _settings.Settings.PausedAt);
        Assert.True(_repository.Schedules[id].IsEnabled); // Resume restores exactly what was on.

        await CreateService().SetPausedAsync(paused: false, "ops", CancellationToken.None);
        Assert.False(_settings.Settings.IsPaused);
        Assert.Null(_settings.Settings.PausedBy);
    }

    [Fact]
    public async Task BulkAsync_Disable_TurnsOffEverySelectedSchedule()
    {
        var a = (await CreateService().CreateAsync(Input() with { Name = "A" }, "ops", CancellationToken.None)).Value;
        var b = (await CreateService().CreateAsync(Input() with { Name = "B" }, "ops", CancellationToken.None)).Value;
        var c = (await CreateService().CreateAsync(Input() with { Name = "C" }, "ops", CancellationToken.None)).Value;

        var result = await CreateService().BulkAsync(BulkScheduleAction.Disable, [a, b, b], "ops", CancellationToken.None);

        Assert.Equal(2, result.Value.Succeeded);
        Assert.False(_repository.Schedules[a].IsEnabled);
        Assert.False(_repository.Schedules[b].IsEnabled);
        Assert.True(_repository.Schedules[c].IsEnabled);
    }

    [Fact]
    public async Task BulkAsync_Delete_ReportsUnknownIdsWithoutStoppingTheRest()
    {
        var a = (await CreateService().CreateAsync(Input() with { Name = "A" }, "ops", CancellationToken.None)).Value;

        var result = await CreateService().BulkAsync(BulkScheduleAction.Delete, [a, 99], "ops", CancellationToken.None);

        Assert.Equal(1, result.Value.Succeeded);
        Assert.Single(result.Value.Errors);
        Assert.True(_repository.Schedules[a].IsDeleted);
    }

    [Fact]
    public async Task BulkAsync_NothingSelected_Fails()
    {
        Assert.True((await CreateService().BulkAsync(BulkScheduleAction.Delete, [], "ops", CancellationToken.None)).IsFailure);
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
