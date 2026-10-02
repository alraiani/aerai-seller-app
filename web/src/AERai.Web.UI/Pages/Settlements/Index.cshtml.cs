using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Domain.Reporting;
using AERai.Web.UI.Models;

namespace AERai.Web.UI.Pages.Settlements;

/// <summary>
/// Settlement payouts (from <c>rpt.vw_SettlementSummary</c>), newest period first.
/// </summary>
/// <param name="reporting">Reporting queries.</param>
public sealed class IndexModel(IReportingQueries reporting) : ListPageModel
{
    /// <summary>The current page of settlements.</summary>
    public PagedResult<SettlementSummary> Settlements { get; private set; } = default!;

    /// <summary>Loads the requested page.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the page is loaded.</returns>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Settlements = await reporting.GetSettlementsAsync(ToPageRequest(), cancellationToken);
    }
}
