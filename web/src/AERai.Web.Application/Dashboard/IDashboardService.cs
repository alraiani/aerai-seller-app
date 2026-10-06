using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Dashboard;

/// <summary>
/// Builds the dashboard's data.
/// </summary>
public interface IDashboardService
{
    /// <summary>Computes one marketplace's dashboard for a period.</summary>
    /// <param name="marketplace">Marketplace to report on; its currency and time zone apply.</param>
    /// <param name="period">Window to summarize.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The snapshot.</returns>
    Task<DashboardSnapshot> GetSnapshotAsync(Marketplace marketplace, DashboardPeriod period, CancellationToken cancellationToken);
}
