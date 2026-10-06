using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Dashboard;
using AERai.Web.Domain.Ingestion;
using AERai.Web.Domain.Reporting;
using AERai.Web.Domain.Staging;
using AERai.Web.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AERai.Web.Infrastructure.Queries;

/// <summary>
/// EF Core implementation of <see cref="IDashboardQueries"/>. Returns rows only; the rules that turn
/// them into the dashboard live in <see cref="DashboardService"/>.
/// </summary>
/// <param name="dbContext">Scoped database context.</param>
internal sealed class DashboardQueries(AppDbContext dbContext) : IDashboardQueries
{
    /// <inheritdoc/>
    public async Task<IReadOnlyList<SalesLine>> GetSalesLinesAsync(DateTimeOffset from, DateTimeOffset to, string currency, CancellationToken cancellationToken) =>
        await dbContext.SalesLines
            .AsNoTracking()
            .Where(l => l.PurchaseDate >= from && l.PurchaseDate < to)

            // Amazon leaves currency blank on some lines (e.g. zero-price shipped items); those
            // belong to the marketplace's own currency, so they are kept rather than dropped.
            .Where(l => l.Currency == currency || l.Currency == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<InventoryPosition>> GetInventoryPositionsAsync(CancellationToken cancellationToken) =>
        await dbContext.InventoryPositions.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    public Task<SettlementSummary?> GetLatestSettlementAsync(string currency, CancellationToken cancellationToken) =>
        dbContext.SettlementSummaries
            .AsNoTracking()
            .Where(s => s.Currency == currency && s.PeriodEnd != null)
            .OrderByDescending(s => s.PeriodEnd)
            .FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetSoldSkusMissingCostAsync(DateTimeOffset since, CancellationToken cancellationToken) =>
        await dbContext.SalesLines
            .AsNoTracking()
            .Where(l => l.PurchaseDate >= since)
            .Join(dbContext.Products.Where(p => p.CostOfGoods == null), l => l.Sku, p => p.Sku, (l, _) => l)
            .GroupBy(l => l.Sku)
            .OrderByDescending(g => g.Sum(l => l.ItemPrice))
            .Select(g => g.Key)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public Task<int> CountBatchesAwaitingPromotionAsync(CancellationToken cancellationToken) =>
        dbContext.ImportBatches.CountAsync(b => b.Status == ImportBatchStatus.Received, cancellationToken);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SyncGlance>> GetSyncHealthAsync(CancellationToken cancellationToken)
    {
        var schedules = await dbContext.SyncSchedules.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        var runs = await dbContext.SyncRuns
            .AsNoTracking()
            .Where(r => r.Status != SyncRunStatus.Running)
            .GroupBy(r => r.ReportType)
            .Select(g => new
            {
                ReportType = g.Key,
                Latest = g.OrderByDescending(r => r.Id).First(),
                LastSuccess = g.Where(r => r.Status == SyncRunStatus.Succeeded || r.Status == SyncRunStatus.NoData).Max(r => (DateTimeOffset?)r.CompletedAt),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return schedules
            .GroupBy(s => s.ReportType)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var run = runs.FirstOrDefault(r => r.ReportType == g.Key);
                return new SyncGlance(g.Key, g.Any(s => s.IsEnabled), run?.LastSuccess, run?.Latest.StartedAt, run?.Latest.Status, run?.Latest.Message);
            })
            .ToList();
    }

    /// <inheritdoc/>
    public Task<bool> IsSyncPausedAsync(CancellationToken cancellationToken) =>
        dbContext.SyncSettings.AnyAsync(s => s.IsPaused, cancellationToken);

    /// <inheritdoc/>
    public Task<DateTimeOffset?> GetOrdersCoverageStartAsync(CancellationToken cancellationToken) =>
        dbContext.SyncRuns
            .Where(r => r.ReportType == AmazonReportType.Orders && r.Status != SyncRunStatus.Failed && r.Status != SyncRunStatus.Running)
            .MinAsync(r => r.DataStart, cancellationToken);
}
