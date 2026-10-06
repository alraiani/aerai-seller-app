using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Domain.Ingestion;

namespace AERai.Web.Application.Tests.Fakes;

/// <summary>In-memory <see cref="ISyncRunRepository"/>.</summary>
internal sealed class FakeSyncRunRepository : ISyncRunRepository
{
    public List<SyncRun> Completed { get; } = [];

    public List<IngestedReport> Ledger { get; } = [];

    private long _nextId = 1;

    public Task<long> StartAsync(SyncRun run, CancellationToken cancellationToken) => Task.FromResult(_nextId++);

    public Task CompleteAsync(SyncRun run, CancellationToken cancellationToken)
    {
        Completed.Add(run);
        return Task.CompletedTask;
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
}
