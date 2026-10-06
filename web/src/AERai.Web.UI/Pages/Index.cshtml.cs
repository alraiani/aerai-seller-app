using AERai.Web.Application.Dashboard;
using AERai.Web.Application.Marketplaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace AERai.Web.UI.Pages;

/// <summary>
/// Dashboard: headline sales metrics for a period, a sales chart, what needs attention, best
/// sellers, inventory health, the latest payout, and data freshness.
/// </summary>
/// <param name="dashboard">Dashboard service.</param>
/// <param name="currentMarketplace">The marketplace the user is viewing.</param>
/// <param name="options">Dashboard settings (stale threshold for freshness dots).</param>
public sealed class IndexModel(IDashboardService dashboard, ICurrentMarketplace currentMarketplace, IOptions<DashboardOptions> options) : PageModel
{
    /// <summary>Period from <c>?period=today|7d|30d</c>; anything else means 7 days.</summary>
    [BindProperty(SupportsGet = true, Name = "period")]
    public string? PeriodKey { get; set; }

    /// <summary>The selected period.</summary>
    public DashboardPeriod Period => PeriodKey switch
    {
        "today" => DashboardPeriod.Today,
        "30d" => DashboardPeriod.Last30Days,
        _ => DashboardPeriod.Last7Days,
    };

    /// <summary>The computed dashboard data.</summary>
    public DashboardSnapshot Snapshot { get; private set; } = default!;

    /// <summary>Age after which sync data shows as stale.</summary>
    public TimeSpan StaleAfter => TimeSpan.FromHours(options.Value.StaleAfterHours);

    /// <summary>Loads the snapshot.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the snapshot is loaded.</returns>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var selection = await currentMarketplace.GetAsync(cancellationToken);
        Snapshot = await dashboard.GetSnapshotAsync(selection.Current, Period, cancellationToken);
    }
}
