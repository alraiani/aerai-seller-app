namespace AERai.Web.Application.Dashboard;

/// <summary>
/// Builds the dashboard's data.
/// </summary>
public interface IDashboardService
{
    /// <summary>Computes the dashboard for a period.</summary>
    /// <param name="period">Window to summarize.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The snapshot.</returns>
    Task<DashboardSnapshot> GetSnapshotAsync(DashboardPeriod period, CancellationToken cancellationToken);
}
