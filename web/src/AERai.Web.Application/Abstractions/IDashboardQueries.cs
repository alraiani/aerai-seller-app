using AERai.Web.Application.Dashboard;
using AERai.Web.Domain.Reporting;

namespace AERai.Web.Application.Abstractions;

/// <summary>
/// Read-only queries the dashboard needs. Returns raw rows; all aggregation and rules live in
/// <see cref="DashboardService"/> so they're unit-testable.
/// </summary>
public interface IDashboardQueries
{
    /// <summary>Sold order items placed in a time window.</summary>
    /// <param name="marketplaceId">Marketplace to report on.</param>
    /// <param name="from">Window start (inclusive).</param>
    /// <param name="to">Window end (exclusive).</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Matching lines.</returns>
    Task<IReadOnlyList<SalesLine>> GetSalesLinesAsync(string marketplaceId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);

    /// <summary>Every SKU's current inventory position.</summary>
    /// <param name="marketplaceId">Marketplace to report on.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>All positions.</returns>
    Task<IReadOnlyList<InventoryPosition>> GetInventoryPositionsAsync(string marketplaceId, CancellationToken cancellationToken);

    /// <summary>The settlement with the latest period end.</summary>
    /// <param name="marketplaceId">Marketplace to report on.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The settlement, or <see langword="null"/>.</returns>
    Task<SettlementSummary?> GetLatestSettlementAsync(string marketplaceId, CancellationToken cancellationToken);

    /// <summary>SKUs sold since a point in time that have no cost of goods entered.</summary>
    /// <param name="marketplaceId">Marketplace to report on.</param>
    /// <param name="since">Lower bound on purchase time.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The SKUs, best-selling first.</returns>
    Task<IReadOnlyList<string>> GetSoldSkusMissingCostAsync(string marketplaceId, DateTimeOffset since, CancellationToken cancellationToken);

    /// <summary>Staging batches waiting for promotion.</summary>
    /// <param name="marketplaceId">Marketplace to report on.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The count.</returns>
    Task<int> CountBatchesAwaitingPromotionAsync(string marketplaceId, CancellationToken cancellationToken);

    /// <summary>Sync freshness and latest outcome per report type.</summary>
    /// <param name="marketplaceId">Marketplace to report on.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>One entry per report type that has a schedule.</returns>
    Task<IReadOnlyList<SyncGlance>> GetSyncHealthAsync(string marketplaceId, CancellationToken cancellationToken);

    /// <summary>Whether an operator has paused all scheduled syncs.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns><see langword="true"/> when paused.</returns>
    Task<bool> IsSyncPausedAsync(CancellationToken cancellationToken);

    /// <summary>Earliest data-window start of any successful Orders run (how far back sales history reaches).</summary>
    /// <param name="marketplaceId">Marketplace to report on.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The earliest start, or <see langword="null"/> when Orders have never synced.</returns>
    Task<DateTimeOffset?> GetOrdersCoverageStartAsync(string marketplaceId, CancellationToken cancellationToken);
}
