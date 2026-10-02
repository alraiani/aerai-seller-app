using AERai.Web.Application.Dashboard;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AERai.Web.UI.Pages;

/// <summary>
/// Dashboard: today's sales by SKU, revenue trend, inventory at risk, and last import.
/// </summary>
/// <param name="dashboard">Dashboard service.</param>
public sealed class IndexModel(IDashboardService dashboard) : PageModel
{
    /// <summary>The computed dashboard data.</summary>
    public DashboardSnapshot Snapshot { get; private set; } = default!;

    /// <summary>Loads the snapshot.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the snapshot is loaded.</returns>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Snapshot = await dashboard.GetSnapshotAsync(cancellationToken);
    }
}
