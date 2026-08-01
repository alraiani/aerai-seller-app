using AERai.Seller.Application.Abstractions;
using AERai.Seller.Application.Reports;
using AERai.Seller.SpApiClient.Reports;
using Microsoft.Extensions.Logging;

namespace AERai.Seller.Application.Sync;

public interface IInventorySyncService
{
    Task SyncAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Pulls current FBA inventory (per-SKU, per-state) via the Reports API and upserts it locally.
/// Follows the request -> poll -> download -> parse -> idempotent-upsert pattern documented in
/// the add-report-sync-job skill.
/// </summary>
public sealed class InventorySyncService(
    ReportsApiClient reportsApiClient,
    IInventoryRepository inventoryRepository,
    ISyncMetadataRepository syncMetadataRepository,
    TimeProvider timeProvider,
    ILogger<InventorySyncService> logger) : IInventorySyncService
{
    public const string SyncJobName = "Inventory";
    private const string ReportType = "GET_FBA_INVENTORY_PLANNING_DATA";

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan PollTimeout = TimeSpan.FromMinutes(10);

    public async Task SyncAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var reportId = await reportsApiClient.CreateReportAsync(
                ReportType, dataStartTime: null, dataEndTime: null, cancellationToken);

            var status = await PollUntilDoneAsync(reportId, cancellationToken);
            if (status.ReportDocumentId is null)
            {
                throw new InvalidOperationException($"Report {reportId} finished as {status.ProcessingStatus} with no document.");
            }

            var document = await reportsApiClient.GetReportDocumentAsync(status.ReportDocumentId, cancellationToken);
            var content = await reportsApiClient.DownloadReportDocumentAsync(document, cancellationToken);

            var rows = TsvReportParser.Parse(content);
            var snapshotDate = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
            var snapshots = InventoryPlanningReportParser.Parse(rows, snapshotDate, timeProvider.GetUtcNow());

            await inventoryRepository.UpsertSnapshotsAsync(snapshots, cancellationToken);
            await syncMetadataRepository.RecordResultAsync(SyncJobName, succeeded: true, errorMessage: null, cancellationToken);

            logger.LogInformation("Inventory sync completed: {Count} snapshots upserted", snapshots.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Inventory sync failed");
            await syncMetadataRepository.RecordResultAsync(SyncJobName, succeeded: false, ex.Message, cancellationToken);
            throw;
        }
    }

    private async Task<ReportsApiClient.ReportStatus> PollUntilDoneAsync(string reportId, CancellationToken cancellationToken)
    {
        var deadline = timeProvider.GetUtcNow() + PollTimeout;

        while (true)
        {
            var status = await reportsApiClient.GetReportAsync(reportId, cancellationToken);

            switch (status.ProcessingStatus)
            {
                case "DONE":
                    return status;
                case "CANCELLED":
                case "FATAL":
                    throw new InvalidOperationException($"Report {reportId} ended with status {status.ProcessingStatus}.");
            }

            if (timeProvider.GetUtcNow() > deadline)
            {
                throw new TimeoutException($"Report {reportId} did not finish within {PollTimeout}.");
            }

            await Task.Delay(PollInterval, timeProvider, cancellationToken);
        }
    }
}
