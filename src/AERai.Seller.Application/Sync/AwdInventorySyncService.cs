using AERai.Seller.Application.Abstractions;
using AERai.Seller.Domain;
using AERai.Seller.Domain.Staging;
using AERai.Seller.SpApiClient.Awd;
using Microsoft.Extensions.Logging;

namespace AERai.Seller.Application.Sync;

public interface IAwdInventorySyncService
{
    Task SyncAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Pulls current Amazon Warehousing and Distribution (AWD) inventory via <c>listInventory</c>.
/// Unlike the Reports-API-backed FBA inventory sync, this is a plain paginated GET through
/// <see cref="AwdApiClient"/> — no create/poll/download/parse cycle, no report type, no TSV.
/// Each page is upserted immediately rather than batched at the end, matching the same
/// incremental-durability principle used by the order backfill.
/// </summary>
public sealed class AwdInventorySyncService(
    AwdApiClient awdApiClient,
    IAwdInventoryRepository awdInventoryRepository,
    ISyncMetadataRepository syncMetadataRepository,
    TimeProvider timeProvider,
    ILogger<AwdInventorySyncService> logger) : IAwdInventorySyncService
{
    public const string SyncJobName = "AwdInventory";

    public async Task SyncAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var now = timeProvider.GetUtcNow();
            var snapshotDate = AmazonBusinessDay.TodayIn(now);

            var totalItems = 0;
            var pageNumber = 0;
            string? nextToken = null;

            do
            {
                pageNumber++;
                progress?.Report($"AWD Inventory: fetching page {pageNumber}...");

                var page = await awdApiClient.ListInventoryAsync(
                    sku: null, nextToken, AwdApiClient.MaxResultsPerPage, cancellationToken);

                var snapshots = page.Inventory.Select(item => new AwdInventorySnapshot
                {
                    Sku = item.Sku,
                    SnapshotDate = snapshotDate,
                    TotalOnhandQuantity = item.TotalOnhandQuantity,
                    TotalInboundQuantity = item.TotalInboundQuantity,
                    AvailableDistributableQuantity = item.AvailableDistributableQuantity,
                    ReservedDistributableQuantity = item.ReservedDistributableQuantity,
                    ReplenishmentQuantity = item.ReplenishmentQuantity,
                    SyncedAt = now,
                }).ToList();

                await awdInventoryRepository.UpsertSnapshotsAsync(snapshots, cancellationToken);
                totalItems += snapshots.Count;

                nextToken = page.NextToken;
            }
            while (nextToken is not null);

            await syncMetadataRepository.RecordResultAsync(SyncJobName, succeeded: true, errorMessage: null, cancellationToken);
            progress?.Report($"AWD Inventory: done, {totalItems} SKU(s) synced.");
            logger.LogInformation("AWD inventory sync completed: {ItemCount} SKUs upserted", totalItems);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AWD inventory sync failed");
            await syncMetadataRepository.RecordResultAsync(SyncJobName, succeeded: false, ex.Message, cancellationToken);
            throw;
        }
    }
}
