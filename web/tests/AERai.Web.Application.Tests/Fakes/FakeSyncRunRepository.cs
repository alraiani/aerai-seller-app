using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Domain.Ingestion;

namespace AERai.Web.Application.Tests.Fakes;

/// <summary>
/// In-memory <see cref="ISyncRunRepository"/>. Stores copies, like a database would, so a test only
/// sees what the service actually saved.
/// </summary>
internal sealed class FakeSyncRunRepository : ISyncRunRepository
{
    public List<SyncRun> Completed { get; } = [];

    public List<IngestedReport> Ledger { get; } = [];

    /// <summary>Every saved run state, keyed by run id.</summary>
    public Dictionary<long, SyncRun> Stored { get; } = [];

    private long _nextId = 1;

    public Task<long> StartAsync(SyncRun run, CancellationToken cancellationToken)
    {
        run.Id = _nextId++;
        Stored[run.Id] = Copy(run);
        return Task.FromResult(run.Id);
    }

    public Task CompleteAsync(SyncRun run, CancellationToken cancellationToken)
    {
        Completed.Add(run);
        var stored = Copy(run);
        stored.PendingReportId = null;
        stored.NextCheckAt = null;
        Stored[run.Id] = stored;
        return Task.CompletedTask;
    }

    public Task<SyncRun?> GetAsync(long id, CancellationToken cancellationToken) =>
        Task.FromResult(Stored.TryGetValue(id, out var run) ? Copy(run) : null);

    public Task SaveProgressAsync(SyncRun run, CancellationToken cancellationToken)
    {
        var stored = Stored[run.Id];
        stored.DataStart = run.DataStart;
        stored.DataEnd = run.DataEnd;
        stored.AmazonReportIds = run.AmazonReportIds;
        stored.PendingReportId = run.PendingReportId;
        stored.NextCheckAt = run.NextCheckAt;
        stored.PollAttempts = run.PollAttempts;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<SyncRun>> GetPendingDueAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SyncRun>>(Pending.Where(r => r.NextCheckAt <= now).OrderBy(r => r.NextCheckAt).Select(Copy).ToList());

    public Task<DateTimeOffset?> GetNextPendingCheckAtAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Pending.Min(r => r.NextCheckAt));

    public Task<bool> TryClaimCheckAsync(long id, DateTimeOffset expectedNextCheckAt, DateTimeOffset newNextCheckAt, CancellationToken cancellationToken)
    {
        if (Stored.GetValueOrDefault(id) is not { Status: SyncRunStatus.Running } run || run.NextCheckAt != expectedNextCheckAt)
        {
            return Task.FromResult(false);
        }

        run.NextCheckAt = newNextCheckAt;
        return Task.FromResult(true);
    }

    public Task<bool> HasPendingAsync(int scheduleId, CancellationToken cancellationToken) =>
        Task.FromResult(Pending.Any(r => r.SyncScheduleId == scheduleId));

    public Task<int> FailAbandonedAsync(DateTimeOffset startedBefore, string message, DateTimeOffset completedAt, CancellationToken cancellationToken)
    {
        var abandoned = Stored.Values.Where(r => r.Status == SyncRunStatus.Running && r.PendingReportId is null && r.StartedAt < startedBefore).ToList();
        foreach (var run in abandoned)
        {
            run.Status = SyncRunStatus.Failed;
            run.Message = message;
            run.CompletedAt = completedAt;
        }

        return Task.FromResult(abandoned.Count);
    }

    public Task<bool> IsReportIngestedAsync(string amazonReportId, CancellationToken cancellationToken) =>
        Task.FromResult(Ledger.Any(r => r.AmazonReportId == amazonReportId));

    public Task AddIngestedReportAsync(IngestedReport report, CancellationToken cancellationToken)
    {
        Ledger.Add(report);
        return Task.CompletedTask;
    }

    public Task<PagedResult<SyncRun>> ListAsync(string marketplaceId, int? scheduleId, PageRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<IReadOnlyDictionary<int, SyncRun>> GetLatestByScheduleAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    private IEnumerable<SyncRun> Pending => Stored.Values.Where(r => r.Status == SyncRunStatus.Running && r.PendingReportId is not null);

    private static SyncRun Copy(SyncRun run) => new()
    {
        Id = run.Id,
        SyncScheduleId = run.SyncScheduleId,
        ReportType = run.ReportType,
        MarketplaceId = run.MarketplaceId,
        Trigger = run.Trigger,
        TriggeredBy = run.TriggeredBy,
        StartedAt = run.StartedAt,
        CompletedAt = run.CompletedAt,
        Status = run.Status,
        DataStart = run.DataStart,
        DataEnd = run.DataEnd,
        AmazonReportIds = run.AmazonReportIds,
        ImportBatchIds = run.ImportBatchIds,
        Message = run.Message,
        PendingReportId = run.PendingReportId,
        NextCheckAt = run.NextCheckAt,
        PollAttempts = run.PollAttempts,
        BackfillStart = run.BackfillStart,
        BackfillEnd = run.BackfillEnd,
    };
}
