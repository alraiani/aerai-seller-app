namespace AERai.Web.Application.Dashboard;

/// <summary>
/// Builds the dashboard's data.
/// </summary>
public interface IDashboardService
{
    /// <summary>Computes the current dashboard snapshot.</summary>
    /// <returns>The snapshot.</returns>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken);
}
