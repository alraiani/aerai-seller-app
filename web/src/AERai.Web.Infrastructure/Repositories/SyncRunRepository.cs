using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Domain.Ingestion;
using AERai.Web.Infrastructure.Persistence;
using AERai.Web.Infrastructure.Queries;
using Microsoft.EntityFrameworkCore;

namespace AERai.Web.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="ISyncRunRepository"/>.
/// </summary>
/// <param name="dbContext">Scoped database context.</param>
internal sealed class SyncRunRepository(AppDbContext dbContext) : ISyncRunRepository
{
    /// <inheritdoc/>
    public async Task<long> StartAsync(SyncRun run, CancellationToken cancellationToken)
    {
        dbContext.SyncRuns.Add(run);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        dbContext.Entry(run).State = EntityState.Detached;
        return run.Id;
    }

    /// <inheritdoc/>
    public Task CompleteAsync(SyncRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);

        return dbContext.SyncRuns
            .Where(r => r.Id == run.Id)
            .ExecuteUpdateAsync(set => set
                .SetProperty(r => r.Status, run.Status)
                .SetProperty(r => r.CompletedAt, run.CompletedAt)
                .SetProperty(r => r.DataStart, run.DataStart)
                .SetProperty(r => r.DataEnd, run.DataEnd)
                .SetProperty(r => r.AmazonReportIds, run.AmazonReportIds)
                .SetProperty(r => r.ImportBatchIds, run.ImportBatchIds)
                .SetProperty(r => r.Message, run.Message == null ? null : run.Message.Substring(0, Math.Min(run.Message.Length, 4000))),
                cancellationToken);
    }

    /// <inheritdoc/>
    public Task<PagedResult<SyncRun>> ListAsync(string marketplaceId, int? scheduleId, PageRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = dbContext.SyncRuns.AsNoTracking().Where(r => r.MarketplaceId == marketplaceId);
        if (scheduleId is { } id)
        {
            query = query.Where(r => r.SyncScheduleId == id);
        }

        return query.OrderByDescending(r => r.Id).ToPagedResultAsync(request, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<int, SyncRun>> GetLatestByScheduleAsync(CancellationToken cancellationToken)
    {
        var latestIds = dbContext.SyncRuns.GroupBy(r => r.SyncScheduleId).Select(g => g.Max(r => r.Id));
        return await dbContext.SyncRuns
            .AsNoTracking()
            .Where(r => latestIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.SyncScheduleId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task<bool> IsReportIngestedAsync(string amazonReportId, CancellationToken cancellationToken) =>
        dbContext.IngestedReports.AnyAsync(r => r.AmazonReportId == amazonReportId, cancellationToken);

    /// <inheritdoc/>
    public async Task AddIngestedReportAsync(IngestedReport report, CancellationToken cancellationToken)
    {
        dbContext.IngestedReports.Add(report);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        dbContext.Entry(report).State = EntityState.Detached;
    }
}
