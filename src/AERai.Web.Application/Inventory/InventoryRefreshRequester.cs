using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Ingestion;
using AERai.Web.Domain.Ingestion;
using Microsoft.Extensions.Logging;

namespace AERai.Web.Application.Inventory;

/// <summary>Default <see cref="IInventoryRefreshRequester"/>: a "Run now" of the matching schedules.</summary>
/// <remarks>
/// Runs go through the same queue as the Schedules page's Run now, so they also happen while scheduled
/// syncs are paused. A marketplace without such schedules gets no run; the next promotion of any
/// inventory data reconciles pending sends anyway.
/// </remarks>
/// <param name="schedules">Schedule lookups.</param>
/// <param name="queue">Scheduler inbox for manual runs.</param>
/// <param name="connection">Amazon connection state.</param>
/// <param name="logger">Logger.</param>
public sealed partial class InventoryRefreshRequester(ISyncScheduleRepository schedules, IManualRunChannel queue, IAmazonConnectionInfo connection, ILogger<InventoryRefreshRequester> logger)
    : IInventoryRefreshRequester
{
    /// <inheritdoc/>
    public async Task<int> RequestAsync(string marketplaceId, string user, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marketplaceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(user);

        if (!connection.CanRun)
        {
            return 0;
        }

        var matching = (await schedules.ListAsync(cancellationToken).ConfigureAwait(false))
            .Where(s => s.MarketplaceId == marketplaceId && s.ReportType is AmazonReportType.FbaInventory or AmazonReportType.RestockRecommendations)
            .ToList();
        foreach (var schedule in matching)
        {
            await queue.EnqueueAsync(new ManualRunRequest(schedule.Id, user), cancellationToken).ConfigureAwait(false);
        }

        LogRequested(matching.Count, marketplaceId);
        return matching.Count;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Queued {Runs} inventory sync runs in {MarketplaceId} after a send to Amazon")]
    private partial void LogRequested(int runs, string marketplaceId);
}
